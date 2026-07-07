namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uninstra.App.Services;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly NavigationService _navigation;

    [ObservableProperty] private string _currentPage = "Programs";
    [ObservableProperty] private bool _isSidebarCollapsed;

    public MainViewModel(NavigationService navigation)
    {
        _navigation = navigation;
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        CurrentPage = page;
        _navigation.NavigateTo(page);
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;
}
