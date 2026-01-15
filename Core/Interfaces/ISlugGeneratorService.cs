namespace MyLanServer.Core.Interfaces;

/// <summary>
///     Slug 生成服务接口
/// </summary>
public interface ISlugGeneratorService
{
    /// <summary>
    ///     生成唯一的任务标识符（Slug）
    /// </summary>
    /// <returns>14 位的随机字符串</returns>
    string GenerateSlug();
}