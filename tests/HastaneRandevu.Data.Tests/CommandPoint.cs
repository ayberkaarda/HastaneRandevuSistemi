using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HastaneRandevu.Data.Tests;

/// <summary>
/// A deterministic interleaving point inside a repository transaction: the first command that matches
/// signals <see cref="Reached"/> and, unless the point only observes, waits until the test releases it,
/// either before the command is sent or after it completed.
/// </summary>
internal sealed class CommandPoint : DbCommandInterceptor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly Func<string, bool> _matches;
    private readonly bool _after;
    private readonly bool _hold;
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _fired;

    private CommandPoint(Func<string, bool> matches, bool after, bool hold)
    {
        _matches = matches;
        _after = after;
        _hold = hold;
    }

    /// <summary>Completes when the matching command arrived at the point.</summary>
    public Task Reached => _reached.Task;

    /// <summary>Holds the first matching command before it is sent.</summary>
    public static CommandPoint Before(Func<string, bool> matches) => new(matches, after: false, hold: true);

    /// <summary>Holds the transaction right after the first matching command completed.</summary>
    public static CommandPoint After(Func<string, bool> matches) => new(matches, after: true, hold: true);

    /// <summary>Only reports that the first matching command is about to be sent.</summary>
    public static CommandPoint Observe(Func<string, bool> matches) => new(matches, after: false, hold: false);

    public static bool IsAppLock(string sql) => sql.Contains("sp_getapplock", StringComparison.Ordinal);

    public static bool IsDeleteFrom(string sql, string table) =>
        sql.TrimStart().StartsWith("DELETE", StringComparison.Ordinal) && sql.Contains($"[{table}]", StringComparison.Ordinal);

    public static bool IsInsertInto(string sql, string table) =>
        sql.Contains($"INSERT INTO [{table}]", StringComparison.Ordinal) || sql.Contains($"INSERT INTO \"{table}\"", StringComparison.Ordinal);

    public void Release() => _released.TrySetResult();

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await PassAsync(command, after: false, cancellationToken);
        return result;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        await PassAsync(command, after: true, cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await PassAsync(command, after: false, cancellationToken);
        return result;
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await PassAsync(command, after: true, cancellationToken);
        return result;
    }

    private async Task PassAsync(DbCommand command, bool after, CancellationToken cancellationToken)
    {
        if (after != _after || !_matches(command.CommandText) || Interlocked.Exchange(ref _fired, 1) == 1)
        {
            return;
        }

        _reached.TrySetResult();
        if (_hold)
        {
            await _released.Task.WaitAsync(Timeout, cancellationToken);
        }
    }
}
