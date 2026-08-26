namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Uninstra.Application.Interfaces;
using Uninstra.Application.Services;
using Uninstra.App.Views;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;

public sealed partial class ProgramsViewModel : ObservableObject
{
    private readonly ScanCoordinator _scanCoordinator;
    private readonly IUninstallService _uninstallService;
    private readonly ILeftoverScanner _leftoverScanner;
    private readonly ISettingsService _settingsService;
    private readonly OperationAuditService _auditService;
    private readonly IElevatedHelperClient _elevatedClient;
    private readonly ILogger<ProgramsViewModel> _logger;
    private List<InstalledApplication> _allApps = [];

    [ObservableProperty] private ObservableCollection<ProgramItemViewModel> _programs = [];
    [ObservableProperty] private ProgramItemViewModel? _selectedProgram;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedCategory = "All Programs";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private int _totalPrograms;
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private long _selectedTotalSize;
    [ObservableProperty] private bool _hasSelection;

    public ProgramsViewModel(
        ScanCoordinator scanCoordinator,
        IUninstallService uninstallService,
        ILeftoverScanner leftoverScanner,
        ISettingsService settingsService,
        OperationAuditService auditService,
        IElevatedHelperClient elevatedClient,
        ILogger<ProgramsViewModel> logger)
    {
        _scanCoordinator = scanCoordinator;
        _uninstallService = uninstallService;
        _leftoverScanner = leftoverScanner;
        _settingsService = settingsService;
        _auditService = auditService;
        _elevatedClient = elevatedClient;
        _logger = logger;
    }

    public string[] Categories { get; } =
    [
        "All Programs", "Recently Installed", "Large Programs",
        "Infrequently Used", "Bundleware", "Logged Programs",
        "Windows Updates", "System Components"
    ];

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = "Scanning installed applications...";

        try
        {
            _allApps = [.. await _scanCoordinator.ScanAsync(forceRefresh: true)];
            ApplyFilters();
            StatusText = $"{_allApps.Count} applications found";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var filtered = _allApps.AsEnumerable();

        // Category filter
        filtered = SelectedCategory switch
        {
            "Recently Installed" => filtered.Where(a => a.InstallDate >= DateTime.Now.AddDays(-30)),
            "Large Programs" => filtered.Where(a => a.EstimatedSizeBytes >= 500 * 1024 * 1024L),
            "Windows Updates" => filtered.Where(a => a.IsUpdate),
            "System Components" => filtered.Where(a => a.IsSystemComponent || a.IsRuntime),
            "Infrequently Used" => filtered.Where(a => a.InstallDate < DateTime.Now.AddDays(-180)),
            _ => filtered.Where(a => !a.IsSystemComponent || SelectedCategory == "All Programs")
        };

        // Search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            filtered = filtered.Where(a =>
                a.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.DisplayVersion.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                a.InstallLocation.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        // Exclude system components from "All Programs" by default
        if (SelectedCategory == "All Programs")
            filtered = filtered.Where(a => !a.IsSystemComponent);

        Programs = new ObservableCollection<ProgramItemViewModel>(
            filtered.Select(a => new ProgramItemViewModel(a, this)));
        TotalPrograms = Programs.Count;
        UpdateSelection();
    }

    public void UpdateSelection()
    {
        var selected = Programs.Where(p => p.IsSelected).ToList();
        SelectedCount = selected.Count;
        SelectedTotalSize = selected.Sum(p => p.App.EstimatedSizeBytes);
        HasSelection = SelectedCount > 0;
    }

    [RelayCommand]
    private async Task UninstallSelectedAsync()
    {
        var selected = Programs.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0) return;

        var settings = _settingsService.Load();

