using MyLanServer.UI.Views;

namespace MyLanServer.UI.Services;

/// <summary>
///     对话框服务，用于显示 Material Design 3 风格的对话框
/// </summary>
public static class DialogService
{
    /// <summary>
    ///     显示信息对话框
    /// </summary>
    public static void ShowInfo(string message, string title = "提示")
    {
        var dialog = new InfoDialog(message, title);
        dialog.ShowDialog();
    }

    /// <summary>
    ///     显示警告对话框
    /// </summary>
    public static void ShowWarning(string message, string title = "警告")
    {
        var dialog = new WarningDialog(message, title);
        dialog.ShowDialog();
    }

    /// <summary>
    ///     显示错误对话框
    /// </summary>
    public static void ShowError(string message, string title = "错误")
    {
        var dialog = new ErrorDialog(message, title);
        dialog.ShowDialog();
    }

    /// <summary>
    ///     显示确认对话框
    /// </summary>
    public static bool ShowConfirm(string message, string title = "确认")
    {
        var dialog = new ConfirmDialog(message, title);
        var result = dialog.ShowDialog();
        return result == true;
    }

    /// <summary>
    ///     显示输入对话框
    /// </summary>
    /// <param name="prompt">提示文本</param>
    /// <param name="title">对话框标题</param>
    /// <param name="placeholder">输入框占位符</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>用户输入的值，如果取消则返回 null</returns>
    public static string? ShowInputDialog(string prompt, string title = "输入", string placeholder = "",
        string defaultValue = "")
    {
        var dialog = new InputDialog(prompt, title, placeholder, defaultValue);
        var result = dialog.ShowDialog();
        return result == true ? dialog.InputValue : null;
    }
}