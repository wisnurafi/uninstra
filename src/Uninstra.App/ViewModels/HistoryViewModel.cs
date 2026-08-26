namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IHistoryRepository _repository;

    [ObservableProperty] private ObservableCollection<HistoryEntry> _records = [];
    [ObservableProperty] private string _statusText = "Ready";

    public HistoryViewModel(IHistoryRepository repository) => _repository = repository;

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var all = await _repository.GetAllAsync();
            Records = new ObservableCollection<HistoryEntry>(all.Select(r => new HistoryEntry(r)));
            StatusText = $"{all.Count} history records";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task ClearAllAsync()
    {
        try
        {
            var all = await _repository.GetAllAsync();
            foreach (var record in all)
                await _repository.DeleteAsync(record.OperationId);
            Records.Clear();
            StatusText = "History cleared";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }
}

/// <summary>
/// Read-only display projection of a <see cref="HistoryRecord"/> for list rows.
/// Adds UI-friendly labels without modifying the Core model.
/// </summary>
public sealed class HistoryEntry
{
    public HistoryEntry(HistoryRecord record) => Record = record;

    public HistoryRecord Record { get; }

    public string ApplicationName =>
        string.IsNullOrWhiteSpace(Record.ApplicationName) ? "Unknown application" : Record.ApplicationName;

    public string TypeName => Record.OperationType switch
    {
        OperationType.NormalUninstall => "Normal Uninstall",
        OperationType.DeepUninstall => "Deep Uninstall",
        OperationType.ForceUninstall => "Force Uninstall",
        OperationType.BatchUninstall => "Batch Uninstall",
        OperationType.LeftoverCleanup => "Leftover Cleanup",
        OperationType.ResidualScan => "Residual Scan",
        OperationType.JunkCleanup => "Junk Cleanup",
        OperationType.QuarantineRestore => "Quarantine Restore",
        OperationType.QuarantineDelete => "Quarantine Delete",
        OperationType.BrowserExtensionRemoval => "Extension Removal",
        OperationType.AppxRemoval => "Appx Removal",
        _ => "Operation"
    };

    public string StatusLabel => Record.Status switch
    {
        UninstallStatus.Completed => "Success",
        UninstallStatus.CompletedWithWarnings => "Warnings",
        UninstallStatus.Cancelled => "Cancelled",
        UninstallStatus.Failed => "Failed",
        _ => "Unknown"
    };

    public DateTime StartedAt => Record.StartedAt;
    public long RecoveredBytes => Record.RecoveredBytes;
    public int ItemsCleaned => Record.ItemsCleaned;
    public int WarningCount => Record.WarningCount;
    public bool HasRecoveredBytes => Record.RecoveredBytes > 0;
    public bool HasItemsCleaned => Record.ItemsCleaned > 0;
    public bool HasWarnings => Record.WarningCount > 0;
}
