namespace Uninstra.App.Views.Pages;

using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using Uninstra.App.ViewModels;

public partial class WindowsAppsPage : UserControl
{
    public WindowsAppsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<WindowsAppsViewModel>();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WindowsAppsViewModel vm) await vm.LoadCommand.ExecuteAsync(null);
    }
}
