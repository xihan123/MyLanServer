using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace MyLanServer.UI.Views;

public partial class DeleteConfirmDialog : INotifyPropertyChanged
{
    private bool _deleteFolder;

    public DeleteConfirmDialog(string message)
    {
        InitializeComponent();
        Message = message;
        DataContext = this;
    }

    public bool DeleteFolder
    {
        get => _deleteFolder;
        set
        {
            _deleteFolder = value;
            OnPropertyChanged(nameof(DeleteFolder));
        }
    }

    public string Message { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // 支持无边框窗口拖动
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}