// ═══════════════════════════════════════════════════════════════════════════
//  EMPTY STATE CONTROL — USAGE (Pages / Views)
// ═══════════════════════════════════════════════════════════════════════════
//
//  xmlns:controls="clr-namespace:Uninstra.App.Controls"
//
//  <controls:EmptyStateControl
//      Title="Nothing quarantined"
//      Message="Items removed during cleanup are held here first. Run a scan to review what can be safely deleted."
//      IconGeometry="{StaticResource Icon_Inbox}">
//      <controls:EmptyStateControl.ActionContent>
//          <Button Style="{StaticResource PrimaryButton}" Content="Run a scan"/>
//      </controls:EmptyStateControl.ActionContent>
//  </controls:EmptyStateControl>
//
//  All four properties are optional; hidden segments collapse so spacing stays
//  balanced whatever the combination. IconGeometry accepts any Geometry resource
//  from Themes/Icons.xaml (Icon_Inbox, Icon_Search, Icon_Trash, ...).
// ═══════════════════════════════════════════════════════════════════════════

namespace Uninstra.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

/// <summary>
/// Reusable empty-state placeholder: large muted vector icon (48px, 40% opacity),
/// semibold title, muted wrapped message, and an optional centered action slot.
/// </summary>
public sealed partial class EmptyStateControl : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(EmptyStateControl),
        new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(EmptyStateControl),
        new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty IconGeometryProperty = DependencyProperty.Register(
        nameof(IconGeometry), typeof(Geometry), typeof(EmptyStateControl),
        new PropertyMetadata(null, OnVisualPropertyChanged));

    public static readonly DependencyProperty ActionContentProperty = DependencyProperty.Register(
        nameof(ActionContent), typeof(object), typeof(EmptyStateControl),
        new PropertyMetadata(null, OnVisualPropertyChanged));

    public EmptyStateControl()
    {
        InitializeComponent();
        UpdateVisualState();
    }

    /// <summary>Short heading, e.g. "No programs found".</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Supporting copy shown under the title (wraps at 360px).</summary>
    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Vector icon geometry (typically an Icon_* resource from Themes/Icons.xaml).</summary>
    public Geometry? IconGeometry
    {
        get => (Geometry?)GetValue(IconGeometryProperty);
        set => SetValue(IconGeometryProperty, value);
    }

    /// <summary>Optional content slot rendered centered below the text (button, hyperlink, ...).</summary>
    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EmptyStateControl)d).UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        var hasIcon = IconGeometry is not null;
        var hasTitle = !string.IsNullOrEmpty(Title);
        var hasMessage = !string.IsNullOrEmpty(Message);
        var hasAction = ActionContent is not null;
        var hasAnythingAboveAction = hasIcon || hasTitle || hasMessage;

        PART_Icon.Data = IconGeometry;
        PART_Icon.Visibility = ToVisibility(hasIcon);
        PART_Icon.Margin = new Thickness(0, 0, 0, hasAnythingAboveAction && hasIcon ? 20 : 0);

        PART_Title.Text = Title ?? string.Empty;
        PART_Title.Visibility = ToVisibility(hasTitle);
        PART_Title.Margin = new Thickness(0, 0, 0, hasMessage || hasAction ? 6 : 0);

        PART_Message.Text = Message ?? string.Empty;
        PART_Message.Visibility = ToVisibility(hasMessage);
        PART_Message.Margin = new Thickness(0, 0, 0, 0);

        PART_Action.Content = ActionContent;
        PART_Action.Visibility = ToVisibility(hasAction);
        PART_Action.Margin = new Thickness(0, hasAnythingAboveAction ? 18 : 0, 0, 0);
    }

    private static Visibility ToVisibility(bool value)
    {
        return value ? Visibility.Visible : Visibility.Collapsed;
    }
}
