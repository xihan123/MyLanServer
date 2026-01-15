using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MyLanServer.Core.Models;

namespace MyLanServer.UI.Converters;

/// <summary>
///     任务配置可见性转换器，检查任务是否有任何配置（密码、限制、一次性）
/// </summary>
public class TaskHasConfigVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LanTask task)
        {
            // 检查是否有任何配置
            var hasPassword = !string.IsNullOrWhiteSpace(task.PasswordHash);
            var hasLimit = task.MaxLimit > 0;
            var isOneTime = task.IsOneTimeLink;

            return hasPassword || hasLimit || isOneTime ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}