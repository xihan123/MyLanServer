namespace MyLanServer.Core.Interfaces;

/// <summary>
///     密码哈希服务接口
///     使用 BCrypt 算法进行密码哈希和验证
/// </summary>
public interface IPasswordHashService
{
    /// <summary>
    ///     对密码进行哈希处理（使用 BCrypt 算法）
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>哈希后的密码字符串</returns>
    string HashPassword(string password);

    /// <summary>
    ///     验证密码是否匹配（使用 BCrypt 算法）
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <param name="hash">哈希值</param>
    /// <returns>密码是否匹配</returns>
    bool VerifyPassword(string password, string hash);
}