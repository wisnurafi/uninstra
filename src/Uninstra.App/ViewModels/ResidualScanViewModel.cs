namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using Uninstra.Core.Enums;
using Uninstra.Core.Models;

public sealed partial class ResidualScanViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<LeftoverCandidate> _residuals = [];
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusText = "Ready to scan for residual files";

    [RelayCommand]
    private async Task ScanAsync()
    {
        IsScanning = true;
        StatusText = "Scanning for residual files...";
        Residuals.Clear();

        await Task.Run(() =>
        {
            // Scan for broken uninstall entries
            var paths = new[]
            {
                (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                (RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
            };

            foreach (var (hive, view, path) in paths)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var key = baseKey.OpenSubKey(path);
                    if (key is null) continue;

                    foreach (var subName in key.GetSubKeyNames())
                    {
                        using var sub = key.OpenSubKey(subName);
                        if (sub is null) continue;

                        var name = sub.GetValue("DisplayName") as string;
                        var uninstall = sub.GetValue("UninstallString") as string;
                        var installLoc = sub.GetValue("InstallLocation") as string;

                        if (string.IsNullOrEmpty(name)) continue;

                        // Check for broken uninstaller
                        if (!string.IsNullOrEmpty(uninstall))
                        {
                            var exe = uninstall.Trim('"').Split(' ')[0].Trim('"');
                            if (!string.IsNullOrEmpty(exe) && !File.Exists(exe) && !exe.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Residuals.Add(new LeftoverCandidate
                                    {
                                        Id = Guid.NewGuid().ToString("N")[..16],
                                        ApplicationId = subName,
                                        DisplayName = $"Broken entry: {name}",
                                        Type = LeftoverType.RegistryKey,
                                        RegistryHive = hive == RegistryHive.LocalMachine ? RegistryHiveType.LocalMachine : RegistryHiveType.CurrentUser,
                                        RegistryPath = $@"{path}\{subName}",
                                        ConfidenceScore = 90,
                                        ConfidenceLevel = ConfidenceLevel.High,
                                        RiskLevel = RiskLevel.Low,
                                        Evidence = [$"Uninstaller not found: {exe}"],
                                        IsSelectedByDefault = false,
                                        SourceScanner = "ResidualScan"
                                    });
                                });
                            }
                        }

                        // Check for missing install location
                        if (!string.IsNullOrEmpty(installLoc) && !Directory.Exists(installLoc))
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                Residuals.Add(new LeftoverCandidate
                                {
                                    Id = Guid.NewGuid().ToString("N")[..16],
                                    ApplicationId = subName,
                                    DisplayName = $"Missing location: {name}",
                                    Type = LeftoverType.RegistryKey,
                                    RegistryHive = hive == RegistryHive.LocalMachine ? RegistryHiveType.LocalMachine : RegistryHiveType.CurrentUser,
                                    RegistryPath = $@"{path}\{subName}",
                                    ConfidenceScore = 85,
                                    ConfidenceLevel = ConfidenceLevel.High,
                                    RiskLevel = RiskLevel.Low,
                                    Evidence = [$"Install location missing: {installLoc}"],
                                    IsSelectedByDefault = false,
                                    SourceScanner = "ResidualScan"
                                });
                            });
                        }
                    }
                }
                catch { }
            }
        });

        StatusText = $"Found {Residuals.Count} residual items";
        IsScanning = false;
    }
}
