namespace Uninstra.App.Views.Pages;

using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using Uninstra.App.ViewModels;

public partial class BrowserExtensionsPage : UserControl
{
    public BrowserExtensionsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<BrowserExtensionsViewModel>();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is BrowserExtensionsViewModel vm) await vm.LoadCommand.ExecuteAsync(null);
    }
}
