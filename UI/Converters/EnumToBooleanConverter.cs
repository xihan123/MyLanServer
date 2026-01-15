using System.Globalization;
using System.Windows.Data;

namespace MyLanServer.UI.Converters;

/// <summary>
///     枚举转布尔转换器（用于RadioButton绑定）
/// </summary>
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        // 检查枚举类型是否匹配
        var enumType = value.GetType();
        if (!enumType.IsEnum)
            return false;

        // 将参数字符串转换为枚举值
        var parameterString = parameter.ToString();
        if (!string.IsNullOrEmpty(parameterString) && Enum.IsDefined(enumType, parameterString))
        {
            var parameterValue = Enum.Parse(enumType, parameterString);
            return value.Equals(parameterValue);
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter != null)
        {
            var parameterString = parameter.ToString();
            if (!string.IsNullOrEmpty(parameterString) && Enum.IsDefined(targetType, parameterString))
                return Enum.Parse(targetType, parameterString);
        }

        return Binding.DoNothing;
    }
}