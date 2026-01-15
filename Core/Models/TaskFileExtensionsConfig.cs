namespace MyLanServer.Core.Models;

/// <summary>
///     任务文件扩展名配置
/// </summary>
public class TaskFileExtensionsConfig
{
    /// <summary>
    ///     任务ID
    /// </summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>
    ///     任务标题
    /// </summary>
    public string TaskTitle { get; set; } = string.Empty;

    /// <summary>
    ///     扩展名列表（包含预设和自定义扩展名）
    /// </summary>
    public List<FileExtensionInfo> Extensions { get; set; } = new();

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}