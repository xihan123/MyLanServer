using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;

namespace MyLanServer.Infrastructure.Web.Middleware;

/// <summary>
///     任务验证中间件
///     功能：验证链接有效性（过期、下载上限、密码验证）
///     适用于：模板下载接口和文件提交接口
/// </summary>
public class TaskValidationMiddleware
{
    private readonly ILogger<TaskValidationMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly ITaskRepository _taskRepository;

    public TaskValidationMiddleware(
        RequestDelegate next,
        ITaskRepository taskRepository,
        ILogger<TaskValidationMiddleware> logger)
    {
        _next = next;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 记录所有请求
        _logger.LogInformation("=== 收到请求 ===");
        _logger.LogInformation("Method: {Method}, Path: {Path}, ContentType: {ContentType}",
            context.Request.Method, context.Request.Path, context.Request.ContentType);

        // 获取 slug：从 URL 路径或查询参数中提取
        string? slug = null;
        var path = context.Request.Path.Value ?? "";

        // 尝试从 URL 路径中提取 slug
        // 支持的路由模式：
        // 1. /api/template/{slug}
        // 2. /api/distribution/{slug}/schema
        // 3. /api/distribution/{slug}/submit
        var pathSegments = path.Split('/');
        for (var i = 0; i < pathSegments.Length; i++)
        {
            var segment = pathSegments[i];
            // 检查是否是可能的 slug（14位字母数字，不是文件名）
            if (!string.IsNullOrWhiteSpace(segment) &&
                segment.Length == 14 &&
                !segment.Contains('.') &&
                Regex.IsMatch(segment, @"^[A-Za-z0-9]+$"))
                // 检查是否在 "template"、"distribution" 等已知路由后面
                if (i > 0)
                {
                    var previousSegment = pathSegments[i - 1].ToLowerInvariant();
                    if (previousSegment == "template" || previousSegment == "distribution" ||
                        previousSegment == "submit")
                    {
                        slug = segment;
                        _logger.LogInformation("Extracted slug from path: {Slug}", slug);
                        break;
                    }
                }
        }

        // 如果路径中没有，尝试从查询参数获取
        if (string.IsNullOrWhiteSpace(slug) &&
            context.Request.Query.TryGetValue("slug", out var querySlug))
        {
            slug = querySlug.ToString();
            _logger.LogInformation("Extracted slug from query: {Slug}", slug);
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            await _next(context);
            return;
        }

        try
        {
            _logger.LogInformation("Starting validation for slug: {Slug}", slug);

            // 查询任务
            var task = await _taskRepository.GetTaskBySlugAsync(slug);
            _logger.LogInformation(
                "Task query result for {Slug}: {TaskExists}, IsActive: {IsActive}, ExpiryDate: {ExpiryDate}",
                slug, task != null, task?.IsActive, task?.ExpiryDate);

            if (task == null)
            {
                await WriteErrorResponse(context, 404, "任务不存在或已删除");
                return;
            }

            // 检查任务是否激活
            if (!task.IsActive)
            {
                await WriteErrorResponse(context, 403, "任务已关闭，请联系管理员");
                return;
            }

            // 检查过期时间
            if (task.ExpiryDate.HasValue && task.ExpiryDate.Value < DateTime.Now)
            {
                await WriteErrorResponse(context, 403, "任务已过期，提交已截止");
                return;
            }

            // 检查提交数量上限（仅对提交操作）
            if (task?.MaxLimit > 0 && task != null && task.CurrentCount >= task.MaxLimit)
            {
                // 如果是下载模板或获取 schema 操作，仍然允许
                var pathForLimit = context.Request.Path.Value;
                if (pathForLimit != null && !pathForLimit.Contains("template") && !pathForLimit.Contains("schema"))
                {
                    await WriteErrorResponse(context, 403, $"提交数量已达上限 ({task.MaxLimit} 份)");
                    return;
                }
            }

            // 检查一次性下载链接（仅对下载操作）
            var pathValue = context.Request.Path.Value;
            if (pathValue != null && pathValue.Contains("template") && task?.IsOneTimeLink == true)
                if (task.DownloadsCount >= 1)
                {
                    await WriteErrorResponse(context, 404, "链接已失效（仅限一次下载）");
                    return;
                }

            // 将任务信息存储到 HttpContext，供后续使用
            context.Items["Task"] = task;
            _logger.LogInformation("Task stored in HttpContext for slug: {Slug}", slug);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task validation failed for slug {Slug}", slug);
            await WriteErrorResponse(context, 500, "服务器内部错误，请稍后重试");
        }
    }

    private async Task WriteErrorResponse(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = message,
            statusCode
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
        await context.Response.CompleteAsync(); // 确保响应完成
    }
}