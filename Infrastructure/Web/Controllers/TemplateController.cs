using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Web.Controllers;

/// <summary>
///     模板下载控制器
///     支持一次性下载链接（下载后自动失效）
/// </summary>
[Route("api")]
[ApiController]
public class TemplateController : ControllerBase
{
    private readonly IFileExtensionService _fileExtensionService;
    private readonly ILogger<TemplateController> _logger;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ITaskAttachmentService _taskAttachmentService;
    private readonly ITaskRepository _taskRepository;

    public TemplateController(
        ITaskRepository taskRepository,
        IPasswordHashService passwordHashService,
        ITaskAttachmentService taskAttachmentService,
        IFileExtensionService fileExtensionService,
        ILogger<TemplateController> logger)
    {
        _taskRepository = taskRepository;
        _passwordHashService = passwordHashService;
        _taskAttachmentService = taskAttachmentService;
        _fileExtensionService = fileExtensionService;
        _logger = logger;
    }

    /// <summary>
    ///     下载 Excel 模板
    ///     如果任务设置为一次性下载链接，下载后链接将失效
    /// </summary>
    [HttpGet("template/{slug}")]
    public async Task<IActionResult> DownloadTemplate(string slug)
    {
        try
        {
            // 从中间件获取已验证的任务
            var task = HttpContext.Items["Task"] as LanTask;
            if (task == null) return NotFound(new { error = "任务验证失败" });

            // 验证密码（如果任务设置了密码）
            var password = Request.Headers["X-Password"].FirstOrDefault();
            if (!string.IsNullOrEmpty(task.PasswordHash))
                if (string.IsNullOrEmpty(password) ||
                    !_passwordHashService.VerifyPassword(password, task.PasswordHash))
                {
                    _logger.LogWarning(
                        "Password verification failed for task {Slug} from IP {IP}",
                        slug,
                        HttpContext.Connection.RemoteIpAddress?.ToString());
                    return Unauthorized(new { error = "密码错误，请重新输入" });
                }

            // 增加下载计数（对于一次性下载链接）
            if (task.IsOneTimeLink)
            {
                await _taskRepository.IncrementDownloadsCountAsync(task.Id);
                _logger.LogInformation("Template downloaded for task {Slug} (One-time link used)", slug);
            }

            // 检查模板文件是否存在并验证路径安全性
            if (!System.IO.File.Exists(task.TemplatePath))
            {
                _logger.LogError("Template file not found: {Path}", task.TemplatePath);
                return NotFound(new { error = "模板文件不存在，请联系管理员" });
            }

            // 验证路径安全性，防止路径遍历攻击
            try
            {
                var fullPath = Path.GetFullPath(task.TemplatePath);
                var basePath = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);

                // 检查路径是否在基目录下
                if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Invalid template path (outside base directory): {Path}", task.TemplatePath);
                    return BadRequest(new { error = "非法的文件路径" });
                }

                // 检查文件扩展名是否为 .xlsx
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                if (extension != ".xlsx")
                {
                    _logger.LogWarning("Invalid template file extension: {Extension}", extension);
                    return BadRequest(new { error = "模板文件格式不正确" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Path validation failed for template: {Path}", task.TemplatePath);
                return BadRequest(new { error = "文件路径验证失败" });
            }

            // 记录下载日志（下载计数已在中间件中处理）
            _logger.LogInformation(
                "Template downloaded: {Slug} by {IP}",
                slug,
                HttpContext.Connection.RemoteIpAddress?.ToString());

            // 返回文件流
            var fileName = Path.GetFileName(task.TemplatePath);
            var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            // 使用符合 RFC 3986 标准的编码方法
            var encodedFileName = Uri.EscapeDataString(fileName);

            // 同时设置 filename 和 filename* 字段以支持所有浏览器
            Response.Headers["Content-Disposition"] =
                $"attachment; filename=\"{encodedFileName}\"; filename*=UTF-8''{encodedFileName}";

            return PhysicalFile(
                task.TemplatePath,
                contentType,
                fileName,
                true); // 支持断点续传
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download template for task {Slug}", slug);
            return StatusCode(500, new { error = "下载失败，请稍后重试" });
        }
    }

    /// <summary>
    ///     获取任务基本信息（用于前端显示）
    /// </summary>
    [HttpGet("task/{slug}/info")]
    public async Task<IActionResult> GetTaskInfo(string slug)
    {
        try
        {
            var task = await _taskRepository.GetTaskBySlugAsync(slug);
            if (task == null) return NotFound(new { error = "任务不存在" });

            // 返回脱敏的任务信息（不包含密码）
            // 获取任务附件列表
            var attachments = await _taskAttachmentService.GetAttachmentsByTaskIdAsync(task.Id);

            // 获取允许的扩展名列表
            List<string>? allowedExtensions = null;
            if (task.AllowAttachmentUpload)
                allowedExtensions = await _fileExtensionService.GetAllowedExtensionsAsync(task.Id, task.Title, true);

            return Ok(new
            {
                id = task.Id,
                slug = task.Slug,
                title = task.Title,
                description = task.ShowDescriptionInApi ? task.Description : null,
                taskType = task.TaskType,
                hasPassword = !string.IsNullOrEmpty(task.PasswordHash),
                maxLimit = task.MaxLimit > 0 ? task.MaxLimit : (int?)null,
                currentCount = task.CurrentCount,
                expiryDate = task.ExpiryDate,
                versioningMode = task.VersioningMode,
                hasAttachment = attachments.Count > 0,
                allowAttachmentUpload = task.AllowAttachmentUpload,
                attachmentDownloadDescription = task.AttachmentDownloadDescription,
                allowedExtensions,
                isActive = task.IsActive,
                isExpired = task.ExpiryDate.HasValue && task.ExpiryDate.Value < DateTime.Now,
                isLimitReached = task.MaxLimit > 0 && task.CurrentCount >= task.MaxLimit
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task info for {Slug}", slug);
            return StatusCode(500, new { error = "获取任务信息失败" });
        }
    }
}