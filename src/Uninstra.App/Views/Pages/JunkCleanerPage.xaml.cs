namespace Uninstra.App.Views.Pages;

using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using Uninstra.App.ViewModels;

public partial class JunkCleanerPage : UserControl
{
    public JunkCleanerPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<JunkCleanerViewModel>();
    }
}
