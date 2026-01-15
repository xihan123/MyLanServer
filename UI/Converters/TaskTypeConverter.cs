using System.Globalization;
using System.Windows.Data;
using MyLanServer.Core.Enums;

namespace MyLanServer.UI.Converters;

/// <summary>
///     TaskType 枚举转换器（转换为中文显示）
/// </summary>
public class TaskTypeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TaskType taskType)
            return taskType switch
            {
                TaskType.FileCollection => "文件收集",
                TaskType.DataCollection => "数据收集",
                _ => taskType.ToString()
            };

        return value?.ToString() ?? "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}