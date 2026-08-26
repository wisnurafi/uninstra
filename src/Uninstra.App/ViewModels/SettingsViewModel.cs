namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using Uninstra.Application.Interfaces;

public sealed partial class SettingsViewModel : ObservableObject
{

    private readonly ISettingsService _settingsService;

    [ObservableProperty] private string _selectedTheme = "Dark";
    [ObservableProperty] private bool _confirmBeforeUninstall = true;
    [ObservableProperty] private bool _confirmBeforeCleanup = true;
    [ObservableProperty] private bool _createRestorePoint = true;
    [ObservableProperty] private int _quarantineRetentionDays = 14;
    [ObservableProperty] private bool _advancedMode;
    [ObservableProperty] private bool _scanAppData = true;
    [ObservableProperty] private bool _scanProgramData = true;
    [ObservableProperty] private bool _scanRegistry = true;
    [ObservableProperty] private bool _scanServices = true;
    [ObservableProperty] private bool _scanScheduledTasks = true;
    [ObservableProperty] private bool _scanStartup = true;
    [ObservableProperty] private string _logLevel = "Information";

    // Only Dark ships today — Themes/LightTheme.xaml doesn't exist yet, so
    // offering "Light"/"System" would silently fail inside ApplyTheme.
    public string[] Themes { get; } = ["Dark"];
    public int[] RetentionOptions { get; } = [7, 14, 30, -1]; // -1 = never

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = _settingsService.Load();
        SelectedTheme = s.Theme;
        ConfirmBeforeUninstall = s.ConfirmBeforeUninstall;
        ConfirmBeforeCleanup = s.ConfirmBeforeCleanup;
        CreateRestorePoint = s.CreateRestorePoint;
        QuarantineRetentionDays = s.QuarantineRetentionDays;
        AdvancedMode = s.AdvancedMode;
        ScanAppData = s.ScanAppData;
        ScanProgramData = s.ScanProgramData;
        ScanRegistry = s.ScanRegistry;
        ScanServices = s.ScanServices;
        ScanScheduledTasks = s.ScanScheduledTasks;
        ScanStartup = s.ScanStartup;
        LogLevel = s.LogLevel;
    }

    [RelayCommand]
    private void Save()
    {
        _settingsService.Save(new Uninstra.Application.Interfaces.AppSettings
        {
            Theme = SelectedTheme,
            ConfirmBeforeUninstall = ConfirmBeforeUninstall,
            ConfirmBeforeCleanup = ConfirmBeforeCleanup,
            CreateRestorePoint = CreateRestorePoint,
            QuarantineRetentionDays = QuarantineRetentionDays,
            AdvancedMode = AdvancedMode,
            ScanAppData = ScanAppData,
            ScanProgramData = ScanProgramData,
            ScanRegistry = ScanRegistry,
            ScanServices = ScanServices,
            ScanScheduledTasks = ScanScheduledTasks,
            ScanStartup = ScanStartup,
            LogLevel = LogLevel
        });
    }

    [RelayCommand]
    private static void OpenLogFolder()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Uninstra", "Logs");
        if (Directory.Exists(path))
            Process.Start("explorer.exe", path);
    }
}
