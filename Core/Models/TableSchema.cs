using System.Text.Json.Serialization;
using MyLanServer.Core.Enums;

namespace MyLanServer.Core.Models;

/// <summary>
///     表格结构定义（用于序列化/反序列化 JSON）
/// </summary>
public class TableSchema
{
    /// <summary>
    ///     表格标题
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    ///     列定义列表
    /// </summary>
    [JsonPropertyName("columns")]
    public List<ColumnDefinition> Columns { get; set; } = new();
}

/// <summary>
///     列定义
/// </summary>
public class ColumnDefinition
{
    /// <summary>
    ///     列名（用于显示和 JSON 键名）
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     列类型（Text, Number, Date, Boolean）
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Text";

    /// <summary>
    ///     是否必填
    /// </summary>
    [JsonPropertyName("required")]
    public bool Required { get; set; } = false;

    /// <summary>
    ///     列描述（可选）
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    ///     合并模式（用于统计）
    /// </summary>
    [JsonPropertyName("mergeMode")]
    public MergeMode MergeMode { get; set; } = MergeMode.Accumulate;

    /// <summary>
    ///     分组字段（仅当 MergeMode = GroupBy 时有效）
    ///     指定按哪个字段分组统计
    /// </summary>
    [JsonPropertyName("groupByField")]
    public string? GroupByField { get; set; }
}