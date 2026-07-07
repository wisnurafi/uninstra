namespace Uninstra.Application.Interfaces;

using Uninstra.Core.Models;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}

// Settings model lives here since it bridges app config
public sealed record AppSettings
{
    // General
    public string StartPage { get; init; } = "Programs";
    public bool ConfirmBeforeUninstall { get; init; } = true;
    public bool ConfirmBeforeCleanup { get; init; } = true;
    public bool RefreshAfterUninstall { get; init; } = true;

    // Appearance
    public string Theme { get; init; } = "Dark";
    public string AccentColor { get; init; } = "#00BCD4";
    public bool CompactDensity { get; init; }

    // Scanning
    public bool IncludeMediumConfidence { get; init; }
    public bool ScanAppData { get; init; } = true;
    public bool ScanProgramData { get; init; } = true;
    public bool ScanRegistry { get; init; } = true;
    public bool ScanServices { get; init; } = true;
    public bool ScanScheduledTasks { get; init; } = true;
    public bool ScanStartup { get; init; } = true;
    public bool ShowSystemComponents { get; init; }

    // Safety
    public bool CreateRestorePoint { get; init; } = true;
    public int QuarantineRetentionDays { get; init; } = 14;
    public bool AllowPermanentDeletion { get; init; }
    public bool AdvancedMode { get; init; }
    public bool ShowProtectedItems { get; init; }

    // Logging
    public string LogLevel { get; init; } = "Information";

    // Window
    public double WindowWidth { get; init; } = 1280;
    public double WindowHeight { get; init; } = 800;
    public double WindowLeft { get; init; } = double.NaN;
    public double WindowTop { get; init; } = double.NaN;
    public string SelectedCategory { get; init; } = "All Programs";
    public string SortColumn { get; init; } = "Name";
    public bool SortAscending { get; init; } = true;
}
