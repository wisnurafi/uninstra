namespace Uninstra.App.Services;

using CommunityToolkit.Mvvm.ComponentModel;

public sealed partial class NavigationService : ObservableObject
{
    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _currentPageName = "Programs";

    public event Action<string>? NavigationRequested;

    public void NavigateTo(string pageName)
    {
        CurrentPageName = pageName;
        NavigationRequested?.Invoke(pageName);
    }
}
