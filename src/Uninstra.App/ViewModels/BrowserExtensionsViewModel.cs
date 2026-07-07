namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed partial class BrowserExtensionsViewModel : ObservableObject
{
    private readonly IBrowserExtensionScanner _scanner;

    [ObservableProperty] private ObservableCollection<BrowserExtension> _extensions = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "Ready";

    public BrowserExtensionsViewModel(IBrowserExtensionScanner scanner) => _scanner = scanner;

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
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }
}
