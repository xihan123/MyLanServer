using System.Globalization;
using System.Windows.Data;

namespace MyLanServer.UI.Converters;

/// <summary>
///     布尔值反转转换器（用于按钮禁用状态）
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue && !boolValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue && !boolValue;
    }
}