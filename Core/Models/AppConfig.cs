namespace MyLanServer.Core.Models;

/// <summary>
///     应用程序配置
/// </summary>
public class AppConfig
{
    /// <summary>
    ///     是否启用自动刷新任务列表
    /// </summary>
    public bool AutoRefreshEnabled { get; set; } = false;

    /// <summary>
    ///     自动刷新间隔（秒）
    /// </summary>
    public int AutoRefreshInterval { get; set; } = 10;
}