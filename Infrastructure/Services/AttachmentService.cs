using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Enums;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;
using MyLanServer.Infrastructure.Security;

namespace MyLanServer.Infrastructure.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IIoLockService _ioLockService;
    private readonly ILogger<AttachmentService> _logger;

    public AttachmentService(
        IIoLockService ioLockService,
        IFileSystemService fileSystemService,
        ILogger<AttachmentService> logger)
    {
        _ioLockService = ioLockService;
        _fileSystemService = fileSystemService;
        _logger = logger;
    }

    public async Task<string> ProcessAttachmentAsync(Stream fileStream, LanTask task,
        string submitterName, string contact, string department, string originalFileName)
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));
        if (task == null)
            throw new ArgumentNullException(nameof(task));
        if (string.IsNullOrWhiteSpace(submitterName))
            throw new ArgumentException("SubmitterName 不能为空", nameof(submitterName));
        if (string.IsNullOrWhiteSpace(department))
            throw new ArgumentException("Department 不能为空", nameof(department));
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("OriginalFileName 不能为空", nameof(originalFileName));

        // 1. 验证任务配置
        if (!task.AllowAttachmentUpload) throw new InvalidOperationException("该任务不允许上传附件");

        // 2. 构建文件夹名称（使用固定规则：提交人-所属部门）
        var folderName = BuildFolderName(submitterName, department);
        var attachmentFolder = Path.Combine(task.CollectionPath, folderName);

        // 确保文件夹存在
        _fileSystemService.EnsureDirectoryExists(attachmentFolder);
        _logger.LogInformation("Attachment folder ensured: {Folder}", attachmentFolder);

        // 4. 构建文件名（应用重名规则）
        var finalPath = await GenerateFilePathAsync(attachmentFolder, originalFileName, task.VersioningMode);

        // 5. 保存文件
        using (await _ioLockService.AcquireLockAsync())
        {
            try
            {
                await _fileSystemService.WriteFileAsync(finalPath, fileStream);
                _logger.LogInformation("Attachment saved: {Path}", finalPath);
                return finalPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save attachment: {Path}", finalPath);
                throw new IOException("保存附件失败", ex);
            }
        }
    }

    private string BuildFolderName(string submitterName, string department)
    {
        var parts = new List<string>
        {
            SecurityHelper.SanitizePathSegment(submitterName),
            SecurityHelper.SanitizePathSegment(department)
        };

        return string.Join("-", parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    private async Task<string> GenerateFilePathAsync(string folder, string originalFileName,
        VersioningMode versioningMode)
    {
        var safeOriginalName = SecurityHelper.SanitizePathSegment(originalFileName);
        var ext = _fileSystemService.GetExtension(safeOriginalName);
        var fileNameWithoutExt = _fileSystemService.GetFileNameWithoutExtension(safeOriginalName);

        // 保持原始文件的扩展名，不强制修改

        using (await _ioLockService.AcquireLockAsync())
        {
            if (versioningMode == VersioningMode.AutoVersion)
            {
                // 自动版本模式
                var pattern = $"{fileNameWithoutExt}_v*{ext}";
                var existingFiles = _fileSystemService.GetFiles(folder, pattern);

                var maxVersion = 0;
                var escapedName = Regex.Escape(fileNameWithoutExt);
                var versionPattern = $@"^{escapedName}_v(\d+)$";

                foreach (var file in existingFiles)
                {
                    var fileName = _fileSystemService.GetFileNameWithoutExtension(file);
                    var match = Regex.Match(fileName, versionPattern);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var version))
                        if (version > maxVersion)
                            maxVersion = version;
                }

                var newVersion = maxVersion + 1;
                return Path.Combine(folder, $"{fileNameWithoutExt}_v{newVersion}{ext}");
            }
            else
            {
                // 覆盖模式：删除所有同名文件
                var pattern = $"{fileNameWithoutExt}*{ext}";
                var existingFiles = _fileSystemService.GetFiles(folder, pattern);

                foreach (var oldFile in existingFiles)
                {
                    _logger.LogInformation("Deleting old attachment for overwrite: {File}", oldFile);
                    _fileSystemService.DeleteFile(oldFile);
                }

                return Path.Combine(folder, $"{fileNameWithoutExt}_v1{ext}");
            }
        }
    }
}