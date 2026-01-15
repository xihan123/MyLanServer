using Microsoft.AspNetCore.Mvc;

namespace MyLanServer.Infrastructure.Web.Controllers;

/// <summary>
///     页面控制器
///     提供下载和上传页面的访问路由
/// </summary>
[Route("")]
[ApiController]
public class PageController : ControllerBase
{
    /// <summary>
    ///     下载页面
    ///     路由：/download?slug=ABC123
    /// </summary>
    [HttpGet("download")]
    public IActionResult DownloadPage([FromQuery] string slug)
    {
        // 重定向到静态HTML文件，slug参数会保留在URL中
        return RedirectPermanent($"~/download.html?slug={slug}");
    }

    /// <summary>
    ///     上传页面
    ///     路由：/upload?slug=ABC123
    /// </summary>
    [HttpGet("upload")]
    public IActionResult UploadPage([FromQuery] string slug)
    {
        // 重定向到静态HTML文件，slug参数会保留在URL中
        return RedirectPermanent($"~/upload.html?slug={slug}");
    }

    /// <summary>
    ///     统一任务页面
    ///     路由：/task/ABC123
    /// </summary>
    [HttpGet("task/{slug}")]
    public IActionResult TaskPage(string slug)
    {
        // 重定向到静态HTML文件，slug参数会保留在URL中
        return RedirectPermanent($"~/task.html?slug={slug}");
    }

    /// <summary>
    ///     首页 - 简单的欢迎页面
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        var html = @"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>局域网文件分发工具</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }
        .container {
            background: white;
            border-radius: 16px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            max-width: 600px;
            width: 100%;
            padding: 40px;
            text-align: center;
        }
        h1 { color: #333; font-size: 32px; margin-bottom: 10px; }
        p { color: #666; margin-bottom: 30px; line-height: 1.6; }
        .info-box {
            background: #e3f2fd;
            color: #1565c0;
            padding: 16px;
            border-radius: 8px;
            margin: 20px 0;
            border-left: 4px solid #1565c0;
            text-align: left;
        }
        .info-box code {
            background: #f0f0f0;
            padding: 2px 8px;
            border-radius: 4px;
            font-family: 'Courier New', monospace;
            color: #c7254e;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>📁 局域网文件分发工具</h1>
        <p>欢迎使用局域网文件分发与收集系统</p>
        
        <div class='info-box'>
            <strong>使用说明：</strong><br><br>
            1. 管理员在主程序中创建任务并生成链接<br>
            2. 下载模板：<code>/download?slug=任务ID</code><br>
            3. 上传文件：<code>/upload?slug=任务ID</code>
        </div>
        
        <p style='font-size: 14px; color: #999;'>
            请联系管理员获取访问链接和密码
        </p>
    </div>
</body>
</html>";
        return Content(html, "text/html; charset=utf-8");
    }
}