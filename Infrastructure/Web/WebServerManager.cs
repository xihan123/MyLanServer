using System.IO;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Infrastructure.Data;
using MyLanServer.Infrastructure.Web.Middleware;

namespace MyLanServer.Infrastructure.Web;

public class WebServerManager : IWebServerService
{
    private readonly ILogger<WebServerManager> _logger;

    // 引用主程序的 ServiceProvider，用于在 WebHost 中注入单例服务
    private readonly IServiceProvider _rootServiceProvider;
    private IHost? _webHost;

    public WebServerManager(IServiceProvider rootServiceProvider, ILogger<WebServerManager> logger)
    {
        _rootServiceProvider = rootServiceProvider;
        _logger = logger;
    }

    public bool IsRunning => _webHost != null;

    public async Task StartServerAsync(int port)
    {
        if (_webHost != null) await StopServerAsync();

        try
        {
            _webHost = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    // 共享主程序的 Logger
                    services.AddSingleton(_rootServiceProvider.GetRequiredService<ILoggerFactory>());
                    services.AddSingleton(_rootServiceProvider.GetRequiredService<ILogger<WebServerManager>>());
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        // 兼容性设置：强制 HTTP/1.1，绑定所有 IP（Windows 7 兼容）
                        options.Listen(IPAddress.Any, port,
                            listenOptions => { listenOptions.Protocols = HttpProtocols.Http1; });
                        // 限制请求体大小 50MB
                        options.Limits.MaxRequestBodySize = 52428800;
                    });

                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddControllersWithViews();

                        // 注册任务验证中间件为服务（启用依赖注入）
                        services.AddScoped<TaskValidationMiddleware>();

                        // 桥接 Root DI -> Web DI
                        // 这样 Controller 就可以使用主程序的 Service 和 Repository
                        services.AddSingleton(_rootServiceProvider.GetRequiredService<ISubmissionService>());
                        services.AddSingleton(_rootServiceProvider.GetRequiredService<ITaskRepository>());
                        services.AddSingleton(_rootServiceProvider.GetRequiredService<IDepartmentRepository>());
                        services.AddSingleton(_rootServiceProvider.GetRequiredService<DapperContext>());
                        services.AddSingleton(_rootServiceProvider.GetRequiredService<IPasswordHashService>());
                        services.AddSingleton(_rootServiceProvider.GetRequiredService<IAttachmentService>());
                        services.AddSingleton(_rootServiceProvider.GetRequiredService<ITaskAttachmentService>());
                        services.AddSingleton(_rootServiceProvider.GetRequiredService<IFileExtensionService>());
                        services.AddSingleton(_rootServiceProvider.GetRequiredService<IFileValidationService>());
                    });

                    webBuilder.Configure(app =>
                    {
                        // 请求日志中间件（最外层）
                        app.Use(async (context, next) =>
                        {
                            var logger = app.ApplicationServices.GetRequiredService<ILogger<WebServerManager>>();
                            logger.LogInformation(
                                ">>> 收到请求 - Method: {Method}, Path: {Path}, ContentType: {ContentType}",
                                context.Request.Method, context.Request.Path, context.Request.ContentType);
                            await next();
                            logger.LogInformation("<<< 响应完成 - StatusCode: {StatusCode}", context.Response.StatusCode);
                        });

                        // 静态文件支持（必须在中间件之前）
                        IFileProvider fileProvider;

                        // 尝试使用嵌入式资源提供器（单文件发布时）
                        try
                        {
                            var embeddedProvider = new ManifestEmbeddedFileProvider(
                                typeof(Program).Assembly,
                                "wwwroot"
                            );

                            // 验证嵌入资源是否存在
                            var fileInfo = embeddedProvider.GetFileInfo("task.html");
                            if (fileInfo.Exists)
                            {
                                fileProvider = embeddedProvider;
                                _logger.LogInformation("Using embedded wwwroot resources");
                            }
                            else
                            {
                                throw new FileNotFoundException("Embedded wwwroot not found");
                            }
                        }
                        catch
                        {
                            // 回退到物理文件提供器（开发环境）
                            var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
                            if (!Directory.Exists(wwwrootPath))
                                // 如果输出目录中没有 wwwroot，尝试使用项目目录
                                wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

                            fileProvider = new PhysicalFileProvider(wwwrootPath);
                            _logger.LogInformation("Using physical wwwroot at: {Path}", wwwrootPath);
                        }

                        app.UseStaticFiles(new StaticFileOptions
                        {
                            ServeUnknownFileTypes = false,
                            FileProvider = fileProvider
                        });

                        // 全局异常处理中间件（最外层）
                        app.UseGlobalException();

                        // 任务验证中间件（在路由之前）
                        app.UseTaskValidation();

                        app.UseRouting();
                        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
                    });
                })
                .Build();

            await _webHost.StartAsync();
            _logger.LogInformation("Kestrel Server started on port {Port}", port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Kestrel Server.");
            throw; // 抛出给 UI 层处理
        }
    }

    public async Task StopServerAsync()
    {
        if (_webHost != null)
            try
            {
                await _webHost.StopAsync(TimeSpan.FromSeconds(3));
            }
            finally
            {
                _webHost.Dispose();
                _webHost = null;
                _logger.LogInformation("Kestrel Server stopped.");
            }
    }
}