using System.Windows;
using MyLanServer.UI.ViewModels;

namespace MyLanServer.UI.Views;

public partial class SubmissionListDialog : Window
{
    private readonly SubmissionListViewModel _viewModel;

    public SubmissionListDialog(SubmissionListViewModel viewModel)
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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}