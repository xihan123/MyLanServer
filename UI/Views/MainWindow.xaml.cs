using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyLanServer.UI.ViewModels;

namespace MyLanServer.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    // 端口输入验证（只允许数字）
    private void Port_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsTextAllowed(e.Text);
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

    // 密码框绑定到ViewModel（已移除，现在在TaskConfigDialog中处理）

    // 处理时间输入（时、分、秒）- 移除TextChanged验证以避免红框
    private void TimeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // 移除TextChanged中的验证，改为在LostFocus时验证
        // 这样可以避免输入过程中显示验证错误红框
    }

    // 时间输入框失去焦点时验证
    private void TimeBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            int value;
            if (int.TryParse(textBox.Text, out value))
            {
                // 根据不同的输入框验证范围
                var isValid = false;
                if (textBox.Name == "HourBox")
                    isValid = value >= 0 && value <= 23;
                else if (textBox.Name == "MinuteBox" || textBox.Name == "SecondBox")
                    isValid = value >= 0 && value <= 59;

                // 如果无效，清空或显示提示
                if (!isValid)
                    textBox.Text = "00";
                else
                    // 格式化为两位数
                    textBox.Text = value.ToString("D2");
            }
            else
            {
                textBox.Text = "00";
            }
        }
    }

    // 双击任务编辑
    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _viewModel.EditTaskCommand?.Execute(null);
    }

    // 右键菜单处理 - 选中当前行
    private void DataGridRow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row)
            // 确保右键点击的行被选中
            row.IsSelected = true;
    }

    // 双击任务编辑
    private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _viewModel.EditTaskCommand?.Execute(null);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else
            WindowState = WindowState.Maximized;
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }
}