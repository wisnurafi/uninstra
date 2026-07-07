namespace Uninstra.App.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Support for string values (e.g., Phase property)
        if (value is string strValue && parameter is string param)
        {
            if (param == "notdone")
                return strValue != "done" ? Visibility.Visible : Visibility.Collapsed;
            if (param == "done")
                return strValue == "done" ? Visibility.Visible : Visibility.Collapsed;
        }

        // Support "invert" parameter
        if (parameter is string p && p == "invert")
            return value is true ? Visibility.Collapsed : Visibility.Visible;

        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
