using MyLanServer.Core.Models;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     数据仓储接口：负责 Task 和 Submission 的持久化
/// </summary>
public interface ITaskRepository
{
    /// <summary>
    ///     获取所有配置的任务
    /// </summary>
    Task<IEnumerable<LanTask>> GetAllTasksAsync();

    /// <summary>
    ///     根据 Slug 获取单个任务 (用于 API 验证)
    /// </summary>
    Task<LanTask?> GetTaskBySlugAsync(string slug);

    /// <summary>
    ///     创建新任务
    /// </summary>
    Task<bool> CreateTaskAsync(LanTask task);

    /// <summary>
    ///     更新任务配置
    /// </summary>
    Task<bool> UpdateTaskAsync(LanTask task);

    /// <summary>
    ///     删除任务 (软删除或硬删除，视实现而定)
    /// </summary>
    Task<bool> DeleteTaskAsync(string taskId);

    /// <summary>
    ///     事务性操作：记录提交信息并原子性地增加任务的计数器
    /// </summary>
    Task RecordSubmissionAsync(Submission submission);

    /// <summary>
    ///     获取某个任务的所有提交记录
    /// </summary>
    Task<IEnumerable<Submission>> GetSubmissionsByTaskIdAsync(string taskId);

    /// <summary>
    ///     增加下载计数
    /// </summary>
    Task<bool> IncrementDownloadsCountAsync(string taskId);

    /// <summary>
    ///     删除单个提交记录
    /// </summary>
    Task<bool> DeleteSubmissionAsync(long submissionId);

    /// <summary>
    ///     清空所有提交记录
    /// </summary>
    Task<int> ClearSubmissionsAsync(string taskId);

    /// <summary>
    ///     减少当前计数
    /// </summary>
    Task<bool> DecrementCurrentCountAsync(string taskId);

    /// <summary>
    ///     更新当前计数
    /// </summary>
    Task<bool> UpdateCurrentCountAsync(string taskId, int count);

    /// <summary>
    ///     复制任务（创建新任务，复制配置）
    /// </summary>
    /// <param name="sourceTaskId">源任务 ID</param>
    /// <param name="newSlug">新任务的 Slug</param>
    /// <param name="newTitle">新任务的标题（可选，不提供则使用源任务标题）</param>
    /// <returns>新创建的任务对象，失败返回 null</returns>
    Task<LanTask?> CopyTaskAsync(string sourceTaskId, string newSlug, string? newTitle = null);
}