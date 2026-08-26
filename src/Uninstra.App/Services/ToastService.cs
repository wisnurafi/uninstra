namespace Uninstra.App.Services;

using System.Windows;
using System.Windows.Threading;

/// <summary>
/// Raises toast notifications to <see cref="IToastService.ToastRaised"/> listeners
/// (the ToastHost overlay subscribes). Thread-safe: calls made from background
/// threads are marshaled onto the WPF UI dispatcher before the event fires, so
/// subscribers never need their own dispatching.
/// </summary>
public sealed class ToastService : IToastService
{
    // Fallback when Application.Current is unavailable (very early startup / tests).
    private readonly Dispatcher _fallbackDispatcher = Dispatcher.CurrentDispatcher;

    public event Action<ToastNotification>? ToastRaised;

    public void ShowSuccess(string message, string? title = null) => Publish(message, title, ToastSeverity.Success);

    public void ShowWarning(string message, string? title = null) => Publish(message, title, ToastSeverity.Warning);

    public void ShowError(string message, string? title = null) => Publish(message, title, ToastSeverity.Error);

    public void ShowInfo(string message, string? title = null) => Publish(message, title, ToastSeverity.Info);

    private void Publish(string message, string? title, ToastSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var timeoutSeconds = severity == ToastSeverity.Error
            ? IToastService.ErrorTimeoutSeconds
            : IToastService.DefaultTimeoutSeconds;

        var notification = new ToastNotification(
            message.Trim(),
            string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            severity,
            TimeSpan.FromSeconds(timeoutSeconds));

        var dispatcher = Application.Current?.Dispatcher ?? _fallbackDispatcher;
        if (dispatcher.CheckAccess())
        {
            ToastRaised?.Invoke(notification);
        }
        else
        {
            _ = dispatcher.BeginInvoke(() => ToastRaised?.Invoke(notification));
        }
    }
}
