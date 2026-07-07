namespace Uninstra.App.Views.Pages;

using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using Uninstra.App.ViewModels;

public partial class SoftwareHealthPage : UserControl
{
    public SoftwareHealthPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<SoftwareHealthViewModel>();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SoftwareHealthViewModel vm) await vm.LoadCommand.ExecuteAsync(null);
    }
}
