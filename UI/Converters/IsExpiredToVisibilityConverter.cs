using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyLanServer.UI.Converters;

/// <summary>
///     过期状态转换器（将已过期的日期转换为 Visible，未过期或无日期转换为 Collapsed）
/// </summary>
public class IsExpiredToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var date = value as DateTime?;
        if (date.HasValue && date.Value < DateTime.Now) return Visibility.Visible;

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}