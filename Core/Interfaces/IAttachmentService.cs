using System.IO;
using MyLanServer.Core.Models;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     附件上传服务接口
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    ///     处理附件上传
    /// </summary>
    /// <param name="fileStream">文件流</param>
    /// <param name="task">任务</param>
    /// <param name="submitterName">提交人姓名</param>
    /// <param name="contact">联系方式</param>
    /// <param name="department">所属部门</param>
    /// <param name="originalFileName">原始文件名</param>
    /// <returns>保存的文件路径</returns>
    Task<string> ProcessAttachmentAsync(Stream fileStream, LanTask task, string submitterName,
        string contact, string department, string originalFileName);
}