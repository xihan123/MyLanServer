using CommunityToolkit.Mvvm.ComponentModel;

namespace MyLanServer.Core.Models;

/// <summary>
///     列配置模型：用于管理DataGrid列的显示、宽度和顺序
/// </summary>
public partial class ColumnConfig : ObservableObject
{
    /// <summary>
    ///     是否支持文本换行
    /// </summary>
    [ObservableProperty] private bool _allowTextWrapping;

    /// <summary>
    ///     数据绑定路径
    /// </summary>
    [ObservableProperty] private string _bindingPath = string.Empty;

    /// <summary>
    ///     列标题
    /// </summary>
    [ObservableProperty] private string _header = string.Empty;

    /// <summary>
    ///     是否可见
    /// </summary>
    [ObservableProperty] private bool _isVisible = true;

    /// <summary>
    ///     最小宽度
    /// </summary>
    [ObservableProperty] private double _minWidth = 80;

    /// <summary>
    ///     排序顺序
    /// </summary>
    [ObservableProperty] private int _sortOrder;

    /// <summary>
    ///     字符串格式化
    /// </summary>
    [ObservableProperty] private string? _stringFormat;

    /// <summary>
    ///     文本截断方式
    /// </summary>
    [ObservableProperty] private string _textTrimming = "CharacterEllipsis";

    /// <summary>
    ///     列宽度
    /// </summary>
    [ObservableProperty] private double _width = 100;
}