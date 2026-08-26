namespace Uninstra.App.Services;

using CommunityToolkit.Mvvm.ComponentModel;

/// <summary>
/// Lightweight routing service for the shell. The legacy public surface is
/// preserved (<see cref="CurrentPage"/>, <see cref="CurrentPageName"/>,
/// <see cref="NavigationRequested"/>, <see cref="NavigateTo(string)"/>);
/// on top of it this adds a view-factory registry, unknown-key guarding
/// via <see cref="TryNavigateTo"/>, and back/forward history.
/// </summary>
public sealed partial class NavigationService : ObservableObject
{
    private readonly Dictionary<string, Func<object>> _viewFactories = new(StringComparer.Ordinal);
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private string _currentPageName = "Programs";

    public event Action<string>? NavigationRequested;

    /// <summary>True when <see cref="GoBack"/> has somewhere to go.</summary>
    public bool CanGoBack => _backStack.Count > 0;

    /// <summary>True when <see cref="GoForward"/> has somewhere to go.</summary>
    public bool CanGoForward => _forwardStack.Count > 0;

    /// <summary>All routing keys that currently have a registered view factory.</summary>
    public IEnumerable<string> RegisteredPages => _viewFactories.Keys;

    /// <summary>Registers (or replaces) the factory that builds the view for a routing key.</summary>
    public void Register(string pageName, Func<object> viewFactory)
    {
        if (string.IsNullOrWhiteSpace(pageName))
        {
            throw new ArgumentException("Page name must not be empty.", nameof(pageName));
        }

        _viewFactories[pageName] = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));
    }

    /// <summary>
    /// Navigates by routing key. Legacy behavior preserved: always updates
    /// <see cref="CurrentPageName"/> and raises <see cref="NavigationRequested"/>.
    /// Additionally resolves <see cref="CurrentPage"/> through the registered
    /// factory (left untouched when no factory exists for the key) and records history.
    /// </summary>
    public void NavigateTo(string pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName))
        {
            return;
        }

        NavigateCore(pageName, recordHistory: true);
    }

    /// <summary>Navigates only when the routing key is registered; returns false for unknown keys.</summary>
    public bool TryNavigateTo(string pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName) || !_viewFactories.ContainsKey(pageName))
        {
            return false;
        }

        NavigateCore(pageName, recordHistory: true);
        return true;
    }

    /// <summary>Steps back through the navigation history. Returns false when there is nothing to go back to.</summary>
    public bool GoBack()
    {
        if (_backStack.Count == 0)
        {
            return false;
        }

        var target = _backStack.Pop();
        _forwardStack.Push(CurrentPageName);
        NavigateCore(target, recordHistory: false);
        return true;
    }

    /// <summary>Steps forward through the navigation history. Returns false when there is nothing to go forward to.</summary>
    public bool GoForward()
    {
        if (_forwardStack.Count == 0)
        {
            return false;
        }

        var target = _forwardStack.Pop();
        _backStack.Push(CurrentPageName);
        NavigateCore(target, recordHistory: false);
        return true;
    }

    private void NavigateCore(string pageName, bool recordHistory)
    {
        if (recordHistory && !string.Equals(CurrentPageName, pageName, StringComparison.Ordinal))
        {
            _backStack.Push(CurrentPageName);
            _forwardStack.Clear();
        }

        CurrentPageName = pageName;

        if (_viewFactories.TryGetValue(pageName, out var factory))
        {
            CurrentPage = factory();
        }

        NavigationRequested?.Invoke(pageName);

        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }
}
