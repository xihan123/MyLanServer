using MyLanServer.Core.Models;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     快捷过期时间选项服务接口
/// </summary>
public interface IExpiryQuickOptionsService
{
    /// <summary>
    ///     加载配置
    /// </summary>
    /// <returns>配置对象</returns>
    Task<ExpiryQuickOptionsConfig> LoadConfigAsync();

    /// <summary>
    ///     获取当前时间选项列表
    /// </summary>
    /// <returns>时间选项列表</returns>
    List<ExpiryQuickOption> GetOptions();

    /// <summary>
    ///     配置变更事件（支持热更新）
    /// </summary>
    event EventHandler? OptionsChanged;
}