namespace Uninstra.App.Converters;

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Data;

public sealed class BoolToOnOffConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isTrue = value is true;
        var param = parameter?.ToString()?.ToLowerInvariant();

        return param switch
        {
            "bg" => isTrue 
                ? new SolidColorBrush(Color.FromRgb(0x15, 0x22, 0xC5)) // Success bg
                : new SolidColorBrush(Color.FromRgb(0x15, 0xEF, 0x44)), // Error bg
            "border" => isTrue
                ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)) // Success
                : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), // Error
            "fg" => isTrue
                ? new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)) // Success
                : new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), // Error
            _ => isTrue ? "ON" : "OFF"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && s.Equals("ON", StringComparison.OrdinalIgnoreCase);
}
