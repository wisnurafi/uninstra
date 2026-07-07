namespace Uninstra.App.Views.Pages;

using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using Uninstra.App.ViewModels;

public partial class ForceUninstallPage : UserControl
{
    public ForceUninstallPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ForceUninstallViewModel>();
    }
}