        // One confirmation for the whole batch (P0: settings were never consulted)
        if (settings.ConfirmBeforeUninstall)
        {
            var names = string.Join("\n  • ", selected.Take(8).Select(s => s.App.DisplayName));
            if (selected.Count > 8) names += $"\n  • …and {selected.Count - 8} more";
            var confirm = MessageBox.Show(
                $"Deep uninstall {selected.Count} program(s)?\n\n  • {names}\n\n" +
                "Each program's own uninstaller runs first, then leftovers are scanned.",
                "Confirm batch uninstall", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }

        foreach (var item in selected.ToList())
        {
            var ok = await DeepUninstallCoreAsync(item, askConfirmation: false);
            if (!ok) break; // user declined mid-batch dialogs / critical failure
        }

        ClearSelection();
        if (settings.RefreshAfterUninstall) await LoadAsync();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var p in Programs) p.IsSelected = false;
        UpdateSelection();
    }

    [RelayCommand]
    private async Task UninstallSingle(ProgramItemViewModel? item)
    {
        if (item is null) return;
        await DeepUninstallCoreAsync(item, askConfirmation: true);
        if (_settingsService.Load().RefreshAfterUninstall) await LoadAsync();
    }

    /// <summary>
    /// Full deep-uninstall pipeline for one program:
    /// confirm → restore point → uninstall → CANCELLED GUARD → leftover scan → review dialog → audit trail.
    /// Returns false when the flow should stop (user cancel / hard failure).
    /// </summary>
    private async Task<bool> DeepUninstallCoreAsync(ProgramItemViewModel item, bool askConfirmation)
    {
        var app = item.App;
        var settings = _settingsService.Load();

        if (askConfirmation && settings.ConfirmBeforeUninstall)
        {
            var confirm = MessageBox.Show(
                $"Deep uninstall {app.DisplayName}?\n\n" +
                "Its uninstaller runs first, then Uninstra scans for leftover files and registry entries.",
                "Confirm deep uninstall", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return false;
        }

        if (app.IsProtected && !settings.AdvancedMode)
        {
            MessageBox.Show(
                $"{app.DisplayName} is a protected application.\n" +
                "Enable Advanced Mode in Settings to override this protection.",
                "Protected application", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var audit = await _auditService.StartAsync(OperationType.DeepUninstall, app);

        // Optional restore point before touching anything.
        // Best-effort: an elevation/pipe failure must never abort the uninstall —
        // it only downgrades the safety net (recorded in the audit trail).
        var restorePointStatus = "";
        if (settings.CreateRestorePoint)
        {
            StatusText = $"Creating system restore point before removing {app.DisplayName}...";
            try
            {
                var rp = await _elevatedClient.ExecuteAsync(
                    ElevatedOperationType.CreateRestorePoint,
                    $"Before uninstalling {app.DisplayName}");
                restorePointStatus = rp.Success ? "Created" : rp.Message;
                StatusText = rp.Success
                    ? "Restore point created."
                    : $"Restore point unavailable ({rp.Message}). Continuing.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Restore-point elevation failed before uninstalling {App}", app.DisplayName);
                restorePointStatus = $"Unavailable: {ex.Message}";
                StatusText = $"Restore point unavailable ({ex.Message}). Continuing.";
            }
        }

        // ── Phase 1: run the vendor uninstaller ──
        StatusText = $"Uninstalling: {app.DisplayName}...";
        var result = await _uninstallService.UninstallAsync(app);

        if (!result.IsSuccess)
        {
            StatusText = $"Failed: {app.DisplayName} — {result.Error?.Message}";
            await _auditService.CompleteAsync(audit, UninstallStatus.Failed,
                restorePointStatus: restorePointStatus,
                errorCount: 1);
            MessageBox.Show(
                $"Uninstall failed for {app.DisplayName}:\n\n{result.Error?.Message}",
                "Uninstra", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // ── P0 SAFETY GUARD ──────────────────────────────────────────
        // A cancelled uninstall means the app is STILL INSTALLED.
        // Scanning/deleting its registry keys here would orphan it permanently.
        if (result.Value == UninstallStatus.Cancelled)
        {
            StatusText = $"Cancelled: {app.DisplayName} was not uninstalled. No cleanup performed.";
            await _auditService.CompleteAsync(audit, UninstallStatus.Cancelled,
                restorePointStatus: restorePointStatus);
            return false;
        }

        StatusText = "Uninstall complete. Scanning for leftovers...";

        // ── Phase 2: leftover scan ──
        var leftovers = await _leftoverScanner.ScanAsync(app);

        // ── Phase 3: review dialog ──
        var vm = App.Services.GetRequiredService<DeepUninstallViewModel>();
        var dialog = new DeepUninstallDialog(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowResults(app.DisplayName, leftovers);
        dialog.ShowDialog();

        var cleanedCount = dialog.CleanupPerformed ? dialog.CleanedCount : 0;
        var freedBytes = dialog.CleanupPerformed ? dialog.FreedBytes : 0;

        if (dialog.CleanupPerformed)
        {
            StatusText = $"Deep uninstall complete: {app.DisplayName} — {cleanedCount} leftovers cleaned";
        }
        else
        {
            StatusText = leftovers.Count == 0
                ? $"Clean uninstall: {app.DisplayName} — no leftovers found"
                : $"Uninstall complete: {app.DisplayName} — {leftovers.Count} leftovers skipped";
        }

        await _auditService.CompleteAsync(audit, result.Value,
            itemsDetected: leftovers.Count,
            itemsCleaned: cleanedCount,
            itemsSkipped: dialog.CleanupPerformed ? Math.Max(0, leftovers.Count - cleanedCount) : leftovers.Count,
            recoveredBytes: freedBytes,
            restorePointStatus: restorePointStatus,
            quarantineAvailable: cleanedCount > 0);
        return true;
    }

    [RelayCommand]
    private static void OpenInstallLocation(ProgramItemViewModel? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.App.InstallLocation)) return;
        if (Directory.Exists(item.App.InstallLocation))
            Process.Start("explorer.exe", item.App.InstallLocation);
    }
}

public sealed partial class ProgramItemViewModel : ObservableObject
{
    public InstalledApplication App { get; }
    private readonly ProgramsViewModel _parent;

    [ObservableProperty] private bool _isSelected;

    public string DisplayName => App.DisplayName;
    public string Publisher => App.Publisher;
    public string Version => App.DisplayVersion;
    public long Size => App.EstimatedSizeBytes;
    public DateTime? InstallDate => App.InstallDate;
    public string InstallerType => App.InstallerType.ToString();
    public string Architecture => App.Architecture.ToString();
    public bool IsProtected => App.IsProtected;

    public ProgramItemViewModel(InstalledApplication app, ProgramsViewModel parent)
    {
        App = app;
        _parent = parent;
    }

    partial void OnIsSelectedChanged(bool value) => _parent.UpdateSelection();
}
