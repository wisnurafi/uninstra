namespace Uninstra.App;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using System.Windows.Threading;
using Uninstra.App.Services;
using Uninstra.App.ViewModels;
using Uninstra.Application.Interfaces;
using Uninstra.Application.Services;
using Uninstra.Infrastructure.Data;
using Uninstra.Infrastructure.Services;
using Uninstra.Windows.Scanning;
using Uninstra.Windows.Services;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Configure Serilog
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Uninstra", "Logs", "uninstra-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(ConfigureServices)
            .Build();

        Services = _host.Services;

        // Initialize database
        var db = Services.GetRequiredService<IDatabaseService>();
        await db.InitializeAsync();

        Log.Information("Uninstra started - v{Version}", Core.UninstraInfo.Version);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<IDatabaseService, SqliteDatabaseService>();
        services.AddSingleton<IHistoryRepository, SqliteHistoryRepository>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IReportService, ReportService>();

        // Windows services
        services.AddSingleton<IApplicationScanner, RegistryApplicationScanner>();
        services.AddSingleton<ILeftoverScanner, LeftoverScanner>();
        services.AddSingleton<IProcessRunner, ProcessRunnerService>();
        services.AddSingleton<IUninstallService, UninstallService>();
        services.AddSingleton<IBrowserExtensionScanner, BrowserExtensionScanner>();
        services.AddSingleton<IWindowsAppScanner, WindowsAppScanner>();
        services.AddSingleton<IJunkScanner, JunkScannerService>();
        services.AddSingleton<ILeftoverCleanupService, LeftoverCleanupService>();

        // Application services
        services.AddSingleton<ScanCoordinator>();
        services.AddSingleton<BatchUninstallCoordinator>();
        services.AddSingleton<ReportCoordinator>();

        // UI services
        services.AddSingleton<NavigationService>();
        services.AddSingleton<ThemeService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ProgramsViewModel>();
        services.AddTransient<SoftwareHealthViewModel>();
        services.AddTransient<InstallMonitorViewModel>();
        services.AddTransient<ForceUninstallViewModel>();
        services.AddTransient<ResidualScanViewModel>();
        services.AddTransient<WindowsAppsViewModel>();
        services.AddTransient<BrowserExtensionsViewModel>();
        services.AddTransient<JunkCleanerViewModel>();
        services.AddTransient<QuarantineViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<DeepUninstallViewModel>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("Uninstra shutting down");
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled dispatcher exception");
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will continue running. Check logs for details.",
            "Uninstra - Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Log.Fatal(ex, "Unhandled domain exception");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
