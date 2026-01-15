using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MyLanServer.UI.ViewModels;

namespace MyLanServer.UI.Views;

public partial class TaskConfigDialog
{
    private readonly TaskConfigViewModel _viewModel;

    public TaskConfigDialog(TaskConfigViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // 设置关闭回调
        _viewModel.SetCloseCallback(result =>
        {
            DialogResult = result;
            Close();
        });
    }

    // 关闭按钮点击事件
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    // 数字输入验证
    private void Number_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsTextAllowed(e.Text);
    }

    private bool IsTextAllowed(string text)
    {
        return Regex.IsMatch(text, @"^[0-9]+$");
    }

    // 密码框绑定到ViewModel
    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox) _viewModel.TaskPassword = passwordBox.Password;
    }

    // 时间输入框失去焦点时验证
    private void TimeBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // 如果文本为空，设置为0
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "00";
                return;
            }

            // 尝试解析为整数
            if (int.TryParse(textBox.Text, out var value))
                // 根据不同的输入框验证范围
                switch (textBox.Name)
                {
                    case "HourBox":
                        _viewModel.ValidateAndFixHour(value);
                        break;
                    case "MinuteBox":
                        _viewModel.ValidateAndFixMinute(value);
                        break;
                    case "SecondBox":
                        _viewModel.ValidateAndFixSecond(value);
                        break;
                }
            else
                // 如果无法解析为整数，设置为0
                textBox.Text = "00";
        }
    }

    // 拖拽进入事件（隧道事件）
    private void TemplateFileBorder_PreviewDragEnter(object sender, DragEventArgs e)
    {
        // 检查是否包含文件
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;

            // 视觉反馈：改变边框和背景
            if (TemplateFileBorder != null)
            {
                TemplateFileBorder.BorderBrush = FindResource("MaterialDesignPrimary") as Brush;
                TemplateFileBorder.BorderThickness = new Thickness(2);
                TemplateFileBorder.Background = FindResource("MaterialDesignTextFieldBoxHoverBackground") as Brush;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    // 拖拽悬停事件（隧道事件）
    private void TemplateFileBorder_PreviewDragOver(object sender, DragEventArgs e)
    {
        // 保持拖拽效果
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    // 拖拽离开事件（隧道事件）
    private void TemplateFileBorder_PreviewDragLeave(object sender, DragEventArgs e)
    {
        // 恢复视觉状态
        ResetTemplateFileVisualState();
        e.Handled = true;
    }

    // 拖拽放置事件（隧道事件）
    private void TemplateFileBorder_PreviewDrop(object sender, DragEventArgs e)
    {
        // 恢复视觉状态
        ResetTemplateFileVisualState();

        // 检查是否包含文件
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
                // 调用 ViewModel 处理文件
                _viewModel.HandleTemplateFileDrop(files[0]);
        }

        e.Handled = true;
    }

    // 恢复模板文件视觉状态
    private void ResetTemplateFileVisualState()
    {
        if (TemplateFileBorder != null)
        {
            TemplateFileBorder.BorderBrush = null;
            TemplateFileBorder.BorderThickness = new Thickness(0);
            TemplateFileBorder.Background = FindResource("MaterialDesignCardBackground") as Brush;
        }
    }

    // 自定义扩展名输入框回车键处理
    private void CustomExtensionInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _viewModel.AddCustomExtensionCommand?.Execute(null);
    }

    // 拖拽进入事件（附件列表）
    private void AttachmentListBorder_PreviewDragEnter(object sender, DragEventArgs e)
    {
        // 检查是否包含文件
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;

            // 视觉反馈：改变边框和背景
            if (AttachmentListBorder != null)
            {
                AttachmentListBorder.BorderBrush = FindResource("MaterialDesignPrimary") as Brush;
                AttachmentListBorder.BorderThickness = new Thickness(2);
                AttachmentListBorder.Background = FindResource("MaterialDesignTextFieldBoxHoverBackground") as Brush;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    // 拖拽悬停事件（附件列表）
    private void AttachmentListBorder_PreviewDragOver(object sender, DragEventArgs e)
    {
        // 保持拖拽效果
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    // 拖拽离开事件（附件列表）
    private void AttachmentListBorder_PreviewDragLeave(object sender, DragEventArgs e)
    {
        // 恢复视觉状态
        ResetAttachmentListVisualState();
        e.Handled = true;
    }

    // 拖拽放置事件（附件列表）
    private void AttachmentListBorder_PreviewDrop(object sender, DragEventArgs e)
    {
        // 恢复视觉状态
        ResetAttachmentListVisualState();

        // 检查是否包含文件
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
                // 调用 ViewModel 处理多个文件
                _viewModel.HandleAttachmentsDrop(files);
        }

        e.Handled = true;
    }

    // 恢复附件列表视觉状态
    private void ResetAttachmentListVisualState()
    {
        if (AttachmentListBorder != null)
        {
            AttachmentListBorder.BorderBrush = FindResource("MaterialDesignDivider") as Brush;
            AttachmentListBorder.BorderThickness = new Thickness(1);
            AttachmentListBorder.Background = FindResource("MaterialDesignTextFieldBoxBackground") as Brush;
        }
    }
}