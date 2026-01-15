using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using MyLanServer.Core.Enums;
using MyLanServer.Infrastructure.Security;

namespace MyLanServer.Core.Models;

/// <summary>
///     代表一个文件收集任务
/// </summary>
public partial class LanTask : ObservableObject
{
    /// <summary>
    ///     是否允许上传附件（仅对 DataCollection 任务有效）
    /// </summary>
    [ObservableProperty] private bool _allowAttachmentUpload;

    /// <summary>
    ///     附件下载提示（任务级别，对所有附件生效）
    /// </summary>
    [ObservableProperty] private string? _attachmentDownloadDescription;

    /// <summary>
    ///     任务创建时间
    /// </summary>
    [ObservableProperty] private DateTime _createdAt = DateTime.UtcNow;

    /// <summary>
    ///     当前已提交数量
    /// </summary>
    [ObservableProperty] private int _currentCount;

    /// <summary>
    ///     任务描述
    /// </summary>
    [ObservableProperty] private string? _description;

    /// <summary>
    ///     下载次数统计
    /// </summary>
    [ObservableProperty] private int _downloadsCount;

    /// <summary>
    ///     任务过期时间 (null 表示不过期)
    /// </summary>
    [ObservableProperty] private DateTime? _expiryDate;

    /// <summary>
    ///     任务唯一标识 (UUID)
    /// </summary>
    [ObservableProperty] private string _id = string.Empty;

    /// <summary>
    ///     任务是否处于激活状态
    /// </summary>
    [ObservableProperty] private bool _isActive = true;

    /// <summary>
    ///     是否为一次性下载链接（下载一次后失效）
    /// </summary>
    [ObservableProperty] private bool _isOneTimeLink;

    /// <summary>
    ///     最大允许提交数量 (0 表示不限制)
    /// </summary>
    [ObservableProperty] private int _maxLimit;

    /// <summary>
    ///     访问密码的哈希值 (可选)
    /// </summary>
    [ObservableProperty] private string? _passwordHash;

    /// <summary>
    ///     是否在接口中公开返回描述文本
    /// </summary>
    [ObservableProperty] private bool _showDescriptionInApi;

    /// <summary>
    ///     URL 路径标识 (8位随机字符串，用于生成访问链接)
    /// </summary>
    [ObservableProperty] private string _slug = string.Empty;

    /// <summary>
    ///     收集到的文件存储目录
    /// </summary>
    [ObservableProperty] private string _targetFolder = string.Empty;

    /// <summary>
    ///     任务类型（文件收集或数据收集）
    /// </summary>
    [ObservableProperty] private TaskType _taskType = TaskType.FileCollection;

    /// <summary>
    ///     Excel 模板文件的本地绝对路径
    /// </summary>
    [ObservableProperty] private string _templatePath = string.Empty;

    /// <summary>
    ///     任务标题（用于文件夹命名和主界面显示）
    /// </summary>
    [ObservableProperty] private string _title = string.Empty;

    /// <summary>
    ///     文件重名处理策略
    /// </summary>
    [ObservableProperty] private VersioningMode _versioningMode = VersioningMode.AutoVersion;

    #region 计算属性

    /// <summary>
    ///     配置目录路径（config/[任务Title]/）
    /// </summary>
    public string ConfigPath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "config",
        SecurityHelper.SanitizePathSegment(Title)
    );

    /// <summary>
    ///     模板文件完整路径
    /// </summary>
    public string TemplateFilePath => Path.Combine(
        ConfigPath,
        Path.GetFileName(TemplatePath)
    );

    /// <summary>
    ///     Schema文件完整路径
    /// </summary>
    public string SchemaFilePath => Path.Combine(
        ConfigPath,
        "schema.json"
    );

    /// <summary>
    ///     收集目录路径（收集/[任务Title]/[任务Slug]/）
    /// </summary>
    public string CollectionRootPath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "收集",
        SecurityHelper.SanitizePathSegment(Title),
        SecurityHelper.SanitizePathSegment(Slug)
    );

    /// <summary>
    ///     文件收集目录路径
    /// </summary>
    public string FileCollectionPath => Path.Combine(
        CollectionRootPath,
        "文件收集"
    );

    /// <summary>
    ///     在线填表目录路径
    /// </summary>
    public string DataCollectionPath => Path.Combine(
        CollectionRootPath,
        "在线填表"
    );

    /// <summary>
    ///     当前任务的收集目录（根据TaskType返回）
    /// </summary>
    public string CollectionPath => TaskType == TaskType.FileCollection
        ? FileCollectionPath
        : DataCollectionPath;

    /// <summary>
    ///     附件目录路径（收集/[任务Title]/[任务ID]/[文件收集|在线填表]/attachments/）
    /// </summary>
    public string AttachmentsPath => Path.Combine(
        CollectionPath,
        "attachments"
    );

    #endregion
}