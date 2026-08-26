namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.App.Services;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed partial class WindowsAppsViewModel : ObservableObject
{
    private readonly IWindowsAppScanner _scanner;

    [ObservableProperty] private ObservableCollection<WindowsApp> _apps = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _searchText = "";
    private List<WindowsApp> _allApps = [];

    private readonly IToastService _toast;

    public WindowsAppsViewModel(IWindowsAppScanner scanner, IToastService toast)
    {
        _scanner = scanner;
        _toast = toast;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        StatusText = "Scanning Windows Apps...";
        try
        {
            _allApps = [.. await _scanner.ScanAsync()];
            ApplyFilter();
            StatusText = $"{_allApps.Count} Windows Apps found";
            _toast.ShowInfo($"{_allApps.Count} Windows Apps found", "Scan complete");
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; _toast.ShowError(ex.Message, "Scan failed"); }
        finally { IsLoading = false; }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allApps
            : _allApps.Where(a =>
                a.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                a.PackageFamilyName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        Apps = new ObservableCollection<WindowsApp>(filtered);
    }
}
