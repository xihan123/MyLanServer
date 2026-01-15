using System.Globalization;
using System.Windows.Data;

namespace MyLanServer.UI.Converters;

/// <summary>
///     限制值转换器（将 0 转换为 "不限"）
/// </summary>
public class LimitConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue) return intValue == 0 ? "不限" : intValue.ToString();

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string strValue) return strValue == "不限" ? 0 : int.Parse(strValue);

        return value;
    }
}