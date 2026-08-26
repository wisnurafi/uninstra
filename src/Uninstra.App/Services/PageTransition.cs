namespace Uninstra.App.Services;

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

/// <summary>
/// Shell page-transition helper. Plays the incoming-page entrance:
/// opacity 0 -> 1 plus TranslateTransform.X 18 -> 0 over 160ms ease-out.
/// Called by <c>MainWindow.NavigateTo</c> right after the content swap.
/// </summary>
public static class PageTransition
{
    private const double SlideFromX = 18d;
    private const int DurationMs = 160;

    public static void PlaySlideIn(FrameworkElement? incomingPage)
    {
        if (incomingPage is null)
        {
            return;
        }

        // Local transform intentionally replaces the theme's implicit
        // UserControl TranslateTransform (Y=12): the shell owns the
        // horizontal entrance; the theme's Loaded storyboard keeps working
        // on the opacity channel without fighting this one.
        var slide = new TranslateTransform(SlideFromX, 0);
        incomingPage.RenderTransform = slide;

        var fade = new DoubleAnimation(0d, 1d, TimeSpan.FromMilliseconds(DurationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        var move = new DoubleAnimation(SlideFromX, 0d, TimeSpan.FromMilliseconds(DurationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        incomingPage.BeginAnimation(UIElement.OpacityProperty, fade);
        slide.BeginAnimation(TranslateTransform.XProperty, move);
    }
}
