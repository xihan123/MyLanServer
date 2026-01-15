using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Models;

namespace MyLanServer.UI.ViewModels;

/// <summary>
///     列选择器视图模型
/// </summary>
public partial class ColumnSelectorViewModel : ObservableObject
{
    private readonly ILogger<ColumnSelectorViewModel> _logger;

    // 关闭对话框的回调
    private Action<bool>? _closeDialogCallback;

    // 列配置集合
    [ObservableProperty] private ObservableCollection<ColumnConfig> _columnConfigs = new();

    public ColumnSelectorViewModel(ILogger<ColumnSelectorViewModel> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     设置列配置
    /// </summary>
    public void SetColumnConfigs(ObservableCollection<ColumnConfig> configs)
    {
        ColumnConfigs = configs;
    }

    /// <summary>
    ///     全选命令
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        _logger.LogInformation("SelectAll: 全选所有列");
        foreach (var config in ColumnConfigs) config.IsVisible = true;
    }

    /// <summary>
    ///     取消全选命令
    /// </summary>
    [RelayCommand]
    private void DeselectAll()
    {
        _logger.LogInformation("DeselectAll: 取消全选所有列");
        foreach (var config in ColumnConfigs) config.IsVisible = false;
    }

    /// <summary>
    ///     重置为默认配置命令
    /// </summary>
    [RelayCommand]
    private void ResetToDefault()
    {
        _logger.LogInformation("ResetToDefault: 重置为默认配置");
        var defaultConfigs = new[]
        {
            new { Header = "姓名", BindingPath = "Name", IsVisible = true },
            new { Header = "身份证号", BindingPath = "IdCard", IsVisible = true },
            new { Header = "联系方式", BindingPath = "Contact", IsVisible = true },
            new { Header = "部门", BindingPath = "Department", IsVisible = true },
            new { Header = "性别", BindingPath = "Gender", IsVisible = true },
            new { Header = "年龄", BindingPath = "Age", IsVisible = true },
            new { Header = "出生日期", BindingPath = "BirthDate", IsVisible = true },
            new { Header = "户籍地址", BindingPath = "RegisteredAddress", IsVisible = true },
            new { Header = "工号", BindingPath = "EmployeeNumber", IsVisible = true },
            new { Header = "职级", BindingPath = "Rank", IsVisible = true },
            new { Header = "创建时间", BindingPath = "CreatedAt", IsVisible = true },
            new { Header = "更新时间", BindingPath = "UpdatedAt", IsVisible = true }
        };

        foreach (var defaultConfig in defaultConfigs)
        {
            var config = ColumnConfigs.FirstOrDefault(c => c.Header == defaultConfig.Header);
            if (config != null) config.IsVisible = defaultConfig.IsVisible;
        }
    }

    /// <summary>
    ///     上移列命令
    /// </summary>
    [RelayCommand]
    private void MoveUp(ColumnConfig config)
    {
        var index = ColumnConfigs.IndexOf(config);
        if (index > 0)
        {
            ColumnConfigs.Move(index, index - 1);
            UpdateSortOrders();
            _logger.LogInformation("MoveUp: 上移列 {Header} 从位置 {OldIndex} 到 {NewIndex}", config.Header, index, index - 1);
        }
    }

    /// <summary>
    ///     下移列命令
    /// </summary>
    [RelayCommand]
    private void MoveDown(ColumnConfig config)
    {
        var index = ColumnConfigs.IndexOf(config);
        if (index < ColumnConfigs.Count - 1)
        {
            ColumnConfigs.Move(index, index + 1);
            UpdateSortOrders();
            _logger.LogInformation("MoveDown: 下移列 {Header} 从位置 {OldIndex} 到 {NewIndex}", config.Header, index,
                index + 1);
        }
    }

    /// <summary>
    ///     更新排序顺序
    /// </summary>
    private void UpdateSortOrders()
    {
        for (var i = 0; i < ColumnConfigs.Count; i++) ColumnConfigs[i].SortOrder = i;
    }

    /// <summary>
    ///     确认命令
    /// </summary>
    [RelayCommand]
    private void Confirm()
    {
        _logger.LogInformation("Confirm: 用户确认列配置修改");
        _logger.LogInformation("当前列配置状态:");
        for (var i = 0; i < ColumnConfigs.Count; i++)
            _logger.LogInformation("  [{Index}] {Header} - IsVisible={IsVisible}",
                i, ColumnConfigs[i].Header, ColumnConfigs[i].IsVisible);

        _closeDialogCallback?.Invoke(true);
    }

    /// <summary>
    ///     取消命令
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _logger.LogInformation("Cancel: 用户取消列配置修改");
        _closeDialogCallback?.Invoke(false);
    }

    /// <summary>
    ///     设置关闭对话框的回调
    /// </summary>
    public void SetCloseDialogCallback(Action<bool> callback)
    {
        _closeDialogCallback = callback;
    }
}