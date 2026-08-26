namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;

public sealed partial class DeepUninstallViewModel : ObservableObject
{
    private readonly ILeftoverCleanupService _cleanupService;

    [ObservableProperty] private string _appName = "";
    [ObservableProperty] private string _statusText = "Scanning for leftover files and registry entries...";
    [ObservableProperty] private string _phase = "scanning"; // scanning, review, cleaning, done
    [ObservableProperty] private bool _isWorking;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 100;
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private ObservableCollection<LeftoverItemViewModel> _leftovers = [];
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private long _selectedSize;
    [ObservableProperty] private int _totalFound;
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private bool _canClean;

    // Result after cleanup
    [ObservableProperty] private int _cleanedCount;
    [ObservableProperty] private int _failedCount;
    [ObservableProperty] private long _freedBytes;

    public bool DialogResult { get; private set; }

    public DeepUninstallViewModel(ILeftoverCleanupService cleanupService)
    {
        _cleanupService = cleanupService;
    }

    public void SetScanResults(string appName, IReadOnlyList<LeftoverCandidate> candidates)
    {
        AppName = appName;
        TotalFound = candidates.Count;

        Leftovers.Clear();
        foreach (var c in candidates)
        {
            var vm = new LeftoverItemViewModel(c, this);
            Leftovers.Add(vm);
        }

        Phase = "review";
        IsWorking = false;
        UpdateSelection();

        StatusText = candidates.Count > 0
            ? $"Found {candidates.Count} leftover items. Review and select items to clean."
            : "No leftover files or registry entries found. The application was cleanly uninstalled.";
    }

    public void UpdateSelection()
    {
        var selected = Leftovers.Where(l => l.IsSelected && !l.Item.IsProtected).ToList();
        SelectedCount = selected.Count;
        SelectedSize = selected.Sum(l => l.Item.SizeBytes);
        CanClean = SelectedCount > 0;
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in Leftovers)
        {
            if (!item.Item.IsProtected)
                item.IsSelected = true;
        }
        UpdateSelection();
    }

    [RelayCommand]
    private void SelectRecommended()
    {
        foreach (var item in Leftovers)
        {
            item.IsSelected = item.Item.IsSelectedByDefault && !item.Item.IsProtected;
        }
        UpdateSelection();
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var item in Leftovers) item.IsSelected = false;
        UpdateSelection();
    }

    [RelayCommand]
    private async Task CleanSelectedAsync()
    {
        var selected = Leftovers
            .Where(l => l.IsSelected && !l.Item.IsProtected)
            .Select(l => l.Item)
            .ToList();

        if (selected.Count == 0) return;

        Phase = "cleaning";
        IsWorking = true;
        ProgressMax = selected.Count;
        ProgressValue = 0;

        var progress = new Progress<CleanupProgress>(p =>
        {
            ProgressValue = p.Current;
            ProgressText = p.CurrentItem;
            StatusText = $"Cleaning ({p.Current}/{p.Total}): {p.CurrentItem}";
        });

        // Hard-failure guard: an unexpected exception from the cleanup service
        // must surface as dialog state, never as an unhandled dispatcher crash.
        try
        {
            var result = await _cleanupService.CleanAsync(selected, progress, applicationDisplayName: AppName);

            // Populate ALL result state BEFORE flipping the phase: the dialog's
            // phase watcher swaps in the completion panel (and reads these
            // values) the instant Phase becomes "done" — assigning afterwards
            // left the stats showing zeros.
            if (result.IsSuccess && result.Value is not null)
            {
                var summary = result.Value;
                CleanedCount = summary.Cleaned;
                FailedCount = summary.Failed;
                FreedBytes = summary.FreedBytes;
                StatusText = $"Cleanup complete: {summary.Cleaned} items cleaned, {FormatSize(summary.FreedBytes)} freed";
                SummaryText = $"{summary.Cleaned} cleaned";
                if (summary.Failed > 0) SummaryText += $"  |  {summary.Failed} failed";
                if (summary.Skipped > 0) SummaryText += $"  |  {summary.Skipped} skipped";
            }
            else
            {
                StatusText = $"Cleanup error: {result.Error?.Message}";
                SummaryText = "Cleanup encountered errors";
            }

            IsWorking = false;
            Phase = "done";
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            IsWorking = false;
            Phase = "review";
            DialogResult = false;
            StatusText = "Cleanup cancelled — only items finished before the cancel were removed.";
            SummaryText = "Cancelled";
        }
        catch (Exception ex)
        {
            IsWorking = false;
            Phase = "done";
            DialogResult = false;
            StatusText = $"Cleanup error: {ex.Message}";
            SummaryText = "Cleanup encountered errors";
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}

public sealed partial class LeftoverItemViewModel : ObservableObject
{
    public LeftoverCandidate Item { get; }
    private readonly DeepUninstallViewModel _parent;

    [ObservableProperty] private bool _isSelected;

    public string DisplayName => Item.DisplayName;
    public string TypeName => Item.Type.ToString();
    public string Location => Item.Path ?? Item.RegistryPath ?? "";
    public string Confidence => $"{Item.ConfidenceScore}% ({Item.ConfidenceLevel})";
    public string Risk => Item.RiskLevel.ToString();
    public long Size => Item.SizeBytes;
    public bool IsProtected => Item.IsProtected;
    public string Evidence => string.Join("; ", Item.Evidence);

    public LeftoverItemViewModel(LeftoverCandidate item, DeepUninstallViewModel parent)
    {
        Item = item;
        _parent = parent;
        _isSelected = item.IsSelectedByDefault && !item.IsProtected;
    }

    partial void OnIsSelectedChanged(bool value) => _parent.UpdateSelection();
}
