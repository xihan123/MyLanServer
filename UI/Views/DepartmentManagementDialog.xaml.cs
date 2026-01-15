using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyLanServer.UI.ViewModels;

namespace MyLanServer.UI.Views;

public partial class DepartmentManagementDialog
{
    private readonly DepartmentViewModel _viewModel;

    public DepartmentManagementDialog(DepartmentViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // 设置关闭回调
        _viewModel.SetCloseDialogCallback(result =>
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

    // 部门名称输入框回车键事件
    private void DepartmentName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            // 调用 ViewModel 的保存命令
            if (_viewModel.SaveDepartmentCommand != null && _viewModel.SaveDepartmentCommand.CanExecute(null))
                _viewModel.SaveDepartmentCommand.Execute(null);
    }

    // 右键菜单处理 - 选中当前行
    private void DataGridRow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row)
            // 确保右键点击的行被选中
            row.IsSelected = true;
    }
}