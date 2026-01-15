using System.Security.Cryptography;
using MyLanServer.Core.Interfaces;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     Slug 生成服务实现
///     使用加密安全的随机数生成器生成 14 位的随机字符串
/// </summary>
public class SlugGeneratorService : ISlugGeneratorService
{
    // 使用字母数字字符集，移除易混淆的字符（I, O, 0, 1）
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int Size = 14;

    /// <summary>
    ///     生成唯一的任务标识符（Slug）
    /// </summary>
    /// <returns>14 位的随机字符串</returns>
    public string GenerateSlug()
    {
        var result = new char[Size];
        var alphabetLength = Alphabet.Length;

        for (var i = 0; i < Size; i++) result[i] = Alphabet[RandomNumberGenerator.GetInt32(alphabetLength)];

        return new string(result);
    }
}