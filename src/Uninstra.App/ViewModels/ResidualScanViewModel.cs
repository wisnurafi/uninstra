namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;

public sealed partial class ResidualScanViewModel : ObservableObject
{
    private readonly ILeftoverCleanupService _cleanup;
    private readonly List<ResidualItemViewModel> _trackedItems = [];

    [ObservableProperty] private ObservableCollection<ResidualItemViewModel> _residuals = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(CleanSelectedCommand))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(CleanSelectedCommand))]
    private bool _isCleaning;

    [ObservableProperty] private string _statusText = "Ready to scan for residual files";
    [ObservableProperty] private int _selectedCount;

    public ResidualScanViewModel(ILeftoverCleanupService cleanup) => _cleanup = cleanup;

    private bool CanStartScan() => !IsScanning && !IsCleaning;
    private bool CanCleanSelected() => SelectedCount > 0 && !IsScanning && !IsCleaning;

    [RelayCommand(CanExecute = nameof(CanStartScan))]
    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusText = "Scanning for residual files...";
        ReplaceItems([]);

        var found = new List<LeftoverCandidate>();

        await Task.Run(() =>
        {
            // Scan for broken uninstall entries
            var paths = new[]
            {
                (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
            };

            foreach (var (hive, view, path) in paths)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey(path);
                    if (key is null) continue;

                    foreach (var subName in key.GetSubKeyNames())
                    {
                        using var sub = key.OpenSubKey(subName);
                        if (sub is null) continue;

                        var name = sub.GetValue("DisplayName") as string;
                        var uninstall = sub.GetValue("UninstallString") as string;
                        var installLoc = sub.GetValue("InstallLocation") as string;

                        if (string.IsNullOrEmpty(name)) continue;

                        // Check for broken uninstaller
                        if (!string.IsNullOrEmpty(uninstall))
                        {
                            var exe = uninstall.Trim('"').Split(' ')[0].Trim('"');
                            if (!string.IsNullOrEmpty(exe) && !File.Exists(exe) && !exe.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
                            {
                                found.Add(new LeftoverCandidate
                                {
                                    Id = Guid.NewGuid().ToString("N")[..16],
                                    ApplicationId = subName,
                                    DisplayName = $"Broken entry: {name}",
                                    Type = LeftoverType.RegistryKey,
                                    RegistryHive = hive == RegistryHive.LocalMachine ? RegistryHiveType.LocalMachine : RegistryHiveType.CurrentUser,
                                    RegistryPath = $@"{path}\{subName}",
                                    ConfidenceScore = 90,
                                    ConfidenceLevel = ConfidenceLevel.High,
                                    RiskLevel = RiskLevel.Low,
                                    Evidence = [$"Uninstaller not found: {exe}"],
                                    IsSelectedByDefault = false,
                                    SourceScanner = "ResidualScan"
                                });
                            }
                        }

                        // Check for missing install location
                        if (!string.IsNullOrEmpty(installLoc) && !Directory.Exists(installLoc))
                        {
                            found.Add(new LeftoverCandidate
                            {
                                Id = Guid.NewGuid().ToString("N")[..16],
                                ApplicationId = subName,
                                DisplayName = $"Missing location: {name}",
                                Type = LeftoverType.RegistryKey,
                                RegistryHive = hive == RegistryHive.LocalMachine ? RegistryHiveType.LocalMachine : RegistryHiveType.CurrentUser,
                                RegistryPath = $@"{path}\{subName}",
                                ConfidenceScore = 85,
                                ConfidenceLevel = ConfidenceLevel.High,
                                RiskLevel = RiskLevel.Low,
                                Evidence = [$"Install location missing: {installLoc}"],
                                IsSelectedByDefault = false,
                                SourceScanner = "ResidualScan"
                            });
                        }
                    }
                }
                catch { }
            }
        });

        ReplaceItems(found.Select(c => new ResidualItemViewModel(c)));
        StatusText = found.Count == 0
            ? "No residual items found"
            : $"Found {found.Count} residual items";
        IsScanning = false;
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in _trackedItems)
            item.IsSelected = true;
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var item in _trackedItems)
            item.IsSelected = false;
    }

    [RelayCommand(CanExecute = nameof(CanCleanSelected))]
    private async Task CleanSelectedAsync()
    {
        var targets = _trackedItems
            .Where(i => i.IsSelected)
            .Select(i => i.Candidate)
            .ToList();
        if (targets.Count == 0) return;

        var confirm = MessageBox.Show(
            $"Clean {targets.Count} selected residual item(s)?\n\n" +
            "Registry entries are exported to a .reg backup first, and files are moved to quarantine so this can be undone.",
            "Confirm cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsCleaning = true;
        try
        {
            var progress = new Progress<CleanupProgress>(p =>
                StatusText = $"{p.Status}: {p.CurrentItem} ({p.Current}/{p.Total})");

            var result = await _cleanup.CleanAsync(targets, progress);
            if (!result.IsSuccess)
            {
                StatusText = $"Cleanup failed: {result.Error?.Message}";
                return;
            }

            var summary = result.Value ?? new CleanupSummary(targets.Count, 0, targets.Count, 0, 0);

            // Drop only entries verifiably gone from the system; anything still
            // present (failed deletion) stays visible so the user can retry.
            var removedIds = await Task.Run(() =>
                targets.Where(VerifyGone).Select(c => c.Id).ToHashSet());
            ReplaceItems(_trackedItems.Where(i => !removedIds.Contains(i.Candidate.Id)));

            StatusText =
                $"Cleaned {summary.Cleaned}, failed {summary.Failed}, skipped {summary.Skipped}" +
                $" - freed {FormatBytes(summary.FreedBytes)}" +
                $"; {_trackedItems.Count} item(s) remaining";
        }
        finally
        {
            IsCleaning = false;
        }
    }

    [RelayCommand]
    private void OpenLocation(ResidualItemViewModel? item)
    {
        if (item is null || !item.HasOpenableLocation) return;

        try
        {
            var path = item.Candidate.Path;
            if (File.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            else if (Directory.Exists(path))
                System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
            else
                StatusText = "Location no longer exists on disk";
        }
        catch (Exception ex)
        {
            StatusText = $"Could not open location: {ex.Message}";
        }
    }

    private void ReplaceItems(IEnumerable<ResidualItemViewModel> items)
    {
        foreach (var tracked in _trackedItems)
            tracked.PropertyChanged -= OnItemPropertyChanged;
        _trackedItems.Clear();

        foreach (var item in items)
        {
            item.PropertyChanged += OnItemPropertyChanged;
            _trackedItems.Add(item);
        }

        Residuals = new ObservableCollection<ResidualItemViewModel>(_trackedItems);
        SelectedCount = 0;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ResidualItemViewModel.IsSelected)) return;
        SelectedCount = _trackedItems.Count(i => i.IsSelected);
        CleanSelectedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Post-cleanup reality check: did the target actually disappear?</summary>
    private static bool VerifyGone(LeftoverCandidate c) => c.Type switch
    {
        LeftoverType.File or LeftoverType.Shortcut => c.Path.Length == 0 || !File.Exists(c.Path),
        LeftoverType.Directory or LeftoverType.EmptyDirectory => c.Path.Length == 0 || !Directory.Exists(c.Path),
        LeftoverType.RegistryKey => RegistryLeafMissing(c),
        LeftoverType.RegistryValue or LeftoverType.StartupEntry => RegistryValueMissing(c),
        _ => false // service/scheduled-task outcomes cannot be verified cheaply; keep listed
    };

    private static RegistryKey OpenHiveBase(LeftoverCandidate c) =>
        RegistryKey.OpenBaseKey(
            c.RegistryHive == RegistryHiveType.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser,
            RegistryView.Default);

    private static bool RegistryLeafMissing(LeftoverCandidate c)
    {
        if (c.RegistryPath.Length == 0) return false;
        try
        {
            var normalized = c.RegistryPath.Replace('/', '\\');
            var idx = normalized.LastIndexOf('\\');
            using var baseKey = OpenHiveBase(c);
            if (idx <= 0)
                return baseKey.OpenSubKey(normalized) is null;

            using var parent = baseKey.OpenSubKey(normalized[..idx]);
            if (parent is null) return true; // parent gone implies leaf gone
            return parent.OpenSubKey(normalized[(idx + 1)..]) is null;
        }
        catch
        {
            return false; // unreadable — keep the entry visible rather than guess
        }
    }

    private static bool RegistryValueMissing(LeftoverCandidate c)
    {
        if (c.RegistryPath.Length == 0 || c.RegistryValueName.Length == 0) return false;
        try
        {
            using var baseKey = OpenHiveBase(c);
            using var key = baseKey.OpenSubKey(c.RegistryPath.Replace('/', '\\'));
            if (key is null) return true;
            return key.GetValue(c.RegistryValueName) is null;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        0 => "0 B",
        < 1024 => $"{bytes} B",
        < 1048576 => $"{bytes / 1024.0:F1} KB",
        < 1073741824 => $"{bytes / 1048576.0:F1} MB",
        _ => $"{bytes / 1073741824.0:F2} GB"
    };
}

/// <summary>Selection-aware wrapper around the immutable LeftoverCandidate for list UI.</summary>
public sealed partial class ResidualItemViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public ResidualItemViewModel(LeftoverCandidate candidate)
    {
        Candidate = candidate;
        _isSelected = candidate.IsSelectedByDefault;
    }

    public LeftoverCandidate Candidate { get; }

    public LeftoverType Type => Candidate.Type;
    public string DisplayName => Candidate.DisplayName;
    public long SizeBytes => Candidate.SizeBytes;
    public int ConfidenceScore => Candidate.ConfidenceScore;

    public string Location =>
        Candidate.Type is LeftoverType.RegistryKey or LeftoverType.RegistryValue or LeftoverType.StartupEntry
            ? Candidate.RegistryPath
            : Candidate.Path.Length > 0
                ? Candidate.Path
                : Candidate.RegistryPath;

    public bool HasOpenableLocation =>
        Candidate.Path.Length > 0 &&
        Candidate.Type is LeftoverType.File or LeftoverType.Shortcut
            or LeftoverType.Directory or LeftoverType.EmptyDirectory;

    public bool IsHighConfidence => Candidate.ConfidenceLevel == ConfidenceLevel.High;
    public bool IsMediumConfidence => Candidate.ConfidenceLevel == ConfidenceLevel.Medium;
    public bool IsLowConfidence => Candidate.ConfidenceLevel == ConfidenceLevel.Low;

    public Geometry TypeIcon =>
        (Geometry?)Application.Current?.TryFindResource(TypeIconKey) ?? Geometry.Empty;

    private string TypeIconKey => Candidate.Type switch
    {
        LeftoverType.File or LeftoverType.Shortcut => "Icon_File",
        LeftoverType.Directory or LeftoverType.EmptyDirectory => "Icon_Folder",
        _ => "Icon_Box" // registry keys/values, services, tasks, startup entries
    };

    public string Details
    {
        get
        {
            var lines = new List<string> { $"Type: {Candidate.Type}", $"Risk level: {Candidate.RiskLevel}" };
            if (Candidate.RegistryHive is { } hive)
                lines.Insert(1, $"Hive: {hive}");
            if (!string.IsNullOrEmpty(Candidate.RegistryValueName))
                lines.Add($"Value: {Candidate.RegistryValueName}");
            lines.AddRange(Candidate.Evidence.Select(ev => $"- {ev}"));
            return string.Join(Environment.NewLine, lines);
        }
    }
}
