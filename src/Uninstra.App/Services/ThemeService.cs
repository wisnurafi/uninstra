namespace Uninstra.App.Services;

using System.Windows;

public sealed class ThemeService
{
    public void ApplyTheme(string theme)
    {
        var dict = new ResourceDictionary();
        var uri = theme switch
        {
            "Light" => new Uri("Themes/LightTheme.xaml", UriKind.Relative),
            _ => new Uri("Themes/DarkTheme.xaml", UriKind.Relative)
        };

        try
        {
            dict.Source = uri;
            System.Windows.Application.Current.Resources.MergedDictionaries.Clear();
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(dict);
        }
        catch
        {
            // Fallback to dark if theme file not found
        }
    }
}
