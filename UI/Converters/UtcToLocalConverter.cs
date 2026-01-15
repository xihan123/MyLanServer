using System.Globalization;
using System.Windows.Data;

namespace MyLanServer.UI.Converters;

/// <summary>
///     UTC 时间转本地时间转换器
/// </summary>
public class UtcToLocalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dateTime) return dateTime.ToLocalTime();

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}