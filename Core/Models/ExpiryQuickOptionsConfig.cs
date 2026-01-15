using System.Text.Json.Serialization;

namespace MyLanServer.Core.Models;

/// <summary>
///     快捷过期时间选项配置
/// </summary>
public class ExpiryQuickOptionsConfig
{
    /// <summary>
    ///     时间选项列表
    /// </summary>
    public List<ExpiryQuickOption> Options { get; set; } = new();

    /// <summary>
    ///     最后更新时间
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     快捷过期时间选项
/// </summary>
public class ExpiryQuickOption
{
    /// <summary>
    ///     显示名称（如："1小时"、"1天"）
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     小时数（用于小于1天的时间）
    /// </summary>
    public int? Hours { get; set; }

    /// <summary>
    ///     天数（用于大于等于1天的时间）
    /// </summary>
    public int? Days { get; set; }

    /// <summary>
    ///     时间跨度（程序内部使用，自动计算，不序列化到配置文件）
    /// </summary>
    [JsonIgnore]
    public TimeSpan TimeSpan
    {
        get => TimeSpan.FromDays(Days ?? 0) + TimeSpan.FromHours(Hours ?? 0);
        set
        {
            Days = value.Days;
            Hours = value.Hours;
        }
    }

    /// <summary>
    ///     命令参数（用于右键菜单，如 "30d"、"2M"）
    /// </summary>
    [JsonIgnore]
    public string CommandParameter
    {
        get
        {
            if (Days.HasValue)
                // 如果是30天、60天、90天等，使用 "Xd" 格式
                return $"{Days.Value}d";

            if (Hours.HasValue) return $"{Hours.Value}h";

            return string.Empty;
        }
    }
}