using System.IO;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     目录辅助工具类
/// </summary>
public static class DirectoryHelper
{
    /// <summary>
    ///     确保目录存在，如果不存在则创建
    /// </summary>
    /// <param name="path">目录路径</param>
    public static void EnsureDirectoryExists(string path)
    {
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    /// <summary>
    ///     确保多个目录存在
    /// </summary>
    /// <param name="paths">目录路径数组</param>
    public static void EnsureDirectoriesExist(params string[] paths)
    {
        foreach (var path in paths) EnsureDirectoryExists(path);
    }

    /// <summary>
    ///     安全删除目录（如果存在）
    /// </summary>
    /// <param name="path">目录路径</param>
    /// <param name="recursive">是否递归删除</param>
    /// <returns>是否删除成功</returns>
    public static bool SafeDeleteDirectory(string path, bool recursive = true)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}