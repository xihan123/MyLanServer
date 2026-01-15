using MyLanServer.Core.Models;
using MyLanServer.Infrastructure.Services;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     Excel 合并服务接口
/// </summary>
public interface IExcelMergeService
{
    /// <summary>
    ///     将指定文件夹内的所有 Excel 文件合并为一个（支持版本选择和多字段去重）
    /// </summary>
    /// <param name="sourceFolder">包含源 Excel 文件的文件夹路径</param>
    /// <param name="outputPath">合并后的输出文件路径</param>
    /// <param name="removeDuplicates">是否去除重复行</param>
    /// <param name="deduplicateColumns">用于去重的列名列表（如"姓名"、"联系方式"等）</param>
    /// <param name="separator">多字段组合去重时的分隔符，默认为"|"</param>
    /// <param name="templatePath">模板文件路径（可选），用于定义输出列结构</param>
    /// <param name="headerRowIndex">表头行索引（从 0 开始），默认为 0（第一行作为表头）</param>
    Task<MergeResult> MergeWithLatestVersionAsync(string sourceFolder, string outputPath,
        bool removeDuplicates, List<string> deduplicateColumns, string separator = "|",
        string? templatePath = null, int headerRowIndex = 0);

    /// <summary>
    ///     合并 JSON 文件并生成统计报表
    /// </summary>
    /// <param name="schemaPath">表格结构文件路径</param>
    /// <param name="sourceFolder">包含 JSON 文件的文件夹路径</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="fieldMergeModes">字段合并模式配置（可选）</param>
    Task<MergeResult> MergeJsonFilesWithStatisticsAsync(string schemaPath, string sourceFolder,
        string outputPath, Dictionary<string, ColumnDefinition>? fieldMergeModes = null);
}