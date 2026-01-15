using System.Globalization;
using System.Windows.Data;
using MyLanServer.Core.Enums;

namespace MyLanServer.UI.Converters;

/// <summary>
///     MergeMode 枚举转换器（转换为中文显示）
/// </summary>
public class MergeModeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MergeMode mode)
            return mode switch
            {
                MergeMode.Accumulate => "累计",
                MergeMode.GroupBy => "分组统计",
                _ => mode.ToString()
            };

        return value?.ToString() ?? "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}