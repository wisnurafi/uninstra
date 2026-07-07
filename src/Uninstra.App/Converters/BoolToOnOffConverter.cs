namespace Uninstra.App.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Data;

public sealed class BoolToOnOffConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "ON" : "OFF";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && s.Equals("ON", StringComparison.OrdinalIgnoreCase);
}
