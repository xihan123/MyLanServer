using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     文件扩展名服务实现
/// </summary>
public class FileExtensionService : IFileExtensionService
{
    // 默认允许的扩展名列表
    private static readonly List<string> DefaultAllowedExtensions = new()
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp",
        ".zip", ".rar", ".7z"
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<FileExtensionService> _logger;

    public FileExtensionService(ILogger<FileExtensionService> logger)
    {
        _logger = logger;
    }

    public async Task<TaskFileExtensionsConfig> GetTaskExtensionsAsync(string taskId, string taskTitle)
    {
        var taskConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config",
            taskTitle,
            "task_extensions.json");

        _logger.LogInformation("FileExtensionService - 准备加载扩展名配置");
        _logger.LogInformation("  文件路径: {Path}", taskConfigPath);
        _logger.LogInformation("  任务ID: {TaskId}", taskId);
        _logger.LogInformation("  任务标题: {TaskTitle}", taskTitle);

        await _lock.WaitAsync();
        try
        {
            _logger.LogInformation("  检查文件是否存在: {Exists}", File.Exists(taskConfigPath));

            if (File.Exists(taskConfigPath))
            {
                _logger.LogInformation("  文件存在，读取内容");
                var json = await File.ReadAllTextAsync(taskConfigPath);
                _logger.LogInformation("  JSON 内容长度: {Length}", json.Length);

                var config = JsonSerializer.Deserialize<TaskFileExtensionsConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null)
                {
                    _logger.LogInformation("  反序列化成功");
                    _logger.LogInformation("  加载到的扩展名数量: {Count}", config.Extensions.Count);
                    _logger.LogInformation("FileExtensionService - 扩展名配置加载成功");
                    return config;
                }
                else
                {
                    _logger.LogWarning("  反序列化失败，返回空配置");
                }
            }
            else
            {
                _logger.LogInformation("  文件不存在，返回空配置");
            }

            _logger.LogInformation("FileExtensionService - 返回空配置");
            return new TaskFileExtensionsConfig
            {
                TaskId = taskId,
                TaskTitle = taskTitle,
                Extensions = new List<FileExtensionInfo>(),
                LastUpdated = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FileExtensionService - 加载扩展名配置失败");
            _logger.LogError("  文件路径: {Path}", taskConfigPath);
            _logger.LogError("  错误信息: {Message}", ex.Message);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveTaskExtensionsAsync(TaskFileExtensionsConfig config)
    {
        var taskConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config",
            config.TaskTitle,
            "task_extensions.json");

        _logger.LogInformation("FileExtensionService - 准备保存扩展名配置");
        _logger.LogInformation("  文件路径: {Path}", taskConfigPath);
        _logger.LogInformation("  任务ID: {TaskId}", config.TaskId);
        _logger.LogInformation("  任务标题: {TaskTitle}", config.TaskTitle);
        _logger.LogInformation("  扩展名数量: {Count}", config.Extensions.Count);

        await _lock.WaitAsync();
        try
        {
            var configDir = Path.GetDirectoryName(taskConfigPath);
            _logger.LogInformation("  配置目录: {Directory}", configDir);
            _logger.LogInformation("  目录是否存在: {Exists}", Directory.Exists(configDir));

            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                _logger.LogInformation("  创建配置目录");
                Directory.CreateDirectory(configDir);
            }

            config.LastUpdated = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            _logger.LogInformation("  JSON 内容长度: {Length}", json.Length);
            _logger.LogInformation("  写入文件");
            await File.WriteAllTextAsync(taskConfigPath, json);

            _logger.LogInformation("  文件写入成功");
            _logger.LogInformation("FileExtensionService - 扩展名配置保存成功: {Path}", taskConfigPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FileExtensionService - 保存扩展名配置失败");
            _logger.LogError("  文件路径: {Path}", taskConfigPath);
            _logger.LogError("  错误信息: {Message}", ex.Message);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<string>> GetAllowedExtensionsAsync(string taskId, string taskTitle,
        bool allowAttachmentUpload)
    {
        _logger.LogInformation(
            "GetAllowedExtensionsAsync - 任务ID: {TaskId}, 任务标题: {TaskTitle}, 允许上传附件: {AllowAttachmentUpload}",
            taskId, taskTitle, allowAttachmentUpload);

        // 如果不允许上传附件，返回空列表
        if (!allowAttachmentUpload)
        {
            _logger.LogInformation("任务不允许上传附件，返回空列表");
            return new List<string>();
        }

        // 尝试加载任务配置
        var taskConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config",
            taskTitle,
            "task_extensions.json");

        await _lock.WaitAsync();
        try
        {
            if (File.Exists(taskConfigPath))
            {
                _logger.LogInformation("配置文件存在，加载自定义扩展名配置");
                var json = await File.ReadAllTextAsync(taskConfigPath);
                var config = JsonSerializer.Deserialize<TaskFileExtensionsConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null && config.Extensions.Count > 0)
                {
                    var allowedExtensions = config.Extensions
                        .Where(e => e.IsSelected)
                        .Select(e => e.Extension)
                        .ToList();

                    _logger.LogInformation("使用自定义扩展名配置，数量: {Count}", allowedExtensions.Count);
                    return allowedExtensions;
                }
            }

            // 配置文件不存在或为空，使用默认列表
            _logger.LogInformation("配置文件不存在或为空，使用默认扩展名列表，数量: {Count}", DefaultAllowedExtensions.Count);
            return new List<string>(DefaultAllowedExtensions);
        }
        finally
        {
            _lock.Release();
        }
    }

    public bool IsExtensionAllowed(string extension, List<string> allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;

        return allowedExtensions.Any(e =>
            e.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    public async Task MigrateTaskExtensionsAsync(string oldTaskTitle, string newTaskTitle)
    {
        await _lock.WaitAsync();
        try
        {
            var oldConfigPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "config",
                oldTaskTitle,
                "task_extensions.json");

            var newConfigPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "config",
                newTaskTitle,
                "task_extensions.json");

            // 如果旧配置文件存在，移动到新位置
            if (File.Exists(oldConfigPath))
            {
                // 确保新目录存在
                var newConfigDir = Path.GetDirectoryName(newConfigPath);
                if (!string.IsNullOrEmpty(newConfigDir) && !Directory.Exists(newConfigDir))
                    Directory.CreateDirectory(newConfigDir);

                // 移动文件
                File.Move(oldConfigPath, newConfigPath, true);

                // 更新配置文件中的任务标题
                var json = await File.ReadAllTextAsync(newConfigPath);
                var config = JsonSerializer.Deserialize<TaskFileExtensionsConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null)
                {
                    config.TaskTitle = newTaskTitle;
                    config.LastUpdated = DateTime.UtcNow;
                    var updatedJson = JsonSerializer.Serialize(config, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    await File.WriteAllTextAsync(newConfigPath, updatedJson);
                }

                _logger.LogInformation("Migrated task extensions from {OldTitle} to {NewTitle}", oldTaskTitle,
                    newTaskTitle);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}