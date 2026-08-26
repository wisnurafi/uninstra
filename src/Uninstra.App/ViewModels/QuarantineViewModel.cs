namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using Uninstra.Application.Interfaces;
using Uninstra.App.Services;
using Uninstra.Core.Models;

public sealed partial class QuarantineViewModel : ObservableObject
{
    private readonly IQuarantineService _quarantine;
    private readonly IToastService _toast;

    [ObservableProperty] private ObservableCollection<QuarantineManifest> _items = [];
    [ObservableProperty] private string _statusText = "Quarantine is empty";
    [ObservableProperty] private bool _isLoading;

    public QuarantineViewModel(IQuarantineService quarantine, IToastService toast)
    {
        _quarantine = quarantine;
        _toast = toast;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _quarantine.GetAllAsync();
            Items = new ObservableCollection<QuarantineManifest>(all);
            StatusText = Items.Count == 0
                ? "Quarantine is empty"
                : $"{Items.Count} quarantined item(s)";
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading quarantine: {ex.Message}";
            _toast.ShowError($"Failed to load quarantine: {ex.Message}", "Quarantine");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Restore(QuarantineManifest? manifest)
    {
        if (manifest is null) return;

        var confirm = MessageBox.Show(
            $"Restore \"{Path.GetFileName(manifest.OriginalPath.TrimEnd('\\', '/'))}\" back to its original location?\n\n" +
            $"From: {manifest.OriginalPath}",
            "Confirm restore", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsWorking = true;
        try
        {
            var result = await _quarantine.RestoreAsync(manifest);
            if (result.IsSuccess)
            {
                if (result.Warnings.Count > 0)
                {
                    _toast.ShowWarning(
                        $"Restored with notes:\n{string.Join("\n", result.Warnings)}",
                        "Restore complete");
                    StatusText = "Restored with warnings — see notification";
                }
                else
                {
                    var dest = manifest.OriginalPath.TrimEnd('\\', '/');
                    _toast.ShowSuccess($"\"{Path.GetFileName(dest)}\" restored to its original location.", "Restore complete");
                    StatusText = $"Restored successfully: {dest}";
                }
            }
            else
            {
                _toast.ShowError(
                    $"Restore failed: {result.Error?.Message}\n\nThe item stays in quarantine.",
                    "Restore failed");
                StatusText = $"Restore failed: {result.Error?.Message}";
            }
        }
        finally
        {
            IsWorking = false;
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task PermanentDelete(QuarantineManifest? manifest)
    {
        if (manifest is null) return;

        var confirm = MessageBox.Show(
            $"PERMANENTLY delete \"{Path.GetFileName(manifest.QuarantinePath)}\"?\n\n" +
            "This cannot be undone.",
            "Confirm permanent deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        IsWorking = true;
        try
        {
            var result = await _quarantine.PermanentDeleteAsync(manifest);
            if (result.IsSuccess)
            {
                _toast.ShowInfo("Item permanently deleted.", "Quarantine");
                StatusText = "Item permanently deleted";
            }
            else
            {
                _toast.ShowError($"Delete failed: {result.Error?.Message}", "Delete failed");
                StatusText = $"Delete failed: {result.Error?.Message}";
            }
        }
        finally
        {
            IsWorking = false;
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task CleanExpired()
    {
        await _quarantine.CleanExpiredAsync();
        _toast.ShowInfo("Expired items purged.", "Quarantine");
        StatusText = "Expired items purged";
        await LoadAsync();
    }

    [ObservableProperty] private bool _isWorking;
}
