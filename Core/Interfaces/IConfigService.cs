using MyLanServer.Core.Models;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     配置服务接口
/// </summary>
public interface IConfigService
{
    /// <summary>
    ///     加载配置
    /// </summary>
    /// <returns>配置对象</returns>
    Task<AppConfig> LoadConfigAsync();

    /// <summary>
    ///     保存配置
    /// </summary>
    /// <param name="config">配置对象</param>
    /// <returns></returns>
    Task SaveConfigAsync(AppConfig config);
}