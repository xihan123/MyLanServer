using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace MyLanServer.UI.Views;

public partial class ConfirmDialog : INotifyPropertyChanged
{
    public ConfirmDialog(string message, string title = "确认")
    {
        InitializeComponent();
        Message = message;
        Title = title;
        DataContext = this;
    }

    public string Message { get; set; } = string.Empty;
    public new string Title { get; set; } = string.Empty;

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

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}