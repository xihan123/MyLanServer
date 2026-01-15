namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     合并结果统计信息
/// </summary>
public class MergeResult
{
    /// <summary>
    ///     总文件数
    /// </summary>
    public int TotalFiles { get; set; }

    /// <summary>
    ///     被过滤的旧版本文件数
    /// </summary>
    public int FilteredFiles { get; set; }

    /// <summary>
    ///     实际合并的文件数
    /// </summary>
    public int MergedFiles { get; set; }

    /// <summary>
    ///     去重前总记录数
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    ///     去重后保留的记录数
    /// </summary>
    public int DeduplicatedRecords { get; set; }

    /// <summary>
    ///     被去重移除的记录数
    /// </summary>
    public int DuplicatedCount { get; set; }

    /// <summary>
    ///     输出文件路径
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    ///     是否成功
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    ///     错误消息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     获取结果描述
    /// </summary>
    public string GetSummary()
    {
        if (!IsSuccess)
            return $"合并失败: {ErrorMessage}";

        if (DuplicatedCount > 0)
            return $"合并成功！总文件: {TotalFiles}, 过滤旧版本: {FilteredFiles}, " +
                   $"实际合并: {MergedFiles}, 总记录: {TotalRecords}, 去重保留: {DeduplicatedRecords}, " +
                   $"移除重复: {DuplicatedCount}";

        return $"合并成功！总文件: {TotalFiles}, 过滤旧版本: {FilteredFiles}, " +
               $"实际合并: {MergedFiles}, 总记录: {DeduplicatedRecords}";
    }
}