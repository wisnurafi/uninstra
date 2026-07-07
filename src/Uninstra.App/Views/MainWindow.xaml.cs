namespace Uninstra.App.Views;

using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using Uninstra.App.ViewModels;
using Uninstra.App.Views.Pages;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainViewModel>();

        // Load Programs page by default
        NavigateToPage("Programs");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            NavigateToPage(tag);
        }
    }

    private void NavigateToPage(string page)
    {
        PageContent.Content = page switch
        {
            "Programs" or "Programs_Recent" or "Programs_Large" or "Programs_Infrequent"
                or "Programs_Updates" or "Programs_System" => CreateProgramsPage(page),
            "SoftwareHealth" => new SoftwareHealthPage(),
            "InstallMonitor" => new InstallMonitorPage(),
            "ForceUninstall" => new ForceUninstallPage(),
            "ResidualScan" => new ResidualScanPage(),
            "WindowsApps" => new WindowsAppsPage(),
            "BrowserExtensions" => new BrowserExtensionsPage(),
            "JunkCleaner" => new JunkCleanerPage(),
            "Quarantine" => new QuarantinePage(),
            "History" => new HistoryPage(),
            "Settings" => new SettingsPage(),
            "About" => new AboutPage(),
            _ => new ProgramsPage()
        };
    }

    private static ProgramsPage CreateProgramsPage(string category)
    {
        var page = new ProgramsPage();
        if (page.DataContext is ProgramsViewModel vm)
        {
            vm.SelectedCategory = category switch
            {
                "Programs_Recent" => "Recently Installed",
                "Programs_Large" => "Large Programs",
                "Programs_Infrequent" => "Infrequently Used",
                "Programs_Updates" => "Windows Updates",
                "Programs_System" => "System Components",
                _ => "All Programs"
            };
        }
        return page;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Ensure window is within screen bounds
        var screen = SystemParameters.WorkArea;
        if (Left + Width > screen.Right) Left = screen.Right - Width;
        if (Top + Height > screen.Bottom) Top = screen.Bottom - Height;
        if (Left < screen.Left) Left = screen.Left;
        if (Top < screen.Top) Top = screen.Top;
    }
}
