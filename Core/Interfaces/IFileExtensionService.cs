using MyLanServer.Core.Models;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     文件扩展名服务接口
/// </summary>
public interface IFileExtensionService
{
    /// <summary>
    ///     获取任务的扩展名配置
    /// </summary>
    Task<TaskFileExtensionsConfig> GetTaskExtensionsAsync(string taskId, string taskTitle);

    /// <summary>
    ///     保存任务的扩展名配置
    /// </summary>
    Task SaveTaskExtensionsAsync(TaskFileExtensionsConfig config);

    /// <summary>
    ///     获取任务允许的扩展名列表
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="taskTitle">任务标题</param>
    /// <param name="allowAttachmentUpload">是否允许上传附件</param>
    /// <returns>允许的扩展名列表，如果不允许上传附件则返回空列表</returns>
    Task<List<string>> GetAllowedExtensionsAsync(string taskId, string taskTitle, bool allowAttachmentUpload);

    /// <summary>
    ///     验证文件扩展名是否在允许列表中
    /// </summary>
    bool IsExtensionAllowed(string extension, List<string> allowedExtensions);

    /// <summary>
    ///     迁移任务扩展名配置（当任务标题修改时）
    /// </summary>
    Task MigrateTaskExtensionsAsync(string oldTaskTitle, string newTaskTitle);
}