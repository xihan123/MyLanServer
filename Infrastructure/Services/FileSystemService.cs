using System.IO;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     文件系统服务实现
///     封装 System.IO 操作，便于测试和替换
/// </summary>
public class FileSystemService : IFileSystemService
{
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(ILogger<FileSystemService> logger)
    {
        _logger = logger;
    }

    public void EnsureDirectoryExists(string path)
    {
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _logger.LogDebug("Created directory: {Path}", path);
        }
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public async Task WriteFileAsync(string path, Stream content, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory != null) EnsureDirectoryExists(directory);

        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await content.CopyToAsync(fileStream, cancellationToken);
        _logger.LogDebug("Wrote file: {Path}", path);
    }

    public Stream OpenRead(string path)
    {
        return File.OpenRead(path);
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogDebug("Deleted file: {Path}", path);
        }
    }

    public async Task CopyFileAsync(string sourcePath, string destPath, bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        var destDirectory = Path.GetDirectoryName(destPath);
        if (destDirectory != null) EnsureDirectoryExists(destDirectory);

        using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write);
        await sourceStream.CopyToAsync(destStream, cancellationToken);
        _logger.LogDebug("Copied file from {Source} to {Dest}", sourcePath, destPath);
    }

    public string[] GetFiles(string path, string searchPattern = "*")
    {
        return Directory.GetFiles(path, searchPattern);
    }

    public string GetFileName(string path)
    {
        return Path.GetFileName(path);
    }

    public string GetExtension(string path)
    {
        return Path.GetExtension(path);
    }

    public string GetFileNameWithoutExtension(string path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }
}