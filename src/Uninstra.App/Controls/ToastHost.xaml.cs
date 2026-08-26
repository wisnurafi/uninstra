// ═══════════════════════════════════════════════════════════════════════════
//  TOAST HOST — INTEGRATION GUIDE (lead engineer)
// ═══════════════════════════════════════════════════════════════════════════
//
//  1. DEPENDENCY REGISTRATION — App.xaml.cs, inside ConfigureServices():
//
//         // UI services
//         services.AddSingleton<IToastService, ToastService>();
//
//  2. HOSTING — MainWindow.xaml: add the namespace, then place ONE ToastHost as
//     the LAST child of the root <Grid>, spanning all columns so it overlays
//     sidebar + content (adjust ColumnSpan to your column count):
//
//         xmlns:controls="clr-namespace:Uninstra.App.Controls"
//         ...
//         <!-- everything else -->
//         <controls:ToastHost Grid.Column="0" Grid.ColumnSpan="2"
//                             Panel.ZIndex="1000"
//                             HorizontalAlignment="Stretch"
//                             VerticalAlignment="Stretch"/>
//
//     The host paints only the toast cards; empty space is hit-test transparent,
//     so it never intercepts clicks meant for the window content beneath it.
//     Cards stack top-right and clear the custom 32px caption strip.
//
//  3. RAISING TOASTS — from anywhere that can reach the container:
//
//         App.Services.GetRequiredService<IToastService>().ShowSuccess("Scan complete");
//         App.Services.GetRequiredService<IToastService>().ShowError("Access denied", "Uninstall failed");
//
//     ViewModels should take IToastService as a CONSTRUCTOR DEPENDENCY instead of
//     resolving statically. ShowX(...) is thread-safe: background threads are
//     marshaled onto the WPF dispatcher by ToastService before the event fires.
//
//  BEHAVIOUR: max 4 visible cards (oldest is dropped to make room), success /
//  warning / info auto-dismiss after 4s, errors after 7s, click anywhere on a
//  card or its close button dismisses it immediately (fade-out 150ms).
// ═══════════════════════════════════════════════════════════════════════════

namespace Uninstra.App.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Uninstra.App.Services;

/// <summary>
/// Overlay panel that renders toast notifications raised through
/// <see cref="IToastService"/>. Designed to sit top-right inside a host Grid.
/// </summary>
public sealed partial class ToastHost : UserControl
{
    private readonly List<ToastCard> _activeCards = new();
    private IToastService? _toastService;
    private bool _subscribed;

