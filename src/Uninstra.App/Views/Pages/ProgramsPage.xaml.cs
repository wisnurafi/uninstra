namespace Uninstra.App.Views.Pages;

using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using Uninstra.App.ViewModels;

public partial class ProgramsPage : UserControl
{
    public ProgramsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ProgramsViewModel>();
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ProgramsViewModel vm)
            await vm.LoadCommand.ExecuteAsync(null);
    }
}

/// <summary>
/// Returns the first letter of a program display name, uppercased — used by the
/// rounded-square letter avatar in the detail panel. Falls back to "?".
/// </summary>
public sealed class FirstLetterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && s.Length > 0 ? s[..1].ToUpperInvariant() : "?";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
