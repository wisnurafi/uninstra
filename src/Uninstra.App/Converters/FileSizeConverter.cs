namespace Uninstra.App.Converters;

using System.Globalization;
using System.Windows.Data;

public sealed class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            return bytes switch
            {
                0 => "—",
                < 1024 => $"{bytes} B",
                < 1048576 => $"{bytes / 1024.0:F1} KB",
                < 1073741824 => $"{bytes / 1048576.0:F1} MB",
                _ => $"{bytes / 1073741824.0:F2} GB"
            };
        }
        return "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
