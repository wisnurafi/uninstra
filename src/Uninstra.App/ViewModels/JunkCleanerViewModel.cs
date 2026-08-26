namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.App.Services;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed partial class JunkCleanerViewModel : ObservableObject
{
    private readonly IJunkScanner _scanner;

    [ObservableProperty] private ObservableCollection<JunkCategory> _categories = [];
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private long _totalJunkSize;

    private readonly IToastService _toast;

    public JunkCleanerViewModel(IJunkScanner scanner, IToastService toast)
    {
        _scanner = scanner;
        _toast = toast;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusText = "Scanning for junk files...";
        try
        {
            var cats = await _scanner.ScanAsync();
            Categories = new ObservableCollection<JunkCategory>(cats);
            TotalJunkSize = cats.Sum(c => c.DetectedSize);
            _toast.ShowInfo($"{cats.Sum(c => c.ItemCount)} junk items found", "Scan complete");
            StatusText = $"Found {cats.Sum(c => c.ItemCount)} junk items";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; _toast.ShowError(ex.Message, "Junk cleaner failed"); }
        finally { IsScanning = false; }
    }

    [RelayCommand]
    private async Task CleanAllAsync()
    {
        var allItems = Categories.SelectMany(c => c.Items.Where(i => !i.IsLocked)).ToList();
        if (allItems.Count == 0) return;

        StatusText = "Cleaning...";
        var result = await _scanner.CleanAsync(allItems);
        if (result.IsSuccess)
                _toast.ShowSuccess("Junk files cleaned successfully", "Junk cleaner");
            else
                _toast.ShowError(result.Error?.Message ?? "Unknown error", "Junk cleanup failed");
            StatusText = result.IsSuccess ? "Cleanup complete" : $"Cleanup: {result.Error?.Message}";
        await ScanAsync();
    }
}
