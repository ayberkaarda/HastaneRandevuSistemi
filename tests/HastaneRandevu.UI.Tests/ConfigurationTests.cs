using HastaneRandevu.Data;
using Microsoft.Extensions.Configuration;

namespace HastaneRandevu.UI.Tests;

public sealed class ConfigurationTests
{
    private const string ExampleFileName = "appsettings.example.json";

    [Fact]
    public void ExampleConfiguration_LoadsWithNamedInstance()
    {
        Assert.True(
            File.Exists(Path.Combine(AppContext.BaseDirectory, ExampleFileName)),
            $"{ExampleFileName} was not copied next to the tests.");

        // Same loader the application uses for appsettings.json; a malformed file throws here.
        var configuration = new ConfigurationBuilder()
            .AddSettingsFile(AppContext.BaseDirectory, ExampleFileName)
            .Build();

        var connectionString = AppConfiguration.ResolveConnectionString(configuration);

        Assert.NotNull(connectionString);
        Assert.Contains(@"Server=localhost\SQLEXPRESS;", connectionString, StringComparison.Ordinal);
        Assert.Contains("Database=HastaneRandevu;", connectionString, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\\", connectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void EnvironmentVariable_OverridesSettingsFile()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:HastaneRandevu"] = "Server=from-file;",
                [DataServiceCollectionExtensions.ConnectionEnvironmentVariable] = "Server=from-environment;",
            })
            .Build();

        Assert.Equal("HASTANE_CONNECTION", DataServiceCollectionExtensions.ConnectionEnvironmentVariable);
        Assert.Equal("Server=from-environment;", AppConfiguration.ResolveConnectionString(configuration));
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("0", null, false)]
    [InlineData("yes", null, false)]
    [InlineData(null, "false", false)]
    [InlineData("1", null, true)]
    [InlineData("TRUE", null, true)]
    [InlineData(null, "true", true)]
    public void DemoMode_IsOffUnlessSwitchedOnExplicitly(string? environment, string? setting, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DataServiceCollectionExtensions.DemoEnvironmentVariable] = environment,
                [AppConfiguration.DemoSettingKey] = setting,
            })
            .Build();

        Assert.Equal(expected, AppConfiguration.ResolveDemoMode(configuration));
    }

    [Fact]
    public void ExampleConfiguration_LeavesDemoModeOff()
    {
        var configuration = new ConfigurationBuilder()
            .AddSettingsFile(AppContext.BaseDirectory, ExampleFileName)
            .Build();

        Assert.Equal("HASTANE_DEMO", DataServiceCollectionExtensions.DemoEnvironmentVariable);
        Assert.False(AppConfiguration.ResolveDemoMode(configuration));
    }

    [Fact]
    public void SettingsFile_IsUsedWithoutEnvironmentVariable()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:HastaneRandevu"] = "Server=from-file;" })
            .Build();

        Assert.Equal("Server=from-file;", AppConfiguration.ResolveConnectionString(configuration));
    }
}
