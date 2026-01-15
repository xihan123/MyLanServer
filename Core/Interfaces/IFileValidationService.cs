using System.IO;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     文件验证服务接口
///     用于验证文件内容（魔数）和扩展名
/// </summary>
public interface IFileValidationService
{
    /// <summary>
    ///     验证文件内容是否与扩展名匹配
    /// </summary>
    /// <param name="fileStream">文件流</param>
    /// <param name="extension">文件扩展名（包含点，如 .xlsx）</param>
    /// <returns>是否有效</returns>
    Task<bool> ValidateFileContentAsync(Stream fileStream, string extension);

    /// <summary>
    ///     验证文件扩展名是否允许
    /// </summary>
    /// <param name="extension">文件扩展名（包含点）</param>
    /// <param name="allowedExtensions">允许的扩展名列表</param>
    /// <returns>是否允许</returns>
    bool IsExtensionAllowed(string extension, string[] allowedExtensions);
}