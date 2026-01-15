using Microsoft.AspNetCore.Http;

namespace MyLanServer.Infrastructure.Web.Dtos;

/// <summary>
///     文件上传请求模型
/// </summary>
public class UploadRequest
{
    /// <summary>
    ///     提交人姓名
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     联系方式（手机号）
    /// </summary>
    public string? Contact { get; set; }

    /// <summary>
    ///     所属单位/部门
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    ///     访问密码（如果任务设置了密码）
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    ///     上传的 Excel 文件
    /// </summary>
    public IFormFile? File { get; set; }

    /// <summary>
    ///     上传的附件文件（可选）
    /// </summary>
    public List<IFormFile>? Attachments { get; set; }
}