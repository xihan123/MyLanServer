using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;
using MyLanServer.Infrastructure.Web.Dtos;

namespace MyLanServer.Infrastructure.Web.Controllers;

/// <summary>
///     文件上传控制器
///     使用流式处理避免大文件内存溢出
/// </summary>
[Route("api")]
[ApiController]
public class UploadController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IFileExtensionService _fileExtensionService;
    private readonly IFileValidationService _fileValidationService;
    private readonly ILogger<UploadController> _logger;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ISubmissionService _submissionService;
    private readonly ITaskRepository _taskRepository;

    public UploadController(
        ISubmissionService submissionService,
        ITaskRepository taskRepository,
        IPasswordHashService passwordHashService,
        IAttachmentService attachmentService,
        IFileExtensionService fileExtensionService,
        IFileValidationService fileValidationService,
        IDepartmentRepository departmentRepository,
        ILogger<UploadController> logger)
    {
        _submissionService = submissionService;
        _taskRepository = taskRepository;
        _passwordHashService = passwordHashService;
        _attachmentService = attachmentService;
        _fileExtensionService = fileExtensionService;
        _fileValidationService = fileValidationService;
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    /// <summary>
    ///     文件提交接口
    /// </summary>
    [HttpPost("submit/{slug}")]
    [RequestSizeLimit(52428800)] // 50MB
    public async Task<IActionResult> Submit(string slug, [FromForm] UploadRequest request)
    {
        try
        {
            // 从中间件获取已验证的任务
            var task = HttpContext.Items["Task"] as LanTask;
            if (task == null) return BadRequest(new { error = "任务验证失败" });

            // --- 验证必填字段 ---
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { error = "请输入姓名" });

            if (string.IsNullOrWhiteSpace(request.Contact)) return BadRequest(new { error = "请输入联系方式" });

            if (string.IsNullOrWhiteSpace(request.Department)) return BadRequest(new { error = "请输入所属单位/部门" });

            // 验证联系方式（4-11位）
            if (string.IsNullOrWhiteSpace(request.Contact) || request.Contact.Length < 4 || request.Contact.Length > 11)
                return BadRequest(new { error = "请输入4-11位的联系方式" });

            // 验证部门是否存在
            var allDepartments = await _departmentRepository.GetAllAsync();
            var departmentExists = allDepartments.Any(d => d.Name == request.Department);
            if (!departmentExists)
            {
                _logger.LogWarning("部门不存在: {DepartmentName}, Slug: {Slug}", request.Department, slug);
                return BadRequest(new { error = "所属单位/部门不存在，请从下拉列表中选择" });
            }

            // 验证密码（如果任务设置了密码）
            if (!string.IsNullOrEmpty(task.PasswordHash))
                if (string.IsNullOrEmpty(request.Password) ||
                    !_passwordHashService.VerifyPassword(request.Password, task.PasswordHash))
                {
                    _logger.LogWarning(
                        "Password verification failed for task {Slug} from IP {IP}",
                        slug,
                        HttpContext.Connection.RemoteIpAddress?.ToString());
                    return Unauthorized(new { error = "密码错误" });
                }

            // 验证文件是否存在
            if (request.File == null || request.File.Length == 0) return BadRequest(new { error = "请上传文件" });

            // 验证文件格式（支持 .xlsx 和 .xls）
            var fileName = request.File.FileName;
            var fileExtension = Path.GetExtension(fileName);
            var allowedExtensions = new[] { ".xlsx", ".xls" };
            if (!allowedExtensions.Contains(fileExtension?.ToLower()))
                return BadRequest(new { error = "仅支持 .xlsx 或 .xls 格式的 Excel 文件" });

            // 验证文件大小
            if (request.File.Length > 52428800) return BadRequest(new { error = "文件大小超过 50MB 限制" });

            // 验证文件内容（魔数验证）
            using (var fileStream = request.File.OpenReadStream())
            {
                var isValidContent =
                    await _fileValidationService.ValidateFileContentAsync(fileStream, fileExtension ?? "");
                if (!isValidContent)
                {
                    _logger.LogWarning("Invalid file content for extension: {Extension}", fileExtension);
                    return BadRequest(new { error = $"文件内容与扩展名不匹配，请上传有效的 {fileExtension} 文件" });
                }
            }

            // --- 文件处理 ---
            var savedPath = await _submissionService.ProcessSubmissionAsync(
                request.File.OpenReadStream(),
                task,
                request.Name,
                request.Contact,
                request.Department,
                fileName);

            // --- 附件处理 ---
            var attachmentPaths = new List<string>();
            if (request.Attachments != null && request.Attachments.Count > 0)
            {
                // 获取任务允许的扩展名
                List<string>? taskAllowedExtensions = null;
                if (task.AllowAttachmentUpload)
                {
                    var taskAllowedExtensionsList =
                        await _fileExtensionService.GetAllowedExtensionsAsync(task.Id, task.Title, true);
                    taskAllowedExtensions = taskAllowedExtensionsList.Count > 0 ? taskAllowedExtensionsList : null;
                }

                foreach (var attachmentFile in request.Attachments)
                    try
                    {
                        var attachmentExtension = Path.GetExtension(attachmentFile.FileName);

                        // 验证附件格式
                        if (taskAllowedExtensions != null && taskAllowedExtensions.Count > 0)
                            if (!_fileExtensionService.IsExtensionAllowed(attachmentExtension, taskAllowedExtensions))
                                return BadRequest(new
                                {
                                    error =
                                        $"附件 \"{attachmentFile.FileName}\" 的文件格式不支持，允许的格式：{string.Join(", ", taskAllowedExtensions)}"
                                });

                        var attachmentPath = await _attachmentService.ProcessAttachmentAsync(
                            attachmentFile.OpenReadStream(),
                            task,
                            request.Name,
                            request.Contact,
                            request.Department,
                            attachmentFile.FileName);
                        attachmentPaths.Add(attachmentPath);

                        _logger.LogInformation("Attachment uploaded successfully: {File}", attachmentFile.FileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to upload attachment: {File}", attachmentFile.FileName);
                    }
            }

            // --- 记录提交 ---
            await _taskRepository.RecordSubmissionAsync(new Submission
            {
                TaskId = task.Id,
                SubmitterName = request.Name,
                Contact = request.Contact,
                Department = request.Department,
                OriginalFilename = fileName,
                StoredFilename = Path.GetFileName(savedPath),
                Timestamp = DateTime.UtcNow,
                ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                AttachmentPath = attachmentPaths.Count > 0 ? JsonSerializer.Serialize(attachmentPaths) : null
            });

            _logger.LogInformation("File submitted successfully: {File} by {Name}", fileName, request.Name);

            return Ok(new
            {
                message = "提交成功",
                filename = Path.GetFileName(savedPath),
                submitter = request.Name,
                contact = request.Contact
            });
        }
        catch (IOException ioEx)
        {
            _logger.LogError(ioEx, "IO error during file upload for task {Slug}", slug);
            return StatusCode(500, new { error = "文件保存失败，请检查磁盘空间或文件权限" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload processing failed for task {Slug}", slug);
            return StatusCode(500, new { error = "服务器内部错误，请稍后重试" });
        }
    }
}