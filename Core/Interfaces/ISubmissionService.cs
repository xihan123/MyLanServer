using System.IO;
using MyLanServer.Core.Models;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     文件处理服务接口：处理上传流、文件命名、磁盘写入
/// </summary>
public interface ISubmissionService
{
    /// <summary>
    ///     处理上传的文件流并保存到磁盘
    /// </summary>
    /// <param name="fileStream">输入文件流</param>
    /// <param name="task">关联的任务配置</param>
    /// <param name="submitter">提交人</param>
    /// <param name="contact">联系方式</param>
    /// <param name="department">所属单位/部门</param>
    /// <param name="originalFileName">原始文件名</param>
    /// <returns>保存后的文件完整物理路径</returns>
    Task<string> ProcessSubmissionAsync(Stream fileStream, LanTask task, string submitter, string contact,
        string department, string originalFileName);
}