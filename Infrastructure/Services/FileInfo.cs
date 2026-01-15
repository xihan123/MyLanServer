namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     Excel文件信息类，用于版本管理
/// </summary>
public class ExcelFileInfo
{
    /// <summary>
    ///     文件完整路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    ///     模板名
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    ///     姓名（提交人）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     联系方式
    /// </summary>
    public string Contact { get; set; } = string.Empty;

    /// <summary>
    ///     版本号
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    ///     时间戳（用于排序）
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    ///     唯一标识（姓名+联系方式）
    /// </summary>
    public string UniqueId => $"{Name}|{Contact}";
}