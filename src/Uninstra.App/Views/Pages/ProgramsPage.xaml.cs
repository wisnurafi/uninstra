namespace Uninstra.App.Views.Pages;

using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using Uninstra.App.ViewModels;

public partial class ProgramsPage : UserControl
{
    public ProgramsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ProgramsViewModel>();
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ProgramsViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}
