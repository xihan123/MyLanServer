using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Exceptions;

namespace MyLanServer.Infrastructure.Web.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly RequestDelegate _next;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            // 处理领域异常
            _logger.LogWarning(ex, "Domain exception occurred - Code: {Code}, Message: {Message}", ex.ErrorCode,
                ex.Message);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(new { error = ex.Message, code = ex.ErrorCode });
        }
        catch (Exception ex)
        {
            // 处理未处理的异常
            _logger.LogError(ex, "Unhandled API Exception - Method: {Method}, Path: {Path}, ContentType: {ContentType}",
                context.Request.Method, context.Request.Path, context.Request.ContentType);
            _logger.LogError("Exception Type: {Type}, Message: {Message}", ex.GetType().Name, ex.Message);
            _logger.LogError("StackTrace: {StackTrace}", ex.StackTrace);
            if (ex.InnerException != null)
                _logger.LogError("Inner Exception: {Type}, Message: {Message}",
                    ex.InnerException.GetType().Name, ex.InnerException.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var response = new { error = "Internal Server Error", message = exception.Message };
        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}