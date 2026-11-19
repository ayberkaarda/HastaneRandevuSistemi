using HastaneRandevu.Business;
using HastaneRandevu.Business.Auditing;
using HastaneRandevu.Data;
using HastaneRandevu.Data.Persistence;
using HastaneRandevu.UI.Infrastructure;
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
                Log.Warning("Demo mode is on: demo accounts with published passwords are created");
            }

            using var serviceProvider = ConfigureServices(connectionString, demo, policy);

            try
            {
                // No synchronization context exists yet, so blocking here cannot deadlock.
                serviceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync().GetAwaiter().GetResult();
            }
#pragma warning disable CA1031 // Startup boundary: report and exit instead of crashing.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                ErrorReporter.Show(ex);
                return;
            }

            var formFactory = serviceProvider.GetRequiredService<IFormFactory>();
            Application.Run(formFactory.Create<LoginForm>());
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
    }
}
