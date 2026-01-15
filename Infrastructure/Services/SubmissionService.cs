using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Enums;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Services;

public class SubmissionService : ISubmissionService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IIoLockService _ioLockService;
    private readonly ILogger<SubmissionService> _logger;

    public SubmissionService(
        IIoLockService ioLockService,
        IFileSystemService fileSystemService,
        ILogger<SubmissionService> logger)
    {
        _ioLockService = ioLockService;
        _fileSystemService = fileSystemService;
        _logger = logger;
    }

    public async Task<string> ProcessSubmissionAsync(Stream fileStream, LanTask task, string submitter,
        string contact, string department, string originalFileName)
    {
        if (fileStream == null)
            throw new ArgumentNullException(nameof(fileStream));
        if (task == null)
            throw new ArgumentNullException(nameof(task));
        if (string.IsNullOrWhiteSpace(submitter))
            throw new ArgumentException("Submitter 不能为空", nameof(submitter));
        if (string.IsNullOrWhiteSpace(contact))
            throw new ArgumentException("Contact 不能为空", nameof(contact));
        if (string.IsNullOrWhiteSpace(department))
            throw new ArgumentException("Department 不能为空", nameof(department));
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("OriginalFileName 不能为空", nameof(originalFileName));

        // 1. 数据清洗
        var safeSubmitter = SanitizeFileName(submitter);
        var safeDepartment = SanitizeFileName(department);

        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(ext)) ext = ".xlsx";

        // 2. 构造文件前缀（不包含版本号和时间戳）
        var templateName = _fileSystemService.GetFileNameWithoutExtension(task.TemplatePath);
        var filePrefix = $"{templateName}-{safeSubmitter}-{safeDepartment}";

        // 确保目录存在
        _fileSystemService.EnsureDirectoryExists(task.CollectionPath);

        var finalPath = string.Empty;

        // 3. 进入临界区 (Critical Section)
        try
        {
            using (await _ioLockService.AcquireLockAsync())
            {
                if (task.VersioningMode == VersioningMode.AutoVersion)
                {
                    // 自动版本模式：计算新版本号并生成文件名
                    finalPath = GetVersionedPath(task.CollectionPath, filePrefix, ext);
                }
                else
                {
                    // 覆盖模式：使用当前时间戳，删除所有匹配"提交人+所属部门"的旧文件
                    var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    var baseFileName = $"{filePrefix}_v1-{timestamp}";
                    finalPath = Path.Combine(task.CollectionPath, baseFileName + ext);

                    var pattern = $"{filePrefix}*{ext}";
                    var existingFiles = _fileSystemService.GetFiles(task.CollectionPath, pattern);

                    foreach (var oldFile in existingFiles)
                    {
                        _logger.LogInformation("Deleting old file for overwrite: {File}", oldFile);
                        _fileSystemService.DeleteFile(oldFile);
                    }
                }

                // 4. 流式写入
                await _fileSystemService.WriteFileAsync(finalPath, fileStream);

                _logger.LogInformation("File successfully saved: {Path}", finalPath);
                return finalPath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing file to disk: {Path}", finalPath);
            throw new IOException("Failed to save file due to server IO error.", ex);
        }
    }

    private string GetVersionedPath(string folder, string prefix, string ext)
    {
        // prefix 格式：模板名-姓名-所属部门
        // 需要找到所有匹配的文件，然后选择下一个版本号

        // 查找所有匹配前缀的文件（模板名-姓名-所属部门_v*.xlsx）
        var pattern = $"{prefix}_v*{ext}";
        var existingFiles = _fileSystemService.GetFiles(folder, pattern);

        _logger.LogInformation("VersionedPath search: Prefix={Prefix}, Pattern={Pattern}, FoundFiles={Count}",
            prefix, pattern, existingFiles.Length);

        // 找出最大的版本号
        var maxVersion = 0;
        var escapedPrefix = Regex.Escape(prefix);

        // 匹配两种格式：
        // 1. 新格式：prefix_v1-20260103-164530 (使用横杠)
        // 2. 旧格式：prefix_v1-20260103-164530 (使用横杠)
        var versionPattern = $@"^{escapedPrefix}_v(\d+)-\d{{8}}-\d{{6}}$";

        foreach (var file in existingFiles)
        {
            var fileName = _fileSystemService.GetFileNameWithoutExtension(file);
            _logger.LogDebug("Checking file: {FileName}", fileName);

            var versionMatch = Regex.Match(fileName, versionPattern);
            if (versionMatch.Success)
            {
                if (int.TryParse(versionMatch.Groups[1].Value, out var version))
                    if (version > maxVersion)
                    {
                        maxVersion = version;
                        _logger.LogInformation("Found version {Version} in file: {FileName}", version, fileName);
                    }
            }
            else
            {
                _logger.LogDebug("File {FileName} does not match pattern {Pattern}", fileName, versionPattern);
            }
        }

        // 生成新版本号
        var newVersion = maxVersion + 1;
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        var finalPath = Path.Combine(folder, $"{prefix}_v{newVersion}-{timestamp}{ext}");
        _logger.LogInformation("Generated new path: {Path}", finalPath);

        return finalPath;
    }

    private string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "Unknown";

        // 移除非法字符
        var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        var invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
        var cleaned = Regex.Replace(input, invalidRegStr, "_");

        // 进一步限制：只允许中文、字母、数字、下划线、横杠
        cleaned = Regex.Replace(cleaned, @"[^\u4e00-\u9fa5a-zA-Z0-9\-_]", "");

        return cleaned;
    }
}