namespace Uninstra.App.Views.Pages;

using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using Uninstra.App.ViewModels;

public partial class ResidualScanPage : UserControl
{
    public ResidualScanPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ResidualScanViewModel>();
    }
}
