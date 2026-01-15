namespace MyLanServer.Core.Models;

/// <summary>
///     任务附件 DTO（用于 API 返回）
/// </summary>
public class TaskAttachmentDto
{
    /// <summary>
    ///     附件 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     原始文件名
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     显示名称（可选）
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    ///     文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     格式化后的文件大小（如 "1.5 MB"）
    /// </summary>
    public string FormattedFileSize => FormatFileSize(FileSize);

    /// <summary>
    ///     上传时间
    /// </summary>
    public DateTime UploadDate { get; set; }

    /// <summary>
    ///     排序顺序
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    ///     格式化文件大小
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}