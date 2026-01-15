using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MyLanServer.UI.Converters;

/// <summary>
///     通用条件到 Visibility 转换器
///     通过 Condition 属性支持多种条件判断
/// </summary>
public class ConditionToVisibilityConverter : IValueConverter
{
    /// <summary>
    ///     条件类型：IsTrue, IsFalse, IsNotNull, IsNullOrEmpty, GreaterThan, LessThan, Equal
    /// </summary>
    public string Condition { get; set; } = "IsTrue";

    /// <summary>
    ///     条件满足时返回的 Visibility
    /// </summary>
    public Visibility TrueValue { get; set; } = Visibility.Visible;

    /// <summary>
    ///     条件不满足时返回的 Visibility
    /// </summary>
    public Visibility FalseValue { get; set; } = Visibility.Collapsed;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 如果提供了 parameter，优先作为比较值（用于兼容 XAML 中的 ConverterParameter）
        var compareValue = parameter;

        bool result = Condition switch
        {
            "IsTrue" => value is bool boolValue && boolValue,
            "IsFalse" => value is bool boolValue && !boolValue,
            "IsNotNull" => value is not null,
            "IsNull" => value is null,
            "IsNullOrEmpty" => value is null || (value is string str && string.IsNullOrEmpty(str)),
            "IsNotNullOrEmpty" => value is not null && (value is not string str || !string.IsNullOrEmpty(str)),
            "IsNotNullOrWhiteSpace" => value is not null &&
                                       (value is not string str || !string.IsNullOrWhiteSpace(str)),
            "IsNullOrWhiteSpace" => value is null || (value is string str && string.IsNullOrWhiteSpace(str)),
            "GreaterThan" => value is IComparable comparable && compareValue is IComparable comp &&
                             comparable.CompareTo(comp) > 0,
            "LessThan" => value is IComparable comparable && compareValue is IComparable comp &&
                          comparable.CompareTo(comp) < 0,
            "Equal" => value?.Equals(compareValue) == true,
            "NotEqual" => value?.Equals(compareValue) != true,
            _ => false
        };

        return result ? TrueValue : FalseValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}