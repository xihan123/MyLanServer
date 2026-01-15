using System.Windows;
using MyLanServer.UI.ViewModels;

namespace MyLanServer.UI.Views;

public partial class MergeDialog : Window
{
    private readonly MergeDialogViewModel _viewModel;

    public MergeDialog(MergeDialogViewModel viewModel)
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