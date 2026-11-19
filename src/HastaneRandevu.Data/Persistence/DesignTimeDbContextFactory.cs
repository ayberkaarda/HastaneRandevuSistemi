using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HastaneRandevu.Data.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> only. Reads the connection string from HASTANE_CONNECTION and falls back
/// to a local SQL Server Express instance.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HospitalDbContext>
{
    private const string FallbackConnectionString =
        "Server=localhost\\SQLEXPRESS;Database=HastaneRandevu;Trusted_Connection=True;TrustServerCertificate=True;";

    public HospitalDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(DataServiceCollectionExtensions.ConnectionEnvironmentVariable);
        var options = new DbContextOptionsBuilder<HospitalDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connectionString) ? FallbackConnectionString : connectionString)
            .Options;
        return new HospitalDbContext(options);
    }
}
