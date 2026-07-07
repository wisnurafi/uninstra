namespace Uninstra.App.Views;

using System.Windows;
using Uninstra.App.ViewModels;
using Uninstra.Core.Models;

public partial class DeepUninstallDialog : Window
{
    private readonly DeepUninstallViewModel _vm;

    public bool CleanupPerformed => _vm.DialogResult;
    public int CleanedCount => _vm.CleanedCount;
    public long FreedBytes => _vm.FreedBytes;

    public DeepUninstallDialog(DeepUninstallViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // Subscribe to phase changes
        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_vm.Phase))
            {
                Dispatcher.Invoke(() => UpdatePhaseUI());
            }
        };
    }

    public void ShowResults(string appName, IReadOnlyList<LeftoverCandidate> candidates)
    {
        _vm.SetScanResults(appName, candidates);
    }

    private void UpdatePhaseUI()
    {
        if (_vm.Phase == "done")
        {
            // Show completion screen
            ReviewPanel.Visibility = Visibility.Collapsed;
            CompletionPanel.Visibility = Visibility.Visible;

            // Populate stats
            StatCleaned.Text = _vm.CleanedCount.ToString();
            StatFailed.Text = _vm.FailedCount.ToString();
            StatFreed.Text = FormatSize(_vm.FreedBytes);

            CompletionSubtitle.Text = _vm.CleanedCount > 0
                ? $"{_vm.AppName} has been completely removed from your system."
                : _vm.FailedCount > 0
                    ? "Some items could not be removed. Check logs for details."
                    : "No items were cleaned.";
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
