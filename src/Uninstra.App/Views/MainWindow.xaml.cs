namespace Uninstra.App.Views;

using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Uninstra.App.ViewModels;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _vm;
        
        // Load default page
        NavigateTo("Programs");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton btn && btn.Tag is string pageName)
        {
            NavigateTo(pageName);
        }
    }

    private void NavigateTo(string pageName)
    {
        object view = pageName switch
        {
            "Programs" => new Pages.ProgramsPage(),
            "Programs_Recent" => new Pages.ProgramsPage { DataContext = SetCategory("Recently Installed") },
            "Programs_Large" => new Pages.ProgramsPage { DataContext = SetCategory("Large Programs") },
            "SoftwareHealth" => new Pages.SoftwareHealthPage(),
            "InstallMonitor" => new Pages.InstallMonitorPage(),
            "ForceUninstall" => new Pages.ForceUninstallPage(),
            "ResidualScan" => new Pages.ResidualScanPage(),
            "WindowsApps" => new Pages.WindowsAppsPage(),
            "BrowserExtensions" => new Pages.BrowserExtensionsPage(),
            "JunkCleaner" => new Pages.JunkCleanerPage(),
            "Quarantine" => new Pages.QuarantinePage(),
            "History" => new Pages.HistoryPage(),
            "Settings" => new Pages.SettingsPage(),
            "About" => new Pages.AboutPage(),
            _ => new Pages.ProgramsPage()
        };
        PageContent.Content = view;
    }

    private object SetCategory(string category)
    {
        var vm = App.Services.GetRequiredService<ProgramsViewModel>();
        vm.SelectedCategory = category;
        return vm;
    }

    // Window Chrome Handlers
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (MaximizeIcon != null)
        {
            var res = WindowState == WindowState.Maximized ? "Icon_Restore" : "Icon_Maximize";
            MaximizeIcon.Data = (Geometry)FindResource(res);
        }
    }
}