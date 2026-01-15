using MyLanServer.Core.Models;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     任务附件服务接口
/// </summary>
public interface ITaskAttachmentService
{
    /// <summary>
    ///     获取任务的所有附件
    /// </summary>
    /// <param name="taskId">任务 ID</param>
    /// <returns>附件列表</returns>
    Task<List<TaskAttachment>> GetAttachmentsByTaskIdAsync(string taskId);

    /// <summary>
    ///     根据任务 Slug 获取附件列表（用于 API）
    /// </summary>
    /// <param name="slug">任务 Slug</param>
    /// <returns>附件 DTO 列表</returns>
    Task<List<TaskAttachmentDto>> GetAttachmentsBySlugAsync(string slug);

    /// <summary>
    ///     添加附件到任务
    /// </summary>
    /// <param name="attachment">附件对象</param>
    /// <returns>添加后的附件对象（包含 ID）</returns>
    Task<TaskAttachment> AddAttachmentAsync(TaskAttachment attachment);

    /// <summary>
    ///     删除附件
    /// </summary>
    /// <param name="attachmentId">附件 ID</param>
    /// <returns>是否删除成功</returns>
    Task<bool> DeleteAttachmentAsync(int attachmentId);

    /// <summary>
    ///     更新附件信息
    /// </summary>
    /// <param name="attachment">附件对象</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateAttachmentAsync(TaskAttachment attachment);

    /// <summary>
    ///     更新附件排序
    /// </summary>
    /// <param name="attachments">附件列表</param>
    /// <returns>是否更新成功</returns>
    Task<bool> UpdateAttachmentsOrderAsync(List<TaskAttachment> attachments);

    /// <summary>
    ///     复制文件到目标目录并返回路径
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="targetFolder">目标文件夹</param>
    /// <param name="fileName">文件名</param>
    /// <returns>目标文件路径</returns>
    Task<string> CopyFileToTaskFolderAsync(string sourcePath, string targetFolder, string fileName);

    /// <summary>
    ///     复制任务的所有附件到新任务
    /// </summary>
    /// <param name="sourceTaskId">源任务 ID</param>
    /// <param name="targetTaskId">目标任务 ID</param>
    /// <param name="sourceAttachmentsPath">源任务附件目录</param>
    /// <param name="targetAttachmentsPath">目标任务附件目录</param>
    /// <returns>复制的附件数量</returns>
    Task<int> CopyAttachmentsAsync(string sourceTaskId, string targetTaskId,
        string sourceAttachmentsPath, string targetAttachmentsPath);
}