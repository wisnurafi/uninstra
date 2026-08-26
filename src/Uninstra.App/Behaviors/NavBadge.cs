namespace Uninstra.App.Behaviors;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

/// <summary>
/// Attached-property badge for sidebar navigation RadioButtons.
/// Mechanism only; no live data is wired here.
///
/// Usage (later, once counters exist):
/// <code>
/// xmlns:behaviors="clr-namespace:Uninstra.App.Behaviors"
/// &lt;RadioButton ... behaviors:NavBadge.Count="{Binding QuarantinePendingCount}"/&gt;
/// </code>
/// Renders a small rounded pill right-aligned inside the nav item
/// (PrimaryTranslucentBrush background, PrimaryBrush 10px text, right margin 8).
/// Collapsed automatically when Count &lt;= 0.
/// </summary>
public static class NavBadge
{
    public static readonly DependencyProperty CountProperty =
        DependencyProperty.RegisterAttached(
            "Count",
            typeof(int),
            typeof(NavBadge),
            new PropertyMetadata(0, OnCountChanged));

    /// <summary>Per-button storage for the pill so it survives count updates.</summary>
    private static readonly DependencyProperty PillProperty =
        DependencyProperty.RegisterAttached(
            "Pill",
            typeof(Border),
            typeof(NavBadge),
            new PropertyMetadata(null));

    public static void SetCount(DependencyObject element, int value) => element.SetValue(CountProperty, value);

    public static int GetCount(DependencyObject element) => (int)element.GetValue(CountProperty);

    private static void OnCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RadioButton button)
        {
            return;
        }

        // The template root only exists after the control is realized,
        // so defer until Loaded; afterwards update live.
        button.Loaded -= OnButtonLoaded;
        button.Loaded += OnButtonLoaded;

        if (button.IsLoaded && VisualTreeHelper.GetChildrenCount(button) > 0)
        {
            ApplyBadge(button);
        }
    }

    private static void OnButtonLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton button)
        {
            ApplyBadge(button);
        }
    }

    private static void ApplyBadge(RadioButton button)
    {
        var count = GetCount(button);

        var pill = (Border?)button.GetValue(PillProperty);
        if (pill is null)
        {
            pill = CreatePill();
            button.SetValue(PillProperty, pill);
        }

        // Detach from a previous host first (controls can be re-hosted).
        if (pill.Parent is Panel previousHost)
        {
            previousHost.Children.Remove(pill);
        }

        ((TextBlock)pill.Child).Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (count <= 0)
        {
            pill.Visibility = Visibility.Collapsed;
            return;
        }

        pill.Visibility = Visibility.Visible;

        // SidebarButton template root is a Grid; the pill docks right on top of it.
        if (VisualTreeHelper.GetChild(button, 0) is Panel templateRoot)
        {
            templateRoot.Children.Add(pill);
        }
    }

    private static Border CreatePill()
    {
        var label = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryBrush");

        var pill = new Border
        {
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(7, 1, 7, 1),
            MinWidth = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            IsHitTestVisible = false,
            Child = label,
        };
        pill.SetResourceReference(Border.BackgroundProperty, "PrimaryTranslucentBrush");

        return pill;
    }
}
