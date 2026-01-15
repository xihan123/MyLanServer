using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Enums;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Web.Controllers;

/// <summary>
///     分发任务控制器 - 处理在线填表任务的 Schema 获取和数据提交
/// </summary>
[ApiController]
[Route("api")]
public class DistributionController : ControllerBase
{
    private readonly IAttachmentService _attachmentService;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IFileExtensionService _fileExtensionService;
    private readonly ILogger<DistributionController> _logger;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ITaskAttachmentService _taskAttachmentService;
    private readonly ITaskRepository _taskRepository;

    public DistributionController(
        ITaskRepository taskRepository,
        IPasswordHashService passwordHashService,
        IAttachmentService attachmentService,
        ITaskAttachmentService taskAttachmentService,
        IFileExtensionService fileExtensionService,
        IDepartmentRepository departmentRepository,
        ILogger<DistributionController> logger)
    {
        _taskRepository = taskRepository;
        _passwordHashService = passwordHashService;
        _attachmentService = attachmentService;
        _taskAttachmentService = taskAttachmentService;
        _fileExtensionService = fileExtensionService;
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    /// <summary>
    ///     获取表格结构定义（Schema）
    /// </summary>
    [HttpGet("distribution/{slug}/schema")]
    public IActionResult GetSchema(string slug)
    {
        try
        {
            // 从中间件获取已验证的任务
            var task = HttpContext.Items["Task"] as LanTask;
            if (task == null)
            {
                _logger.LogWarning("任务验证失败: {Slug}", slug);
                return NotFound(new { error = "任务验证失败" });
            }

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

            if (task.TaskType != TaskType.DataCollection)
            {
                _logger.LogWarning("任务不是在线填表类型: {Slug}", slug);
                return BadRequest(new { error = "该任务不是在线填表任务" });
            }

            if (!System.IO.File.Exists(task.TemplatePath))
            {
                _logger.LogWarning("表格结构文件不存在: {SchemaPath}", task.TemplatePath);
                return NotFound(new { error = "表格结构文件不存在" });
            }

            var json = System.IO.File.ReadAllText(task.TemplatePath);

            // 添加附件上传配置到 schema
            var schema = JsonSerializer.Deserialize<JsonElement>(json);
            var schemaDict = JsonSerializer.Deserialize<Dictionary<string, object>>(schema.ToString()) ??
                             new Dictionary<string, object>();
            schemaDict["allowAttachmentUpload"] = task.AllowAttachmentUpload;

            var modifiedJson = JsonSerializer.Serialize(schemaDict, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("成功获取表格结构（含附件配置）: {Slug}", slug);
            return Content(modifiedJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取表格结构失败: {Slug}", slug);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     提交表单数据
    /// </summary>
    [HttpPost("distribution/{slug}/submit")]
    [RequestSizeLimit(52428800)] // 50MB
    public async Task<IActionResult> SubmitData(string slug)
    {
        try
        {
            _logger.LogInformation("=== 开始处理提交请求 ===");
            _logger.LogInformation("Slug: {Slug}", slug);
            _logger.LogInformation("Request Content-Type: {ContentType}", Request.ContentType);
            _logger.LogInformation("Request Content-Length: {ContentLength}", Request.ContentLength);

            // 从中间件获取已验证的任务
            var task = HttpContext.Items["Task"] as LanTask;
            if (task == null)
            {
                _logger.LogWarning("任务验证失败: {Slug}, HttpContext.Items['Task'] 为 null", slug);
                return NotFound(new { error = "任务验证失败" });
            }

            _logger.LogInformation("从中间件获取到任务: TaskId={TaskId}, Slug={Slug}, TaskType={TaskType}",
                task.Id, task.Slug, task.TaskType);

            // 从请求体读取表单数据
            _logger.LogInformation("开始读取表单数据...");
            var form = await Request.ReadFormAsync();
            _logger.LogInformation("表单数据读取完成，字段数量: {FieldCount}", form.Count);

            // 验证密码（如果任务设置了密码）- 从表单数据中读取
            var password = form["password"].ToString();
            _logger.LogInformation(
                "密码验证: HasPasswordHash={HasPasswordHash}, HasPasswordFormField={HasPasswordFormField}",
                !string.IsNullOrEmpty(task.PasswordHash), !string.IsNullOrEmpty(password));

            if (!string.IsNullOrEmpty(task.PasswordHash))
            {
                if (string.IsNullOrEmpty(password) ||
                    !_passwordHashService.VerifyPassword(password, task.PasswordHash))
                {
                    _logger.LogWarning(
                        "Password verification failed for task {Slug} from IP {IP}",
                        slug,
                        HttpContext.Connection.RemoteIpAddress?.ToString());
                    return Unauthorized(new { error = "密码错误，请重新输入" });
                }

                _logger.LogInformation("密码验证通过");
            }

            if (task.TaskType != TaskType.DataCollection)
            {
                _logger.LogWarning("任务不是在线填表类型: {Slug}, TaskType={TaskType}", slug, task.TaskType);
                return BadRequest(new { error = "该任务不是在线填表任务" });
            }

            _logger.LogInformation("任务类型验证通过: DataCollection");

            // 从已读取的表单数据中获取字段值
            var name = form["name"].ToString();
            var contact = form["contact"].ToString();
            var department = form["department"].ToString();
            var jsonData = form["jsonData"].ToString();

            _logger.LogInformation(
                "表单字段值 - Name: {Name}, Contact: {Contact}, Department: {Department}, JsonDataLength: {JsonDataLength}",
                name, contact, department, jsonData?.Length ?? 0);

            if (!string.IsNullOrEmpty(jsonData)) _logger.LogDebug("JsonData 内容: {JsonData}", jsonData);

            // 验证必填字段
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("验证失败: 提交人姓名为空");
                return BadRequest(new { error = "提交人姓名不能为空" });
            }

            if (string.IsNullOrWhiteSpace(contact) || contact.Length < 4 || contact.Length > 11)
            {
                _logger.LogWarning("验证失败: 联系方式长度不符合要求, Length={Length}", contact?.Length ?? 0);
                return BadRequest(new { error = "联系方式长度必须在 4-11 位之间" });
            }

            if (string.IsNullOrWhiteSpace(department))
            {
                _logger.LogWarning("验证失败: 所属单位/部门为空");
                return BadRequest(new { error = "所属单位/部门不能为空" });
            }

            // 验证部门是否存在
            var allDepartments = await _departmentRepository.GetAllAsync();
            var departmentExists = allDepartments.Any(d => d.Name == department);
            if (!departmentExists)
            {
                _logger.LogWarning("验证失败: 部门不存在: {DepartmentName}", department);
                return BadRequest(new { error = "所属单位/部门不存在，请从下拉列表中选择" });
            }

            if (string.IsNullOrWhiteSpace(jsonData))
            {
                _logger.LogWarning("验证失败: 表单数据为空");
                return BadRequest(new { error = "表单数据不能为空" });
            }

            // 将所属部门添加到 JSON 数据中
            var dataObj = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonData) ??
                          new Dictionary<string, object>();
            dataObj["所属部门"] = department;
            jsonData = JsonSerializer.Serialize(dataObj);

            _logger.LogInformation("必填字段验证通过");

            // 处理附件上传
            List<string>? attachmentPaths = null;
            if (task.AllowAttachmentUpload)
            {
                _logger.LogInformation("任务允许上传附件，开始处理附件...");
                var attachmentFiles = form.Files.GetFiles("attachment");
                if (attachmentFiles != null && attachmentFiles.Count > 0)
                {
                    _logger.LogInformation("检测到 {Count} 个附件文件", attachmentFiles.Count);
                    attachmentPaths = new List<string>();

                    // 获取任务允许的扩展名
                    List<string>? taskAllowedExtensions = null;
                    if (task.AllowAttachmentUpload)
                    {
                        var taskAllowedExtensionsList =
                            await _fileExtensionService.GetAllowedExtensionsAsync(task.Id, task.Title, true);
                        taskAllowedExtensions = taskAllowedExtensionsList.Count > 0 ? taskAllowedExtensionsList : null;
                    }

                    foreach (var attachmentFile in attachmentFiles)
                    {
                        _logger.LogInformation("处理附件文件: FileName={FileName}, Size={Size}",
                            attachmentFile.FileName, attachmentFile.Length);

                        // 验证附件大小（50MB 限制）
                        if (attachmentFile.Length > 52428800)
                        {
                            _logger.LogWarning("附件大小超过限制: {FileName}, Size={Size}",
                                attachmentFile.FileName, attachmentFile.Length);
                            return BadRequest(new { error = $"附件 \"{attachmentFile.FileName}\" 大小不能超过 50MB" });
                        }

                        // 验证附件格式
                        var attachmentExtension = Path.GetExtension(attachmentFile.FileName);
                        if (taskAllowedExtensions != null && taskAllowedExtensions.Count > 0)
                            if (!_fileExtensionService.IsExtensionAllowed(attachmentExtension, taskAllowedExtensions))
                            {
                                _logger.LogWarning("附件格式不支持: {FileName}, Extension={Extension}",
                                    attachmentFile.FileName, attachmentExtension);
                                return BadRequest(new
                                {
                                    error =
                                        $"附件 \"{attachmentFile.FileName}\" 的文件格式不支持，允许的格式：{string.Join(", ", taskAllowedExtensions)}"
                                });
                            }

                        using (var stream = attachmentFile.OpenReadStream())
                        {
                            var path = await _attachmentService.ProcessAttachmentAsync(
                                stream, task, name, contact, department, attachmentFile.FileName);
                            attachmentPaths.Add(path);
                            _logger.LogInformation("附件保存成功: {Path}", path);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("任务允许上传附件，但用户未上传附件");
                }
            }
            else
            {
                _logger.LogInformation("任务不允许上传附件");
            }

            // 保存 JSON 数据文件
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var safeName = SanitizeFileName(name);
            var safeDepartment = SanitizeFileName(department);
            var filename = $"{safeName}_{safeDepartment}_{timestamp}.json";
            var filePath = Path.Combine(task.CollectionPath, filename);

            _logger.LogInformation("准备保存文件: FilePath={FilePath}, Filename={Filename}", filePath, filename);
            _logger.LogInformation("目标文件夹: {TargetFolder}", task.CollectionPath);

            // 确保目标目录存在
            if (!Directory.Exists(task.CollectionPath))
            {
                _logger.LogInformation("目标文件夹不存在，正在创建: {TargetFolder}", task.CollectionPath);
                Directory.CreateDirectory(task.CollectionPath);
            }

            _logger.LogInformation("开始写入文件...");
            await System.IO.File.WriteAllTextAsync(filePath, jsonData);
            _logger.LogInformation("文件写入成功");

            // 记录提交记录
            _logger.LogInformation("开始记录提交记录...");
            var attachmentPathJson = attachmentPaths != null && attachmentPaths.Count > 0
                ? JsonSerializer.Serialize(attachmentPaths)
                : null;

            await _taskRepository.RecordSubmissionAsync(new Submission
            {
                TaskId = task.Id,
                SubmitterName = name,
                Contact = contact,
                Department = department,
                OriginalFilename = filename,
                StoredFilename = filename,
                AttachmentPath = attachmentPathJson,
                Timestamp = DateTime.UtcNow,
                ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            });
            _logger.LogInformation("提交记录保存成功");

            _logger.LogInformation("=== 数据提交成功: {Slug}, 提交人: {Name}, 所属部门: {Department}, 附件数量: {AttachmentCount} ===",
                slug, name, department, attachmentPaths?.Count ?? 0);
            return Ok(new
            {
                message = "提交成功",
                filename,
                submitter = name,
                contact,
                department,
                attachmentCount = attachmentPaths?.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "=== 提交数据失败: {Slug}, Exception: {Exception}, Message: {Message}, StackTrace: {StackTrace} ===",
                slug, ex.GetType().Name, ex.Message, ex.StackTrace);
            _logger.LogError("Inner Exception: {InnerException}", ex.InnerException?.Message);
            return StatusCode(500, new { error = "服务器错误", detail = ex.Message });
        }
    }

    /// <summary>
    ///     获取任务附件列表
    /// </summary>
    [HttpGet("distribution/{slug}/attachments")]
    public async Task<IActionResult> GetAttachments(string slug)
    {
        try
        {
            // 从中间件获取已验证的任务
            var task = HttpContext.Items["Task"] as LanTask;
            if (task == null)
            {
                _logger.LogWarning("任务验证失败: {Slug}", slug);
                return NotFound(new { error = "任务验证失败" });
            }

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

            var attachments = await _taskAttachmentService.GetAttachmentsBySlugAsync(slug);
            _logger.LogInformation("成功获取任务附件列表: {Slug}, 附件数量: {Count}", slug, attachments.Count);
            return Ok(new
            {
                attachments,
                attachmentDownloadDescription = task.AttachmentDownloadDescription
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取附件列表失败: {Slug}", slug);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     下载任务附件
    /// </summary>
    [HttpGet("distribution/{slug}/attachments/{attachmentId}")]
    public async Task<IActionResult> DownloadAttachment(string slug, int attachmentId)
    {
        try
        {
            // 从中间件获取已验证的任务
            var task = HttpContext.Items["Task"] as LanTask;
            if (task == null)
            {
                _logger.LogWarning("任务验证失败: {Slug}", slug);
                return NotFound(new { error = "任务验证失败" });
            }

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

            var attachments = await _taskAttachmentService.GetAttachmentsByTaskIdAsync(task.Id);
            var attachment = attachments.FirstOrDefault(a => a.Id == attachmentId);

            if (attachment == null)
            {
                _logger.LogWarning("附件不存在: {Slug}, AttachmentId: {AttachmentId}", slug, attachmentId);
                return NotFound(new { error = "附件不存在" });
            }

            if (!System.IO.File.Exists(attachment.FilePath))
            {
                _logger.LogWarning("附件文件不存在: {FilePath}", attachment.FilePath);
                return NotFound(new { error = "附件文件不存在" });
            }

            // 返回文件流
            var fileBytes = await System.IO.File.ReadAllBytesAsync(attachment.FilePath);
            var contentType = GetContentType(attachment.FileName);
            var downloadName = attachment.FileName;

            // 使用符合 RFC 3986 标准的编码方法
            var encodedFileName = Uri.EscapeDataString(downloadName);

            // 同时设置 filename 和 filename* 字段以支持所有浏览器
            Response.Headers["Content-Disposition"] =
                $"attachment; filename=\"{encodedFileName}\"; filename*=UTF-8''{encodedFileName}";

            _logger.LogInformation("成功下载附件: {Slug}, AttachmentId: {AttachmentId}, FileName: {FileName}",
                slug, attachmentId, downloadName);
            return File(fileBytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载附件失败: {Slug}, AttachmentId: {AttachmentId}", slug, attachmentId);
            return StatusCode(500, new { error = "服务器错误" });
        }
    }

    /// <summary>
    ///     根据文件扩展名获取 Content-Type
    /// </summary>
    private string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".7z" => "application/x-7z-compressed",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    ///     清理文件名，移除非法字符
    /// </summary>
    private string SanitizeFileName(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", input.Split(invalidChars));
        return sanitized;
    }
}

/// <summary>
///     分发任务提交请求
/// </summary>
public class DistributionSubmitRequest
{
    /// <summary>
    ///     提交人姓名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     联系方式
    /// </summary>
    public string Contact { get; set; } = string.Empty;

    /// <summary>
    ///     JSON 数据
    /// </summary>
    public string JsonData { get; set; } = string.Empty;
}