    public ToastHost()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_toastService is null && Application.Current is App)
        {
            _toastService = App.Services.GetService<IToastService>();
        }

        if (_toastService is not null && !_subscribed)
        {
            _toastService.ToastRaised += OnToastRaised;
            _subscribed = true;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_toastService is not null && _subscribed)
        {
            _toastService.ToastRaised -= OnToastRaised;
            _subscribed = false;
        }

        // Stop timers / drop visuals when the host leaves the tree.
        foreach (var card in _activeCards.ToArray())
        {
            card.Dismiss(immediate: true);
        }
    }

    private void OnToastRaised(ToastNotification notification)
    {
        // ToastService guarantees UI-thread delivery.
        while (_activeCards.Count >= IToastService.MaxVisibleToasts)
        {
            _activeCards[0].Dismiss(immediate: true);
        }

        var card = new ToastCard(this, notification);
        _activeCards.Add(card);
        ToastStack.Children.Add(card.Root);
        card.PlayEntrance();
    }

    private void RemoveCard(ToastCard card)
    {
        _activeCards.Remove(card);
        ToastStack.Children.Remove(card.Root);
    }

    /// <summary>Self-contained toast card: visuals, auto-dismiss timer, animations.</summary>
    private sealed class ToastCard
    {
        private const double EntranceMillis = 200;
        private const double ExitMillis = 150;

        private readonly ToastHost _owner;
        private readonly Border _root;
        private readonly DispatcherTimer _autoDismissTimer;
        private readonly TranslateTransform _slide;
        private bool _dismissStarted;

        public ToastCard(ToastHost owner, ToastNotification notification)
        {
            _owner = owner;
            _slide = new TranslateTransform(36, 0);

            _root = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                MinWidth = 300,
                MaxWidth = 400,
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true,
                RenderTransform = _slide,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 22,
                    ShadowDepth = 0,
                    Opacity = 0.45
                }
            };
            // Near-opaque carbon glass card.
            _root.SetResourceReference(Border.BackgroundProperty, "CardBrush");
            _root.SetResourceReference(Border.BorderBrushProperty, "GlassBorderBrush");

            var layout = new Grid();

            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });                              // accent bar
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                                // severity icon
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });           // title + message
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });                                // close button

            // 3px left accent bar tinted by severity.
            var accentBar = new Border { CornerRadius = new CornerRadius(9, 0, 0, 9) };
            accentBar.SetResourceReference(Border.BackgroundProperty, AccentBrushKey(notification.Severity));
            Grid.SetColumn(accentBar, 0);
            layout.Children.Add(accentBar);

            var icon = new Path
            {
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0),
                Data = owner.TryFindResource(IconResourceKey(notification.Severity)) as Geometry
            };
            icon.SetResourceReference(Shape.FillProperty, AccentBrushKey(notification.Severity));
            Grid.SetColumn(icon, 1);
            layout.Children.Add(icon);

            var text = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 11, 4, 11)
            };

            if (notification.Title is not null)
            {
                var title = new TextBlock
                {
                    Text = notification.Title,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                };
                title.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                text.Children.Add(title);
            }

            var message = new TextBlock
            {
                Text = notification.Message,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, notification.Title is null ? 0 : 2, 0, 0)
            };
            message.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            text.Children.Add(message);

            Grid.SetColumn(text, 2);
            layout.Children.Add(text);

            var closeGlyph = new Path
            {
                Width = 10,
                Height = 10,
                Stretch = Stretch.Uniform,
                Data = owner.TryFindResource("Icon_Close") as Geometry
            };
            closeGlyph.SetResourceReference(Shape.FillProperty, "TextMutedBrush");

            var closeButton = new Button
            {
                Content = closeGlyph,
                Width = 26,
                Height = 26,
                Margin = new Thickness(6, 8, 8, 0),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Focusable = false,
                Template = CreateGhostButtonTemplate()
            };
            closeButton.MouseEnter += (_, _) => closeGlyph.SetResourceReference(Shape.FillProperty, "TextPrimaryBrush");
            closeButton.MouseLeave += (_, _) => closeGlyph.SetResourceReference(Shape.FillProperty, "TextMutedBrush");
            closeButton.Click += (_, _) => Dismiss();
            Grid.SetColumn(closeButton, 3);
            layout.Children.Add(closeButton);

            _root.Child = layout;

            // Click-to-dismiss anywhere on the card (the close Button swallows its own clicks).
            _root.MouseLeftButtonUp += (_, _) => Dismiss();

            _autoDismissTimer = new DispatcherTimer { Interval = notification.AutoDismissAfter };
            _autoDismissTimer.Tick += (_, _) => Dismiss();
        }

        public Border Root => _root;

        /// <summary>Slide-in-from-right plus fade, 200ms ease-out.</summary>
        public void PlayEntrance()
        {
            var timing = TimeSpan.FromMilliseconds(EntranceMillis);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            _root.Opacity = 0d;
            _root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0d, 1d, timing) { EasingFunction = ease });
            _slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(36d, 0d, timing) { EasingFunction = ease });

            _autoDismissTimer.Start();
        }

        /// <summary>Fade-out (150ms) then removal from the host stack.</summary>
        public void Dismiss(bool immediate = false)
        {
            if (_dismissStarted)
            {
                return;
            }

            _dismissStarted = true;
            _autoDismissTimer.Stop();

            if (immediate)
            {
                _owner.RemoveCard(this);
                return;
            }

            var timing = TimeSpan.FromMilliseconds(ExitMillis);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var fadeOut = new DoubleAnimation(1d, 0d, timing) { EasingFunction = ease };
            fadeOut.Completed += (_, _) => _owner.RemoveCard(this);

            _root.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            _slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0d, 24d, timing) { EasingFunction = ease });
        }

        private static string AccentBrushKey(ToastSeverity severity) => severity switch
        {
            ToastSeverity.Success => "SuccessBrush",
            ToastSeverity.Warning => "WarningBrush",
            ToastSeverity.Error => "ErrorBrush",
            _ => "InfoBrush"
        };

        private static string IconResourceKey(ToastSeverity severity) => severity switch
        {
            ToastSeverity.Success => "Icon_Success",
            ToastSeverity.Warning => "Icon_Warning",
            ToastSeverity.Error => "Icon_Error",
            _ => "Icon_Info"
        };

        /// <summary>Minimal chrome-free button template (transparent border hit area).</summary>
        private static ControlTemplate CreateGhostButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "Bd";
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));

            var presenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            presenterFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenterFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(presenterFactory);

            template.VisualTree = borderFactory;
            return template;
        }
    }
}
