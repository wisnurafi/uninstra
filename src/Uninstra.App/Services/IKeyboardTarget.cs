namespace Uninstra.App.Services;

/// <summary>
/// Contract implemented by shell pages (or their view-models) to receive
/// global keyboard shortcuts from <c>MainWindow</c>.
/// Resolution order: page itself, then page.DataContext. A defensive
/// reflection fallback also accepts public parameterless
/// <c>Refresh()</c> / <c>FocusSearch()</c> methods, so adopting the
/// interface is optional.
/// </summary>
public interface IKeyboardTarget
{
    /// <summary>Moves focus to the page's search box (bound to Ctrl+F).</summary>
    void FocusSearch();

    /// <summary>Reloads the page data (bound to F5).</summary>
    void Refresh();
}
