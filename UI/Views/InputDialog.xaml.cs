using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace MyLanServer.UI.Views;

public partial class InputDialog : INotifyPropertyChanged
{
    public InputDialog(string prompt, string title = "输入", string placeholder = "", string defaultValue = "")
    {
        InitializeComponent();
        Prompt = prompt;
        Title = title;
        Placeholder = placeholder;
        InputValue = defaultValue;
        DataContext = this;

        // 窗口加载后聚焦到输入框
        Loaded += (s, e) => InputTextBox.Focus();
    }

    public string Prompt { get; set; } = string.Empty;
    public string Placeholder { get; set; } = string.Empty;
    public string InputValue { get; set; } = string.Empty;
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