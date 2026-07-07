namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed partial class ForceUninstallViewModel : ObservableObject
{
    private readonly ILeftoverScanner _leftoverScanner;
    private readonly IApplicationScanner _scanner;

    [ObservableProperty] private string _targetPath = "";
    [ObservableProperty] private string _statusText = "Drop an executable or folder, or browse to select";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private ObservableCollection<LeftoverCandidate> _leftovers = [];

    public ForceUninstallViewModel(ILeftoverScanner leftoverScanner, IApplicationScanner scanner)
    {
        _leftoverScanner = leftoverScanner;
        _scanner = scanner;
    }

    [RelayCommand]
    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Executables|*.exe|All Files|*.*",
            Title = "Select a program to force uninstall"
        };
        if (dialog.ShowDialog() == true)
            TargetPath = dialog.FileName;
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetPath)) return;
        IsScanning = true;
        StatusText = "Scanning for related files...";

        try
        {
            var installDir = File.Exists(TargetPath)
                ? Path.GetDirectoryName(TargetPath) ?? TargetPath
                : TargetPath;

            var fakeApp = new InstalledApplication
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                DisplayName = Path.GetFileNameWithoutExtension(TargetPath),
                NormalizedName = Path.GetFileNameWithoutExtension(TargetPath).ToLowerInvariant(),
                InstallLocation = installDir
            };

            var results = await _leftoverScanner.ScanAsync(fakeApp);
            Leftovers = new ObservableCollection<LeftoverCandidate>(results);
            StatusText = $"Found {results.Count} related items";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally { IsScanning = false; }
    }
}
