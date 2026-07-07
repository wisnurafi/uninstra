namespace Uninstra.App.Views.Pages;

using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using Uninstra.App.ViewModels;

public partial class QuarantinePage : UserControl
{
    public QuarantinePage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<QuarantineViewModel>();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is QuarantineViewModel vm) await vm.LoadCommand.ExecuteAsync(null);
    }
}
