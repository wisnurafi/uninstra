namespace Uninstra.App.Views;

using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Uninstra.App.ViewModels;
using Uninstra.Core.Models;

public partial class DeepUninstallDialog : Window
{
    private readonly DeepUninstallViewModel _vm;
    private ICollectionView? _leftoverView;
    private System.Collections.ObjectModel.ObservableCollection<LeftoverItemViewModel>? _trackedLeftovers;
    private string _activeGroup = "All";

    public bool CleanupPerformed => _vm.DialogResult;
    public int CleanedCount => _vm.CleanedCount;
    public long FreedBytes => _vm.FreedBytes;

    public DeepUninstallDialog(DeepUninstallViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // Group-by-type chips filter the SHARED default view of Leftovers —
        // the collection itself is never mutated, so selection state stays intact.
        AttachLeftoverView(_vm.Leftovers);

        // Subscribe to phase changes
        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_vm.Phase))
            {
                Dispatcher.Invoke(() => UpdatePhaseUI());
            }
        };

        RefreshChipCounts();
    }

    private void AttachLeftoverView(System.Collections.ObjectModel.ObservableCollection<LeftoverItemViewModel> leftovers)
    {
        if (_trackedLeftovers is not null)
        {
            _trackedLeftovers.CollectionChanged -= OnLeftoversCollectionChanged;
        }

        _trackedLeftovers = leftovers;
        _leftoverView = CollectionViewSource.GetDefaultView(leftovers);
        _leftoverView.Filter = o => o is LeftoverItemViewModel item && MatchesGroup(item.TypeName);

        leftovers.CollectionChanged += OnLeftoversCollectionChanged;
    }

    private void OnLeftoversCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RefreshChipCounts();

    private bool MatchesGroup(string typeName) => _activeGroup switch
    {
        "Files" => typeName is "File" or "Directory" or "EmptyDirectory",
        "Registry" => typeName is "RegistryKey" or "RegistryValue",
        "Shortcuts" => typeName == "Shortcut",
        "Startup" => typeName == "StartupEntry",
        "Other" => typeName is not (
            "File" or "Directory" or "EmptyDirectory" or
            "RegistryKey" or "RegistryValue" or "Shortcut" or "StartupEntry"),
        _ => true,
    };

    private void FilterChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton chip && chip.Tag is string group)
        {
            _activeGroup = group;
            _leftoverView?.Refresh();
        }
    }

    private void RefreshChipCounts()
    {
        int all = 0, files = 0, registry = 0, shortcuts = 0, startup = 0;

        foreach (var leftover in _vm.Leftovers)
        {
            all++;
            switch (leftover.TypeName)
            {
                case "File":
                case "Directory":
                case "EmptyDirectory":
                    files++;
                    break;
                case "RegistryKey":
                case "RegistryValue":
                    registry++;
                    break;
                case "Shortcut":
                    shortcuts++;
                    break;
                case "StartupEntry":
                    startup++;
                    break;
            }
        }

        var other = all - files - registry - shortcuts - startup;

        if (CheckAccess())
        {
            SetChipCounts(all, files, registry, shortcuts, startup, other);
        }
        else
        {
            Dispatcher.Invoke(() => SetChipCounts(all, files, registry, shortcuts, startup, other));
        }
    }

    private void SetChipCounts(int all, int files, int registry, int shortcuts, int startup, int other)
    {
        ChipCountAll.Text = all.ToString();
        ChipCountFiles.Text = files.ToString();
        ChipCountRegistry.Text = registry.ToString();
        ChipCountShortcuts.Text = shortcuts.ToString();
        ChipCountStartup.Text = startup.ToString();
        ChipCountOther.Text = other.ToString();
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

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }
}

/// <summary>
/// Maps a confidence string like "85% (High)" onto the matching theme brush:
/// High → SuccessBrush, Medium → WarningBrush, Low → ErrorBrush.
/// Resolves brushes through Application resources so theming stays intact.
/// </summary>
public sealed class ConfidenceToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;
        var key = text.Contains("(High)", StringComparison.OrdinalIgnoreCase) ? "SuccessBrush"
            : text.Contains("(Medium)", StringComparison.OrdinalIgnoreCase) ? "WarningBrush"
            : text.Contains("(Low)", StringComparison.OrdinalIgnoreCase) ? "ErrorBrush"
            : "TextMutedBrush";

        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
