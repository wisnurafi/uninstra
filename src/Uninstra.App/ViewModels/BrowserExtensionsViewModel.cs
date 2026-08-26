namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.App.Services;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed partial class BrowserExtensionsViewModel : ObservableObject
{
    private readonly IBrowserExtensionScanner _scanner;

    [ObservableProperty] private ObservableCollection<BrowserExtension> _extensions = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "Ready";

    private readonly IToastService _toast;

    public BrowserExtensionsViewModel(IBrowserExtensionScanner scanner, IToastService toast)
    {
        _scanner = scanner;
        _toast = toast;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = "Scanning browser extensions...";
        try
        {
            var exts = await _scanner.ScanAsync();
            Extensions = new ObservableCollection<BrowserExtension>(exts);
            StatusText = $"{exts.Count} extensions found";
            _toast.ShowInfo($"{exts.Count} browser extensions detected", "Scan complete");
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; _toast.ShowError(ex.Message, "Extension scan failed"); }
        finally { IsLoading = false; }
    }
}
