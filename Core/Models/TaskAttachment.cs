using CommunityToolkit.Mvvm.ComponentModel;

namespace MyLanServer.Core.Models;

/// <summary>
///     任务附件模型（管理员提供的附件）
/// </summary>
public partial class TaskAttachment : ObservableObject
{
    /// <summary>
    ///     附件描述（可选）
    /// </summary>
    [ObservableProperty] private string? _description;

    /// <summary>
    ///     显示名称（可选，默认使用文件名）
    /// </summary>
    [ObservableProperty] private string? _displayName;

    /// <summary>
    ///     原始文件名
    /// </summary>
    [ObservableProperty] private string _fileName = string.Empty;

    /// <summary>
    ///     文件存储的绝对路径
    /// </summary>
    [ObservableProperty] private string _filePath = string.Empty;

    /// <summary>
    ///     文件大小（字节）
    /// </summary>
    [ObservableProperty] private long _fileSize;

    /// <summary>
    ///     附件 ID
    /// </summary>
    [ObservableProperty] private int _id;

    /// <summary>
    ///     排序顺序（越小越靠前）
    /// </summary>
    [ObservableProperty] private int _sortOrder;

    /// <summary>
    ///     关联的任务 ID
    /// </summary>
    [ObservableProperty] private string _taskId = string.Empty;

    /// <summary>
    ///     上传时间
    /// </summary>
    [ObservableProperty] private DateTime _uploadDate = DateTime.Now;
}