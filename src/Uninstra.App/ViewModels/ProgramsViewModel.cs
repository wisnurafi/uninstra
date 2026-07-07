namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
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

    public ProgramsViewModel(ScanCoordinator scanCoordinator, IUninstallService uninstallService, ILeftoverScanner leftoverScanner)
    {
        _scanCoordinator = scanCoordinator;
        _uninstallService = uninstallService;
        _leftoverScanner = leftoverScanner;
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

        foreach (var item in selected)
        {
            await DeepUninstallAsync(item);
        }

        await LoadAsync();
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
        await DeepUninstallAsync(item);
        await LoadAsync();
    }

    private async Task DeepUninstallAsync(ProgramItemViewModel item)
    {
        // Phase 1: Run the normal uninstaller
        StatusText = $"Uninstalling: {item.App.DisplayName}...";
        var result = await _uninstallService.UninstallAsync(item.App);

        if (!result.IsSuccess)
        {
            StatusText = $"Failed: {item.App.DisplayName} — {result.Error?.Message}";
            MessageBox.Show(
                $"Uninstall failed for {item.App.DisplayName}:\n\n{result.Error?.Message}",
                "Uninstra", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusText = $"Uninstall complete. Scanning for leftovers...";

        // Phase 2: Scan for leftover files/registry
        var leftovers = await _leftoverScanner.ScanAsync(item.App);

        // Phase 3: Show Deep Uninstall dialog
        var vm = App.Services.GetRequiredService<DeepUninstallViewModel>();
        var dialog = new DeepUninstallDialog(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowResults(item.App.DisplayName, leftovers);
        dialog.ShowDialog();

        if (dialog.CleanupPerformed)
        {
            StatusText = $"Deep uninstall complete: {item.App.DisplayName} — " +
                         $"{dialog.CleanedCount} leftovers cleaned";
        }
        else
        {
            StatusText = leftovers.Count == 0
                ? $"Clean uninstall: {item.App.DisplayName} — no leftovers found"
                : $"Uninstall complete: {item.App.DisplayName} — {leftovers.Count} leftovers skipped";
        }
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
