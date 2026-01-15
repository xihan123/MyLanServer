using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using MyLanServer.UI.Converters;
using MyLanServer.UI.ViewModels;

namespace MyLanServer.UI.Views;

/// <summary>
///     PersonManagementDialog.xaml 的交互逻辑
/// </summary>
public partial class PersonManagementDialog : Window
{
    private readonly PersonViewModel _viewModel;

    public PersonManagementDialog(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _viewModel = serviceProvider.GetRequiredService<PersonViewModel>();
        DataContext = _viewModel;

        _viewModel.SetCloseDialogCallback(CloseDialog);

        // 监听 ColumnConfigs 集合变更，重新生成列
        _viewModel.ColumnConfigs.CollectionChanged += (s, e) =>
        {
            // 如果不是从 DataGrid 拖拽触发的更新，才重新生成列
            if (!_viewModel.IsUpdatingFromDataGrid) RefreshColumns();
        };

        // 动态生成 DataGrid 列
        CreateColumns();

        // 监听 DataGrid.Columns 集合变化，检测列拖拽
        PersonDataGrid.Columns.CollectionChanged += PersonDataGrid_Columns_CollectionChanged;
    }

    /// <summary>
    ///     刷新 DataGrid 列（清空并重新创建）
    /// </summary>
    private void RefreshColumns()
    {
        // 清空现有列
        PersonDataGrid.Columns.Clear();
        // 重新创建列
        CreateColumns();
    }

    /// <summary>
    ///     动态生成 DataGrid 列
    /// </summary>
    private void CreateColumns()
    {
        if (_viewModel.ColumnConfigs == null || _viewModel.ColumnConfigs.Count == 0) return;

        for (var i = 0; i < _viewModel.ColumnConfigs.Count; i++)
        {
            var config = _viewModel.ColumnConfigs[i];

            var column = new DataGridTemplateColumn
            {
                Header = config.Header,
                DisplayIndex = i // 直接设置 DisplayIndex，不使用绑定
            };

            // 绑定 Width
            var widthBinding = new Binding($"ColumnConfigs[{i}].Width")
            {
                Source = _viewModel,
                Mode = BindingMode.TwoWay
            };
            BindingOperations.SetBinding(column, DataGridColumn.WidthProperty, widthBinding);

            // 绑定 MinWidth
            var minWidthBinding = new Binding($"ColumnConfigs[{i}].MinWidth")
            {
                Source = _viewModel,
                Mode = BindingMode.TwoWay
            };
            BindingOperations.SetBinding(column, DataGridColumn.MinWidthProperty, minWidthBinding);

            // 绑定 Visibility
            var visibilityBinding = new Binding($"ColumnConfigs[{i}].IsVisible")
            {
                Source = _viewModel,
                Mode = BindingMode.OneWay,
                Converter = new ConditionToVisibilityConverter { Condition = "IsTrue" }
            };
            BindingOperations.SetBinding(column, DataGridColumn.VisibilityProperty, visibilityBinding);

            // 设置 CellTemplate
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetBinding(TextBlock.TextProperty, new Binding(config.BindingPath));

            // 应用 StringFormat
            if (!string.IsNullOrEmpty(config.StringFormat))
                factory.SetValue(TextBlock.TextProperty, new Binding(config.BindingPath)
                {
                    StringFormat = config.StringFormat
                });

            // 设置垂直对齐
            factory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);

            // 处理文本换行和截断
            if (config.AllowTextWrapping) factory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);

            if (!string.IsNullOrEmpty(config.TextTrimming))
                factory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);

            column.CellTemplate = new DataTemplate { VisualTree = factory };

            PersonDataGrid.Columns.Add(column);
        }

        // 重新绑定 ColumnReordered 事件（确保事件处理器有效）
        PersonDataGrid.ColumnReordered -= PersonDataGrid_ColumnReordered;
        PersonDataGrid.ColumnReordered += PersonDataGrid_ColumnReordered;
    }

    /// <summary>
    ///     DataGrid 列重新排序事件处理
    /// </summary>
    private void PersonDataGrid_ColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        // 获取 DataGrid 的新列顺序（按 DisplayIndex 排序）
        var newColumnOrder = PersonDataGrid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Select(c => c.Header?.ToString())
            .OfType<string>()
            .ToList();

        // 记录日志
        Debug.WriteLine($"[PersonManagementDialog] ColumnReordered 事件触发，新列顺序: {string.Join(", ", newColumnOrder)}");

        // 通知 ViewModel 更新列配置
        _viewModel.UpdateColumnOrderFromDataGrid(newColumnOrder);
    }

    /// <summary>
    ///     DataGrid 列集合变化事件处理（用于检测列拖拽）
    /// </summary>
    private void PersonDataGrid_Columns_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 只处理 Move 操作（拖拽列顺序变化）
        if (e.Action == NotifyCollectionChangedAction.Move)
        {
            // 获取 DataGrid 的新列顺序（按 DisplayIndex 排序）
            var newColumnOrder = PersonDataGrid.Columns
                .OrderBy(c => c.DisplayIndex)
                .Select(c => c.Header?.ToString())
                .OfType<string>()
                .ToList();

            // 记录日志
            Debug.WriteLine(
                $"[PersonManagementDialog] Columns.CollectionChanged 触发，新列顺序: {string.Join(", ", newColumnOrder)}");

            // 通知 ViewModel 更新列配置
            _viewModel.UpdateColumnOrderFromDataGrid(newColumnOrder);
        }
    }

    private void CloseDialog(bool result)
    {
        DialogResult = result;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseDialog(true);
    }
}