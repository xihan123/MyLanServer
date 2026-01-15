using System.Windows;
using MyLanServer.UI.ViewModels;

namespace MyLanServer.UI.Views;

/// <summary>
///     ColumnSelectorDialog.xaml 的交互逻辑
/// </summary>
public partial class ColumnSelectorDialog : Window
{
    private readonly ColumnSelectorViewModel _viewModel;

    public ColumnSelectorDialog(ColumnSelectorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.SetCloseDialogCallback(CloseDialog);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseDialog(false);
    }

    private void CloseDialog(bool result)
    {
        DialogResult = result;
        Close();
    }
}