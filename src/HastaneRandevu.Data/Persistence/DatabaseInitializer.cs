using Microsoft.EntityFrameworkCore;

namespace HastaneRandevu.Data.Persistence;

/// <summary>Brings the database up to date when the application starts.</summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// Applies pending EF Core migrations (creating the database when it does not exist). A database
    /// created by the original application is upgraded in place first, keeping its data.
    /// Providers without migrations get the schema from the model. No account is created unless demo
    /// mode is switched on (<see cref="DemoDataOptions"/>).
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

internal sealed class DatabaseInitializer(IDbContextFactory<HospitalDbContext> contextFactory, DemoDataOptions demo) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (db.Database.IsSqlServer())
            {
                // A database from the original application has the tables but no migration history.
                await LegacySchemaUpgrader.UpgradeIfLegacyAsync(db, cancellationToken).ConfigureAwait(false);
                await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            }

            if (demo.Enabled)
            {
                await DemoDataSeeder.SeedAsync(db, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new DatabaseInitializationException(ex);
        }
    }
}

/// <summary>Raised when the database cannot be created or migrated at startup.</summary>
public sealed class DatabaseInitializationException : Exception
{
    public DatabaseInitializationException()
        : base("Database initialization failed.")
    {
    }

    public DatabaseInitializationException(Exception innerException)
        : base("Database initialization failed.", innerException)
    {
    }

    public DatabaseInitializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
