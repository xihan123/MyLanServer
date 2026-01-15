using CommunityToolkit.Mvvm.ComponentModel;

namespace MyLanServer.Core.Models;

/// <summary>
///     文件扩展名信息
/// </summary>
public class FileExtensionInfo : ObservableObject
{
    private DateTime _addedAt = DateTime.UtcNow;
    private string _category = string.Empty;
    private string _displayName = string.Empty;
    private string _extension = string.Empty;
    private bool _isPreset;
    private bool _isSelected;

    /// <summary>
    ///     扩展名（如 ".pdf"）
    /// </summary>
    public string Extension
    {
        get => _extension;
        set => SetProperty(ref _extension, value);
    }

    /// <summary>
    ///     显示名称（如 "PDF 文档"）
    /// </summary>
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    /// <summary>
    ///     分类（如 "文档"、"图片"、"压缩包"）
    /// </summary>
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    /// <summary>
    ///     是否为系统预设
    /// </summary>
    public bool IsPreset
    {
        get => _isPreset;
        set => SetProperty(ref _isPreset, value);
    }

    /// <summary>
    ///     添加时间
    /// </summary>
    public DateTime AddedAt
    {
        get => _addedAt;
        set => SetProperty(ref _addedAt, value);
    }

    /// <summary>
    ///     是否被选中（用于UI绑定）
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}