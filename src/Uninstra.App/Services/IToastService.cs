namespace Uninstra.App.Services;

/// <summary>Severity of an in-app toast notification. Drives accent colour and icon.</summary>
public enum ToastSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>A single toast request produced by <see cref="IToastService"/>.</summary>
/// <param name="Message">Body copy shown on the card.</param>
/// <param name="Title">Optional bold heading rendered above the message.</param>
/// <param name="Severity">Semantic severity of the toast.</param>
/// <param name="AutoDismissAfter">How long the card stays visible before auto-dismissing.</param>
public sealed record ToastNotification(
    string Message,
    string? Title,
    ToastSeverity Severity,
    TimeSpan AutoDismissAfter);

/// <summary>
/// In-app toast notifications (never MessageBox). Implementations must raise
/// <see cref="ToastRaised"/> on the WPF UI thread and are safe to call from any thread.
/// Register as a singleton: <c>services.AddSingleton&lt;IToastService, ToastService&gt;();</c>
/// </summary>
public interface IToastService
{
    /// <summary>Maximum number of simultaneously visible toasts (enforced by the ToastHost overlay).</summary>
    const int MaxVisibleToasts = 4;

    /// <summary>Auto-dismiss delay in seconds for success / warning / info toasts.</summary>
    const int DefaultTimeoutSeconds = 4;

    /// <summary>Auto-dismiss delay in seconds for error toasts (kept longer for reading).</summary>
    const int ErrorTimeoutSeconds = 7;

    /// <summary>Raised on the UI thread whenever a toast should become visible.</summary>
    event Action<ToastNotification>? ToastRaised;

    /// <summary>Shows a lime/green success toast.</summary>
    void ShowSuccess(string message, string? title = null);

    /// <summary>Shows an amber warning toast.</summary>
    void ShowWarning(string message, string? title = null);

    /// <summary>Shows a red error toast (auto-dismisses after 7 seconds).</summary>
    void ShowError(string message, string? title = null);

    /// <summary>Shows a blue informational toast.</summary>
    void ShowInfo(string message, string? title = null);
}
