namespace Uninstra.App.Views.Pages;

using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using Uninstra.App.ViewModels;

public partial class InstallMonitorPage : UserControl
{
    public InstallMonitorPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<InstallMonitorViewModel>();
    }
}
