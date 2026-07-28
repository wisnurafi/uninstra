namespace Uninstra.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using Uninstra.Application.Interfaces;
using Uninstra.Core.Models;

public sealed partial class InstallMonitorViewModel : ObservableObject
{
    private readonly IInstallMonitorService _monitorService;
    private readonly ILogger<InstallMonitorViewModel> _logger;
    private CancellationTokenSource? _monitoringCts;

    [ObservableProperty] private bool _isMonitoring;
    [ObservableProperty] private string _statusText = "Ready to monitor installations";
    [ObservableProperty] private string _installerPath = "";
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private bool _hasProgress;
    [ObservableProperty] private InstallMonitorSession? _lastSession;
    
    public ObservableCollection<InstallMonitorSession> Sessions { get; } = [];
    public ObservableCollection<ChangeSummary> Changes { get; } = [];

    public InstallMonitorViewModel(
        IInstallMonitorService monitorService,
        ILogger<InstallMonitorViewModel> logger)
    {
        _monitorService = monitorService;
        _logger = logger;
        
        _ = LoadSessionsAsync();
    }

    [RelayCommand]
    private void BrowseInstaller()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Installers|*.exe;*.msi|Executables|*.exe|MSI Packages|*.msi|All Files|*.*",
            Title = "Select an installer to monitor"
        };
        
        if (dialog.ShowDialog() == true)
        {
            InstallerPath = dialog.FileName;
            StatusText = $"Selected: {System.IO.Path.GetFileName(InstallerPath)}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartMonitoring))]
    private async Task StartMonitoringAsync()
    {
        if (string.IsNullOrWhiteSpace(InstallerPath))
        {
            StatusText = "Please select an installer first";
            return;
        }

        if (!System.IO.File.Exists(InstallerPath))
        {
            StatusText = "Installer file not found";
            return;
        }

        IsMonitoring = true;
        HasProgress = true;
        StatusText = "Starting monitoring session...";
        Changes.Clear();

        _monitoringCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<string>(text =>
            {
                ProgressText = text;
                StatusText = text;
            });

            var result = await _monitorService.StartMonitoringAsync(
                InstallerPath,
                progress,
                _monitoringCts.Token);

            if (result.IsSuccess && result.Value is not null)
            {
                LastSession = result.Value;
                Sessions.Insert(0, result.Value);
                
                // Populate changes summary
                PopulateChangesSummary(result.Value);
                
                StatusText = $"Monitoring complete. Found {result.Value.CreatedDirectories.Count} new directories, {result.Value.RegistryChanges.Count} registry changes.";
            }
            else
            {
                StatusText = result.Error?.Message ?? "Monitoring failed";
                _logger.LogWarning("Install monitoring failed: {Error}", result.Error?.Message);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Monitoring cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            _logger.LogError(ex, "Install monitoring error");
        }
        finally
        {
            IsMonitoring = false;
            HasProgress = false;
            _monitoringCts?.Dispose();
            _monitoringCts = null;
        }
    }

    private bool CanStartMonitoring => !IsMonitoring && !string.IsNullOrWhiteSpace(InstallerPath);

    [RelayCommand(CanExecute = nameof(IsMonitoring))]
    private void CancelMonitoring()
    {
        _monitoringCts?.Cancel();
        StatusText = "Cancelling monitoring...";
    }

    private void PopulateChangesSummary(InstallMonitorSession session)
    {
        Changes.Clear();

        if (session.CreatedDirectories.Count > 0)
        {
            Changes.Add(new ChangeSummary(
                "Created Directories",
                session.CreatedDirectories.Count,
                session.CreatedDirectories.Take(5).ToList()));
        }

        if (session.RegistryChanges.Count > 0)
        {
            Changes.Add(new ChangeSummary(
                "Registry Changes",
                session.RegistryChanges.Count,
                session.RegistryChanges.Take(5).ToList()));
        }

        if (session.NewServices.Count > 0)
        {
            Changes.Add(new ChangeSummary(
                "New Services",
                session.NewServices.Count,
                session.NewServices));
        }

        if (session.NewStartupEntries.Count > 0)
        {
            Changes.Add(new ChangeSummary(
                "Startup Entries",
                session.NewStartupEntries.Count,
                session.NewStartupEntries));
        }

        if (session.DetectedApplications.Count > 0)
        {
            Changes.Add(new ChangeSummary(
                "Detected Applications",
                session.DetectedApplications.Count,
                session.DetectedApplications));
        }

        if (session.NewScheduledTasks.Count > 0)
        {
            Changes.Add(new ChangeSummary(
                "Scheduled Tasks",
                session.NewScheduledTasks.Count,
                session.NewScheduledTasks));
        }

        if (session.Warnings.Count > 0)
        {
            Changes.Add(new ChangeSummary(
                "Warnings",
                session.Warnings.Count,
                session.Warnings));
        }
    }

    private async Task LoadSessionsAsync()
    {
        try
        {
            var sessions = await _monitorService.GetSessionsAsync();
            
            Sessions.Clear();
            foreach (var session in sessions.OrderByDescending(s => s.StartedAt))
            {
                Sessions.Add(session);
            }

            if (Sessions.Count > 0)
            {
                StatusText = $"Loaded {Sessions.Count} monitoring session(s)";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load monitoring sessions");
        }
    }

    partial void OnInstallerPathChanged(string value)
    {
        StartMonitoringCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsMonitoringChanged(bool value)
    {
        CancelMonitoringCommand.NotifyCanExecuteChanged();
        StartMonitoringCommand.NotifyCanExecuteChanged();
    }
}

public record ChangeSummary(
    string Category,
    int Count,
    IReadOnlyList<string> Samples);
