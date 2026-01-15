using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     使用 PBKDF2 算法的密码哈希服务实现
/// </summary>
public class PasswordHashService : IPasswordHashService
{
    // PBKDF2 参数
    private const int SaltSize = 16; // 128 bit
    private const int HashSize = 32; // 256 bit
    private const int Iterations = 10000; // 迭代次数，可根据需要调整
    private readonly ILogger<PasswordHashService> _logger;

    public PasswordHashService(ILogger<PasswordHashService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     对密码进行哈希处理
    /// </summary>
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("密码不能为空", nameof(password));

        // 生成随机盐值
        var salt = new byte[SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // 使用 PBKDF2 生成哈希
        var hash = Pbkdf2(password, salt, Iterations, HashSize);

        // 将盐值和哈希值组合为字符串
        // 格式: iterations:salt(base64):hash(base64)
        var parts = new[]
        {
            Iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash)
        };

        return string.Join(":", parts);
    }

    /// <summary>
    ///     验证密码是否匹配
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash)) return false;

        try
        {
            // 解析哈希字符串
            var parts = hash.Split(':');
            if (parts.Length != 3)
            {
                _logger.LogWarning("Invalid hash format");
                return false;
            }

            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var storedHash = Convert.FromBase64String(parts[2]);

            // 使用相同的参数计算密码哈希
            var computedHash = Pbkdf2(password, salt, iterations, storedHash.Length);

            // 比较哈希值（使用固定时间比较，防止时序攻击）
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password verification failed");
            return false;
        }
    }

    /// <summary>
    ///     PBKDF2 哈希计算
    /// </summary>
    private static byte[] Pbkdf2(string password, byte[] salt, int iterations, int outputBytes)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(outputBytes);
    }
}