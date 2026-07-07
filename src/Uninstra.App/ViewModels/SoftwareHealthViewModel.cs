namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed partial class SoftwareHealthViewModel : ObservableObject
{
    private readonly IApplicationScanner _scanner;

    [ObservableProperty] private ObservableCollection<SoftwareHealthItem> _items = [];
    [ObservableProperty] private bool _isLoading;

    public SoftwareHealthViewModel(IApplicationScanner scanner) => _scanner = scanner;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var apps = await _scanner.ScanAsync();
            Items.Clear();

            var broken = apps.Count(a => string.IsNullOrWhiteSpace(a.UninstallCommand));
            if (broken > 0)
                Items.Add(new SoftwareHealthItem { Id = "broken", Title = "Broken Uninstall Entries",
                    Description = $"{broken} programs have missing or broken uninstallers",
                    Severity = "Attention recommended", AffectedCount = broken });

            var missing = apps.Count(a => a.InstallerType == Core.Enums.InstallerType.MissingUninstaller);
            if (missing > 0)
                Items.Add(new SoftwareHealthItem { Id = "missing", Title = "Missing Uninstallers",
                    Description = $"{missing} programs have no uninstaller",
                    Severity = "Needs review", AffectedCount = missing });

            var recent = apps.Count(a => a.InstallDate >= DateTime.Now.AddDays(-7));
            if (recent > 0)
                Items.Add(new SoftwareHealthItem { Id = "recent", Title = "Recently Installed",
                    Description = $"{recent} programs installed in the last 7 days",
                    Severity = "No action required", AffectedCount = recent });

            var large = apps.Where(a => a.EstimatedSizeBytes > 1073741824).ToList();
            if (large.Count > 0)
                Items.Add(new SoftwareHealthItem { Id = "large", Title = "Large Programs",
                    Description = $"{large.Count} programs over 1 GB",
                    Severity = "No action required", AffectedCount = large.Count });

            if (Items.Count == 0)
                Items.Add(new SoftwareHealthItem { Id = "ok", Title = "All Good",
                    Description = "No issues detected", Severity = "No action required" });
        }
        finally { IsLoading = false; }
    }
}
