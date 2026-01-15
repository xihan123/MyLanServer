using CommunityToolkit.Mvvm.ComponentModel;

namespace MyLanServer.Core.Models;

/// <summary>
///     代表一个部门
/// </summary>
public partial class Department : ObservableObject
{
    /// <summary>
    ///     部门唯一标识（自增主键）
    /// </summary>
    [ObservableProperty] private int _id;

    /// <summary>
    ///     部门名称
    /// </summary>
    [ObservableProperty] private string _name = string.Empty;

    /// <summary>
    ///     排序顺序（数值越小越靠前）
    /// </summary>
    [ObservableProperty] private int _sortOrder;
}