namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.App.Services;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;
using Uninstra.Core.Validation;

public sealed partial class ForceUninstallViewModel : ObservableObject
{
    private readonly ILeftoverScanner _leftoverScanner;
    private readonly IToastService _toast;
    private readonly IApplicationScanner _scanner;

    [ObservableProperty] private string _targetPath = "";
    [ObservableProperty] private string _statusText = "Drop an executable or folder, or browse to select";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private ObservableCollection<LeftoverCandidate> _leftovers = [];

    /// <summary>
    /// Non-empty when the target name is generic — consumed by the red warning banner
    /// on ForceUninstallPage. Empty string hides the banner.
    /// </summary>
    [ObservableProperty] private string _genericNameWarning = "";

    public ForceUninstallViewModel(ILeftoverScanner leftoverScanner, IApplicationScanner scanner, IToastService toast)
    {
        _leftoverScanner = leftoverScanner;
        _scanner = scanner;
        _toast = toast;
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

            var rawName = Path.GetFileNameWithoutExtension(TargetPath);
            var normalizedName = NameNormalizer.Normalize(rawName);

            // P0 safety: generic targets match other programs' folders/shortcuts/startup entries.
            GenericNameWarning = NameNormalizer.IsGenericName(normalizedName)
                ? $"'{rawName}' is a generic name. Results may include files belonging to OTHER programs — " +
                  "review every row's evidence before cleaning anything."
                : "";

            var fakeApp = new InstalledApplication
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                DisplayName = rawName,
                NormalizedName = normalizedName,
                InstallLocation = installDir
            };

            var results = await _leftoverScanner.ScanAsync(fakeApp);

            // NEVER auto-select in force-uninstall mode — the identity of the target
            // is inferred from a path, not verified against an uninstall entry.
            Leftovers = new ObservableCollection<LeftoverCandidate>(
                results.Select(r => r with { IsSelectedByDefault = false }));

            _toast.ShowInfo($"Found {results.Count} related items for force uninstall", "Scan complete");
            StatusText = $"Found {results.Count} related items" +
                (GenericNameWarning.Length > 0 ? " — review carefully" : "");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _toast.ShowError(ex.Message, "Force-uninstall scan failed");
        }
        finally { IsScanning = false; }
    }
}
