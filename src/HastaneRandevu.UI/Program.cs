using HastaneRandevu.Business;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Data;
using HastaneRandevu.Data.Persistence;
using HastaneRandevu.UI.Infrastructure;
using HastaneRandevu.UI.Theming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace HastaneRandevu.UI
{
    internal static class Program
    {
        internal const string ConnectionStringName = "HastaneRandevu";

        /// <summary>Application entry point and composition root.</summary>
        [STAThread]
        private static void Main()
        {
            Log.Logger = AppLogging.CreateLogger(AppLogging.DefaultDirectory);
            try
            {
                Log.Information("Application started");
                Run();
            }
            finally
            {
                Log.Information("Application stopped");
                Log.CloseAndFlush();
            }
        }

        private static void Run()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => ErrorReporter.Show(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.IsTerminating)
                {
                    Log.Fatal("Unhandled exception terminates the application");
                }

                ErrorReporter.Show(e.ExceptionObject as Exception);
                if (e.IsTerminating)
                {
                    Log.CloseAndFlush();
                }
            };

            ApplicationConfiguration.Initialize();

            var configuration = BuildConfiguration();
            ApplyTheme(AppConfiguration.ResolveTheme(configuration));

            var (policy, policyProblems) = AppConfiguration.ReadAppointmentPolicy(configuration);
            if (policyProblems.Count > 0)
            {
                // The problems name settings and limits only, never entered or personal values.
                Log.Warning("The Appointments settings are invalid: {Problems}", string.Join(" ", policyProblems));
                Dialogs.Show(
                    null,
                    "appsettings.json içindeki \"Appointments\" ayarları geçersiz:\n\n" + string.Join("\n", policyProblems),
                    AppText.Title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var connectionString = AppConfiguration.ResolveConnectionString(configuration);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Log.Warning("No database connection string is configured");
                Dialogs.Show(
                    null,
                    "Veritabanı bağlantı ayarı bulunamadı.\n\n"
                    + "appsettings.example.json dosyasını appsettings.json olarak kopyalayıp bağlantı bilgisini girin "
                    + "ya da HASTANE_CONNECTION ortam değişkenini tanımlayın.",
                    AppText.Title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var demo = new DemoDataOptions { Enabled = AppConfiguration.ResolveDemoMode(configuration) };
            if (demo.Enabled)
            {
                Log.Warning("Demo mode is on: demo accounts with published passwords are added to an empty database");
            }

            using var serviceProvider = ConfigureServices(connectionString, demo, policy);

            try
            {
                // No synchronization context exists yet, so blocking here cannot deadlock.
                var outcome = serviceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync().GetAwaiter().GetResult();
                if (outcome == DemoSeedOutcome.SkippedExistingData)
                {
                    Log.Warning("Demo mode is on but the database already has data; no demo account was added");
                }
                else if (outcome == DemoSeedOutcome.Seeded)
                {
                    Log.Warning("Demo accounts were added to the empty database");
                }
            }
#pragma warning disable CA1031 // Startup boundary: report and exit instead of crashing.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorReporter.Show(ex);
                return;
            }

            var formFactory = serviceProvider.GetRequiredService<IFormFactory>();

            // While the application runs, Windows may switch high contrast or the app mode; open screens follow.
            using var themeWatcher = new ThemeWatcher(
                new SystemPreferenceSource(),
                new WindowsFormsSynchronizationContext(),
                () => SystemInformation.HighContrast,
                Theme.SystemPrefersDark);
            Application.Run(formFactory.Create<LoginForm>());
        }

        /// <summary>Chooses the palette; in dark mode the native parts of the controls are switched to dark too.</summary>
        private static void ApplyTheme(ThemeMode mode)
        {
            var highContrast = SystemInformation.HighContrast;
            Theme.Initialize(mode, highContrast, Theme.SystemPrefersDark());
            if (Theme.Current.IsDark && !highContrast)
            {
#pragma warning disable WFO5001 // Dark mode of the native controls is marked experimental in Windows Forms.
                Application.SetColorMode(SystemColorMode.Dark);
#pragma warning restore WFO5001
            }
        }

        private static IConfiguration BuildConfiguration() =>
            new ConfigurationBuilder()
                .AddSettingsFile(AppContext.BaseDirectory, AppConfiguration.SettingsFileName)
                .AddUserSecrets(typeof(Program).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();

        private static ServiceProvider ConfigureServices(string connectionString, DemoDataOptions demo, AppointmentPolicy policy)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IAuditLog>(new SerilogAuditLog(Log.Logger));
            services.AddSqlServerDataAccess(connectionString, demo);
            services.AddBusinessServices(policy);
            if (policy.ShowReminderBalloon)
            {
                services.AddSingleton<IReminderNotifier, NotifyIconReminderNotifier>();
            }
            else
            {
                services.AddSingleton<IReminderNotifier, NullReminderNotifier>();
            }

            services.AddSingleton<IFormFactory, FormFactory>();
            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        }
    }

    /// <summary>How the application reads its settings.</summary>
    internal static class AppConfiguration
    {
        public const string SettingsFileName = "appsettings.json";
        public const string DemoSettingKey = "Seed:Demo";
        public const string ThemeKey = "Appearance:Theme";

        /// <summary>Adds an optional JSON settings file located in <paramref name="basePath"/>.</summary>
        public static IConfigurationBuilder AddSettingsFile(this IConfigurationBuilder builder, string basePath, string fileName) =>
            builder
                .SetBasePath(basePath)
                .AddJsonFile(fileName, optional: true, reloadOnChange: false);

        /// <summary>
        /// Resolution order (last wins): appsettings.json, user secrets, ConnectionStrings__HastaneRandevu
        /// environment variable, HASTANE_CONNECTION environment variable.
        /// </summary>
        public static string? ResolveConnectionString(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var fromEnvironment = configuration[DataServiceCollectionExtensions.ConnectionEnvironmentVariable];
            return string.IsNullOrWhiteSpace(fromEnvironment)
                ? configuration.GetConnectionString(Program.ConnectionStringName)
                : fromEnvironment;
        }

        /// <summary>
        /// Reads the <c>Appointments</c> section (defaults for missing values) and validates it. Values that
        /// cannot be converted (for example text for a number) are reported as problems too.
        /// </summary>
        public static (AppointmentPolicy Policy, IReadOnlyList<string> Problems) ReadAppointmentPolicy(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            AppointmentPolicy policy;
            try
            {
                policy = configuration.GetSection(AppointmentPolicy.SectionName).Get<AppointmentPolicy>() ?? new AppointmentPolicy();
            }
            catch (InvalidOperationException)
            {
                return (new AppointmentPolicy(), ["Ayar değerlerinden biri okunamadı."]);
            }

            return (policy, policy.Validate());
        }

        /// <summary>
        /// Demo mode creates the demo accounts whose passwords are published in the README. It is off
        /// unless HASTANE_DEMO is "1" or "true", or the <c>Seed:Demo</c> setting is true.
        /// </summary>
        public static bool ResolveDemoMode(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            static bool IsOn(string? value) =>
                value is "1" || string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

            return IsOn(configuration[DataServiceCollectionExtensions.DemoEnvironmentVariable]) || IsOn(configuration[DemoSettingKey]);
        }

        /// <summary>The <c>Appearance:Theme</c> setting (Light, Dark or System); anything else falls back to Light.</summary>
        public static ThemeMode ResolveTheme(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var value = configuration[ThemeKey]?.Trim();
            // Only the names are accepted; Enum.TryParse would also take numbers.
            return value is { Length: > 0 } && value.All(char.IsAsciiLetter) && Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var mode)
                ? mode
                : ThemeMode.Light;
        }
    }
}
