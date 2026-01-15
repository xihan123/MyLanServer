using Microsoft.AspNetCore.Builder;

namespace MyLanServer.Infrastructure.Web.Middleware;

/// <summary>
///     中间件扩展方法
/// </summary>
public static class MiddlewareExtensions
{
    /// <summary>
    ///     注册任务验证中间件
    /// </summary>
    public static IApplicationBuilder UseTaskValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TaskValidationMiddleware>();
    }

    /// <summary>
    ///     注册全局异常处理中间件
    /// </summary>
    public static IApplicationBuilder UseGlobalException(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}