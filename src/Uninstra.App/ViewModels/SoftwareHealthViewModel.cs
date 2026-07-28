namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed partial class SoftwareHealthViewModel : ObservableObject
{
    private readonly IApplicationScanner _scanner;

    [ObservableProperty] private ObservableCollection<SoftwareHealthItem> _items = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private SoftwareHealthItem? _selectedHealthItem;
    [ObservableProperty] private HealthIssueDetail? _selectedDetail;
    [ObservableProperty] private bool _isDetailExpanded;

    public SoftwareHealthViewModel(IApplicationScanner scanner) => _scanner = scanner;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var apps = await _scanner.ScanAsync();
            Items.Clear();

            // Broken Uninstall Entries
            var brokenApps = apps.Where(a => string.IsNullOrWhiteSpace(a.UninstallCommand) && !a.IsStoreApplication).ToList();
            if (brokenApps.Count > 0)
            {
                var details = brokenApps.Select(a => new HealthIssueDetail
                {
                    Id = a.Id,
                    Name = a.DisplayName,
                    Publisher = a.Publisher,
                    InstallLocation = a.InstallLocation,
                    SizeBytes = a.EstimatedSizeBytes,
                    InstallDate = a.InstallDate,
                    RegistryKeyPath = a.RegistryKeyPath,
                    CanFix = !string.IsNullOrEmpty(a.RegistryKeyPath),
                    FixAction = "Delete Registry Entry"
                });

                Items.Add(new SoftwareHealthItem
                {
                    Id = "broken",
                    Title = "Broken Uninstall Entries",
                    Description = $"{brokenApps.Count} programs have missing or broken uninstallers",
                    Severity = "Attention recommended",
                    AffectedCount = brokenApps.Count,
                    Details = new ObservableCollection<HealthIssueDetail>(details)
                });
            }

            // Missing Uninstallers
            var missingApps = apps.Where(a => a.InstallerType == Core.Enums.InstallerType.MissingUninstaller).ToList();
            if (missingApps.Count > 0)
            {
                var details = missingApps.Select(a => new HealthIssueDetail
                {
                    Id = a.Id,
                    Name = a.DisplayName,
                    Publisher = a.Publisher,
                    InstallLocation = a.InstallLocation,
                    SizeBytes = a.EstimatedSizeBytes,
                    InstallDate = a.InstallDate,
                    RegistryKeyPath = a.RegistryKeyPath,
                    CanFix = !string.IsNullOrEmpty(a.InstallLocation) && Directory.Exists(a.InstallLocation),
                    FixAction = "Open Install Location"
                });

                Items.Add(new SoftwareHealthItem
                {
                    Id = "missing",
                    Title = "Missing Uninstallers",
                    Description = $"{missingApps.Count} programs have no uninstaller",
                    Severity = "Needs review",
                    AffectedCount = missingApps.Count,
                    Details = new ObservableCollection<HealthIssueDetail>(details)
                });
            }

            // Recently Installed
            var recentApps = apps.Where(a => a.InstallDate >= DateTime.Now.AddDays(-7))
                .OrderByDescending(a => a.InstallDate).ToList();
            if (recentApps.Count > 0)
            {
                var details = recentApps.Select(a => new HealthIssueDetail
                {
                    Id = a.Id,
                    Name = a.DisplayName,
                    Publisher = a.Publisher,
                    InstallLocation = a.InstallLocation,
                    SizeBytes = a.EstimatedSizeBytes,
                    InstallDate = a.InstallDate,
                    UninstallCommand = a.UninstallCommand,
                    CanFix = !string.IsNullOrEmpty(a.UninstallCommand),
                    FixAction = "Uninstall"
                });

                Items.Add(new SoftwareHealthItem
                {
                    Id = "recent",
                    Title = "Recently Installed",
                    Description = $"{recentApps.Count} programs installed in the last 7 days",
                    Severity = "No action required",
                    AffectedCount = recentApps.Count,
                    Details = new ObservableCollection<HealthIssueDetail>(details)
                });
            }

            // Large Programs
            var largeApps = apps.Where(a => a.EstimatedSizeBytes > 1073741824)
                .OrderByDescending(a => a.EstimatedSizeBytes).ToList();
            if (largeApps.Count > 0)
            {
                var details = largeApps.Select(a => new HealthIssueDetail
                {
                    Id = a.Id,
                    Name = a.DisplayName,
                    Publisher = a.Publisher,
                    InstallLocation = a.InstallLocation,
                    SizeBytes = a.EstimatedSizeBytes,
                    InstallDate = a.InstallDate,
                    UninstallCommand = a.UninstallCommand,
                    CanFix = !string.IsNullOrEmpty(a.UninstallCommand),
                    FixAction = "Uninstall"
                });

                Items.Add(new SoftwareHealthItem
                {
                    Id = "large",
                    Title = "Large Programs",
                    Description = $"{largeApps.Count} programs over 1 GB",
                    Severity = "No action required",
                    AffectedCount = largeApps.Count,
                    Details = new ObservableCollection<HealthIssueDetail>(details)
                });
            }

            if (Items.Count == 0)
                Items.Add(new SoftwareHealthItem
                {
                    Id = "ok",
                    Title = "All Good",
                    Description = "No issues detected",
                    Severity = "No action required"
                });
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleDetails(SoftwareHealthItem item)
    {
        if (SelectedHealthItem == item && IsDetailExpanded)
        {
            IsDetailExpanded = false;
            SelectedHealthItem = null;
        }
        else
        {
            SelectedHealthItem = item;
            IsDetailExpanded = true;
        }
    }

    [RelayCommand]
    private static void OpenInstallLocation(HealthIssueDetail detail)
    {
        if (!string.IsNullOrEmpty(detail.InstallLocation) && Directory.Exists(detail.InstallLocation))
            Process.Start("explorer.exe", detail.InstallLocation);
    }

    [RelayCommand]
    private static void OpenRegistryLocation(HealthIssueDetail detail)
    {
        if (!string.IsNullOrEmpty(detail.RegistryKeyPath))
            Process.Start("regedit.exe", $"/m \"{detail.RegistryKeyPath}\"");
    }
}
