namespace Uninstra.App;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
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
    private Mutex? _singleInstanceMutex;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single-instance guard: a second copy racing this one would contend
        // over SQLite writes and quarantine moves. Local\ scope = per user
        // session (data lives under %LOCALAPPDATA%, so separate users may run
        // their own copies concurrently).
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            $@"Local\{Uninstra.Core.UninstraInfo.AppId}.SingleInstance",
            out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "Uninstra is already running.",
                "Uninstra", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Global exception handlers
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Configure Serilog — level comes from saved settings when valid
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Uninstra", "Logs", "uninstra-.log");

        var settingsService = new JsonSettingsService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonSettingsService>.Instance);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(ParseLogLevel(settingsService.Load().LogLevel))
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

        // Initialize database + purge expired quarantine items from previous runs
        try
        {
            var db = Services.GetRequiredService<IDatabaseService>();
            await db.InitializeAsync();
            await Services.GetRequiredService<IQuarantineService>().CleanExpiredAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup initialization failed");
        }

        Log.Information("Uninstra started - v{Version}", Core.UninstraInfo.Version);
    }

    private static LogEventLevel ParseLogLevel(string level) => level switch
    {
        "Verbose" or "Trace" => LogEventLevel.Verbose,
        "Debug" => LogEventLevel.Debug,
        "Warning" => LogEventLevel.Warning,
        "Error" => LogEventLevel.Error,
        "Fatal" => LogEventLevel.Fatal,
        _ => LogEventLevel.Information
    };

    private static void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<IDatabaseService, SqliteDatabaseService>();
        services.AddSingleton<IHistoryRepository, SqliteHistoryRepository>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IQuarantineService, FileQuarantineService>();
        services.AddSingleton<RegistryBackupService>();

        // Windows services
        services.AddSingleton<IApplicationScanner, RegistryApplicationScanner>();
        services.AddSingleton<ILeftoverScanner, LeftoverScanner>();
        services.AddSingleton<IProcessRunner, ProcessRunnerService>();
        services.AddSingleton<IUninstallService, UninstallService>();
        services.AddSingleton<IBrowserExtensionScanner, BrowserExtensionScanner>();
        services.AddSingleton<IWindowsAppScanner, WindowsAppScanner>();
        services.AddSingleton<IJunkScanner, JunkScannerService>();
        services.AddSingleton<IElevatedHelperClient, ElevatedHelperClient>();
        services.AddSingleton<ILeftoverCleanupService, LeftoverCleanupService>();
        services.AddSingleton<IInstallMonitorService, InstallMonitorService>();

        // Application services
        services.AddSingleton<ScanCoordinator>();
        services.AddSingleton<BatchUninstallCoordinator>();
        services.AddSingleton<ReportCoordinator>();
        services.AddSingleton<OperationAuditService>();

        // UI services
        services.AddSingleton<NavigationService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<IToastService, ToastService>();

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

        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Single-instance mutex release skipped");
        }

        base.OnExit(e);
    }

    // Throttle state for identical dispatcher-error dialogs.
    private string? _lastErrorDialogMessage;
    private long _lastErrorDialogTicks;
    private int _suppressedIdenticalErrors;

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled dispatcher exception");

        // Layout/render loops can raise the same exception dozens of times per
        // second; showing one modal per occurrence floods the user. Suppress
        // IDENTICAL messages within a short window — every failure is still logged.
        var now = Environment.TickCount64;
        if (e.Exception.Message == _lastErrorDialogMessage &&
            now - _lastErrorDialogTicks < 5_000)
        {
            _suppressedIdenticalErrors++;
            e.Handled = true;
            return;
        }

        var suppressedNote = _suppressedIdenticalErrors > 0
            ? $"\n\n({_suppressedIdenticalErrors} identical errors were suppressed)"
            : string.Empty;
        _suppressedIdenticalErrors = 0;
        _lastErrorDialogMessage = e.Exception.Message;
        _lastErrorDialogTicks = now;

        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}{suppressedNote}\nThe application will continue running. Check logs for details.",
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
