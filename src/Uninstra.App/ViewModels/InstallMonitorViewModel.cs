namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class InstallMonitorViewModel : ObservableObject
{
    [ObservableProperty] private bool _isMonitoring;
    [ObservableProperty] private string _statusText = "Ready to monitor installations";
    [ObservableProperty] private string _installerPath = "";

    [RelayCommand]
    private void BrowseInstaller()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Installers|*.exe;*.msi|All Files|*.*",
            Title = "Select an installer to monitor"
        };
        if (dialog.ShowDialog() == true)
            InstallerPath = dialog.FileName;
    }

    [RelayCommand]
    private async Task StartMonitoringAsync()
    {
        if (string.IsNullOrWhiteSpace(InstallerPath)) return;
        IsMonitoring = true;
        StatusText = $"Monitoring: {System.IO.Path.GetFileName(InstallerPath)}...";
        // In a full implementation, this would use the IInstallMonitorService
        await Task.Delay(1000); // Placeholder for actual monitoring
        StatusText = "Monitoring session placeholder — full implementation uses snapshot comparison";
        IsMonitoring = false;
    }
}
