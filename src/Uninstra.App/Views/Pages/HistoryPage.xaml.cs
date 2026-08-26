namespace Uninstra.App.Views.Pages;

using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using Uninstra.App.ViewModels;

public partial class HistoryPage : UserControl
{
    public HistoryPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<HistoryViewModel>();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is HistoryViewModel vm) await vm.LoadCommand.ExecuteAsync(null);
    }

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HistoryViewModel vm) return;

        var result = MessageBox.Show(
            "Clear all history?", "Uninstra",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        await vm.ClearAllCommand.ExecuteAsync(null);
    }
}
