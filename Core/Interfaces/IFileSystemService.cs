using System.IO;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     文件系统服务接口
///     抽象文件系统操作，便于测试和替换
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    ///     确保目录存在
    /// </summary>
    /// <param name="path">目录路径</param>
    void EnsureDirectoryExists(string path);

    /// <summary>
    ///     检查文件是否存在
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <returns>是否存在</returns>
    bool FileExists(string path);

    /// <summary>
    ///     检查目录是否存在
    /// </summary>
    /// <param name="path">目录路径</param>
    /// <returns>是否存在</returns>
    bool DirectoryExists(string path);

    /// <summary>
    ///     写入文件流
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="content">文件内容流</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteFileAsync(string path, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    ///     读取文件流
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <returns>文件流</returns>
    Stream OpenRead(string path);

    /// <summary>
    ///     删除文件
    /// </summary>
    /// <param name="path">文件路径</param>
    void DeleteFile(string path);

    /// <summary>
    ///     复制文件
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="destPath">目标文件路径</param>
    /// <param name="overwrite">是否覆盖</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     获取目录中的文件
    /// </summary>
    /// <param name="path">目录路径</param>
    /// <param name="searchPattern">搜索模式</param>
    /// <returns>文件路径数组</returns>
    string[] GetFiles(string path, string searchPattern = "*");

    /// <summary>
    ///     获取文件名
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <returns>文件名</returns>
    string GetFileName(string path);

    /// <summary>
    ///     获取文件扩展名
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <returns>扩展名（包含点）</returns>
    string GetExtension(string path);

    /// <summary>
    ///     获取不带扩展名的文件名
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <returns>不带扩展名的文件名</returns>
    string GetFileNameWithoutExtension(string path);
}