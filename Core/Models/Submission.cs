namespace MyLanServer.Core.Models;

/// <summary>
///     代表一次成功的文件提交记录
/// </summary>
public class Submission
{
    /// <summary>
    ///     自增主键
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    ///     关联的任务 ID
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    ///     提交人姓名
    /// </summary>
    public string SubmitterName { get; set; } = string.Empty;

    /// <summary>
    ///     联系方式 (手机号)
    /// </summary>
    public string Contact { get; set; } = string.Empty;

    /// <summary>
    ///     所属单位/部门
    /// </summary>

    public string Department { get; set; } = string.Empty;

    /// <summary>
    ///     用户上传时的原始文件名
    /// </summary>
    public string OriginalFilename { get; set; } = string.Empty;

    /// <summary>
    ///     服务器存储的实际文件名 (经过重命名处理)
    /// </summary>
    public string StoredFilename { get; set; } = string.Empty;

    /// <summary>
    ///     提交时间（UTC时间）
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    ///     提交者的 IP 地址 (用于审计)
    /// </summary>
    public string ClientIp { get; set; } = string.Empty;

    /// <summary>
    ///     附件文件路径（如果有）
    /// </summary>
    public string? AttachmentPath { get; set; }
}