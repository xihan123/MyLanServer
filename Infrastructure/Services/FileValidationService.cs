using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     文件验证服务实现
///     通过检查文件魔数（Magic Numbers）验证文件真实类型
/// </summary>
public class FileValidationService : IFileValidationService
{
    // 常见文件类型的魔数（文件头）
    private static readonly Dictionary<string, byte[]> FileMagicNumbers = new()
    {
        // Office 2007+ (ZIP 格式)
        { ".xlsx", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        { ".xls", new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },
        { ".docx", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        { ".doc", new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },
        { ".pptx", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        { ".ppt", new byte[] { 0xD0, 0xCF, 0x11, 0xE0 } },

        // PDF
        { ".pdf", Encoding.ASCII.GetBytes("%PDF") },

        // 图片
        { ".jpg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { ".jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },
        { ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        { ".gif", new byte[] { 0x47, 0x49, 0x46, 0x38 } },
        { ".bmp", new byte[] { 0x42, 0x4D } },

        // 文本
        { ".txt", Array.Empty<byte>() }, // 文本文件没有固定的魔数

        // 压缩文件
        { ".zip", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        { ".rar", new byte[] { 0x52, 0x61, 0x72, 0x21 } },
        { ".7z", new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C } }
    };

    private readonly ILogger<FileValidationService> _logger;

    public FileValidationService(ILogger<FileValidationService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ValidateFileContentAsync(Stream fileStream, string extension)
    {
        if (fileStream == null)
            return false;

        // 标准化扩展名
        var normalizedExt = extension.ToLowerInvariant();

        // 文本文件没有魔数，直接返回 true
        if (normalizedExt == ".txt")
            return true;

        // 检查是否有该扩展名的魔数定义
        if (!FileMagicNumbers.TryGetValue(normalizedExt, out var expectedMagic))
        {
            _logger.LogWarning("No magic number defined for extension: {Extension}", extension);
            return true; // 如果没有定义，则跳过验证
        }

        // 如果魔数为空，跳过验证
        if (expectedMagic.Length == 0)
            return true;

        try
        {
            // 保存当前位置
            var originalPosition = fileStream.Position;

            // 读取文件头
            var buffer = new byte[expectedMagic.Length];
            var bytesRead = await fileStream.ReadAsync(buffer, 0, expectedMagic.Length);

            // 恢复流位置
            fileStream.Position = originalPosition;

            // 比较魔数
            if (bytesRead < expectedMagic.Length)
            {
                _logger.LogWarning("File too short to validate: {Extension}", extension);
                return false;
            }

            var isValid = buffer.Take(expectedMagic.Length).SequenceEqual(expectedMagic);

            if (!isValid)
                _logger.LogWarning(
                    "File content does not match extension: {Extension}. Expected magic: {Expected}, Actual: {Actual}",
                    extension,
                    BitConverter.ToString(expectedMagic),
                    BitConverter.ToString(buffer.Take(expectedMagic.Length).ToArray()));

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating file content for extension: {Extension}", extension);
            return false;
        }
    }

    public bool IsExtensionAllowed(string extension, string[] allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        var normalizedExt = extension.ToLowerInvariant();
        return allowedExtensions.Any(e => e.Equals(normalizedExt, StringComparison.OrdinalIgnoreCase));
    }
}