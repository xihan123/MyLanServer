using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Data;

public class TaskRepository : ITaskRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<TaskRepository> _logger;
    private readonly ISlugGeneratorService _slugGeneratorService;

    public TaskRepository(DapperContext context, ISlugGeneratorService slugGeneratorService,
        ILogger<TaskRepository> logger)
    {
        _context = context;
        _slugGeneratorService = slugGeneratorService;
        _logger = logger;
    }

    public async Task<IEnumerable<LanTask>> GetAllTasksAsync()
    {
        using var conn = _context.CreateConnection();
        var sql = "SELECT * FROM Tasks ORDER BY rowid DESC";
        return await conn.QueryAsync<LanTask>(sql);
    }

    public async Task<LanTask?> GetTaskBySlugAsync(string slug)
    {
        using var conn = _context.CreateConnection();
        var sql = "SELECT * FROM Tasks WHERE Slug = @Slug LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<LanTask>(sql, new { Slug = slug });
    }

    public async Task<bool> CreateTaskAsync(LanTask task)
    {
        const int maxRetries = 5;
        var retryCount = 0;
        var originalSlug = task.Slug;

        while (retryCount < maxRetries)
            try
            {
                using var conn = _context.CreateConnection();
                var sql = @"
                    INSERT INTO Tasks (Id, Title, TemplatePath, TargetFolder, Slug, PasswordHash, MaxLimit, CurrentCount, ExpiryDate, IsActive, VersioningMode, DownloadsCount, IsOneTimeLink, Description, TaskType, AllowAttachmentUpload, AttachmentDownloadDescription, CreatedAt, ShowDescriptionInApi)
                    VALUES (@Id, @Title, @TemplatePath, @TargetFolder, @Slug, @PasswordHash, @MaxLimit, @CurrentCount, @ExpiryDate, @IsActive, @VersioningMode, @DownloadsCount, @IsOneTimeLink, @Description, @TaskType, @AllowAttachmentUpload, @AttachmentDownloadDescription, @CreatedAt, @ShowDescriptionInApi)";

                var rows = await conn.ExecuteAsync(sql, task);
                return rows > 0;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && retryCount < maxRetries - 1)
            {
                // 唯一约束违反（Slug 重复），生成新的 slug 并重试
                retryCount++;
                task.Slug = _slugGeneratorService.GenerateSlug();
            }

        // 所有重试都失败，恢复原始 slug 并抛出异常
        task.Slug = originalSlug;
        throw new InvalidOperationException($"Failed to create task after {maxRetries} attempts due to slug conflicts");
    }

    public async Task<bool> UpdateTaskAsync(LanTask task)
    {
        using var conn = _context.CreateConnection();
        var sql = @"
                UPDATE Tasks
                SET Title = @Title,
                    TemplatePath = @TemplatePath,
                    TargetFolder = @TargetFolder,
                    PasswordHash = @PasswordHash,
                    MaxLimit = @MaxLimit,
                    ExpiryDate = @ExpiryDate,
                    IsActive = @IsActive,
                    VersioningMode = @VersioningMode,
                    DownloadsCount = @DownloadsCount,
                    CurrentCount = @CurrentCount,
                    IsOneTimeLink = @IsOneTimeLink,
                    Description = @Description,
                    TaskType = @TaskType,
                    AllowAttachmentUpload = @AllowAttachmentUpload,
                    AttachmentDownloadDescription = @AttachmentDownloadDescription,
                    ShowDescriptionInApi = @ShowDescriptionInApi
                WHERE Id = @Id";

        var rows = await conn.ExecuteAsync(sql, task);
        return rows > 0;
    }

    public async Task<bool> DeleteTaskAsync(string taskId)
    {
        using var conn = _context.CreateConnection();
        // 级联删除在 Create Table 时已定义，但显式删除更安全
        var sql = "DELETE FROM Tasks WHERE Id = @Id";
        var rows = await conn.ExecuteAsync(sql, new { Id = taskId });
        return rows > 0;
    }

    public async Task RecordSubmissionAsync(Submission submission)
    {
        using var conn = _context.CreateConnection();
        using var trans = conn.BeginTransaction();

        try
        {
            // 1. 插入提交记录
            var insertSql = @"
                    INSERT INTO Submissions (TaskId, SubmitterName, Contact, Department, OriginalFilename, StoredFilename, ClientIP, Timestamp, AttachmentPath)
                    VALUES (@TaskId, @SubmitterName, @Contact, @Department, @OriginalFilename, @StoredFilename, @ClientIp, @Timestamp, @AttachmentPath)";

            await conn.ExecuteAsync(insertSql, submission, trans);

            // 2. 更新计数器 (原子操作，带 MaxLimit 检查)
            var updateSql = @"
                    UPDATE Tasks
                    SET CurrentCount = CurrentCount + 1
                    WHERE Id = @TaskId
                      AND (MaxLimit = 0 OR CurrentCount < MaxLimit)";

            var rowsAffected = await conn.ExecuteAsync(updateSql, new { submission.TaskId }, trans);

            // 如果没有更新任何行，说明已达到上限
            if (rowsAffected == 0)
            {
                _logger.LogWarning(
                    "Attempt to exceed submission limit: TaskId={TaskId}, Submitter={Submitter}",
                    submission.TaskId,
                    submission.SubmitterName);
                trans.Rollback();
                throw new InvalidOperationException("提交数量已达到上限");
            }

            trans.Commit();
        }
        catch
        {
            trans.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<Submission>> GetSubmissionsByTaskIdAsync(string taskId)
    {
        using var conn = _context.CreateConnection();
        var sql = "SELECT * FROM Submissions WHERE TaskId = @TaskId ORDER BY Timestamp DESC";
        return await conn.QueryAsync<Submission>(sql, new { TaskId = taskId });
    }

    public async Task<bool> IncrementDownloadsCountAsync(string taskId)
    {
        using var conn = _context.CreateConnection();
        var sql = "UPDATE Tasks SET DownloadsCount = DownloadsCount + 1 WHERE Id = @Id";
        var rows = await conn.ExecuteAsync(sql, new { Id = taskId });
        return rows > 0;
    }

    /// <summary>
    ///     删除单个提交记录
    /// </summary>
    public async Task<bool> DeleteSubmissionAsync(long submissionId)
    {
        using var conn = _context.CreateConnection();
        var sql = "DELETE FROM Submissions WHERE Id = @Id";
        var rows = await conn.ExecuteAsync(sql, new { Id = submissionId });
        return rows > 0;
    }

    /// <summary>
    ///     清空所有提交记录
    /// </summary>
    public async Task<int> ClearSubmissionsAsync(string taskId)
    {
        using var conn = _context.CreateConnection();
        var sql = "DELETE FROM Submissions WHERE TaskId = @TaskId";
        var rows = await conn.ExecuteAsync(sql, new { TaskId = taskId });
        return rows;
    }

    /// <summary>
    ///     减少当前计数
    /// </summary>
    public async Task<bool> DecrementCurrentCountAsync(string taskId)
    {
        using var conn = _context.CreateConnection();
        var sql = "UPDATE Tasks SET CurrentCount = CurrentCount - 1 WHERE Id = @Id AND CurrentCount > 0";
        var rows = await conn.ExecuteAsync(sql, new { Id = taskId });
        return rows > 0;
    }

    /// <summary>
    ///     更新当前计数
    /// </summary>
    public async Task<bool> UpdateCurrentCountAsync(string taskId, int count)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new ArgumentException("TaskId 不能为空", nameof(taskId));
        if (count < 0)
            throw new ArgumentException("Count 不能为负数", nameof(count));

        using var conn = _context.CreateConnection();
        var sql = "UPDATE Tasks SET CurrentCount = @Count WHERE Id = @Id";
        var rows = await conn.ExecuteAsync(sql, new { Id = taskId, Count = count });
        return rows > 0;
    }

    /// <summary>
    ///     复制任务（创建新任务，复制配置）
    /// </summary>
    public async Task<LanTask?> CopyTaskAsync(string sourceTaskId, string newSlug, string? newTitle = null)
    {
        const int maxRetries = 5;
        var retryCount = 0;
        var originalSlug = newSlug;

        while (retryCount < maxRetries)
            try
            {
                using var conn = _context.CreateConnection();

                // 1. 查询源任务
                var sourceTask = await conn.QueryFirstOrDefaultAsync<LanTask>(
                    "SELECT * FROM Tasks WHERE Id = @Id",
                    new { Id = sourceTaskId });

                if (sourceTask == null) return null;

                // 2. 创建新任务对象
                var newTask = new LanTask
                {
                    Id = Guid.NewGuid().ToString(),
                    Slug = newSlug,
                    Title = newTitle ?? sourceTask.Title, // 使用新标题或源任务标题
                    Description = sourceTask.Description,
                    TemplatePath = sourceTask.TemplatePath,
                    TargetFolder = sourceTask.TargetFolder,
                    PasswordHash = sourceTask.PasswordHash,
                    MaxLimit = sourceTask.MaxLimit,
                    ExpiryDate = sourceTask.ExpiryDate,
                    IsActive = sourceTask.IsActive,
                    VersioningMode = sourceTask.VersioningMode,
                    IsOneTimeLink = sourceTask.IsOneTimeLink,
                    TaskType = sourceTask.TaskType,
                    AllowAttachmentUpload = sourceTask.AllowAttachmentUpload,
                    AttachmentDownloadDescription = sourceTask.AttachmentDownloadDescription,
                    // 重置计数器
                    CurrentCount = 0,
                    DownloadsCount = 0,
                    // 重新生成创建时间
                    CreatedAt = DateTime.UtcNow
                };

                // 3. 插入新任务
                var sql = @"
                    INSERT INTO Tasks (Id, Title, TemplatePath, TargetFolder, Slug, PasswordHash,
                                      MaxLimit, CurrentCount, ExpiryDate, IsActive, VersioningMode,
                                      DownloadsCount, IsOneTimeLink, Description, TaskType,
                                      AllowAttachmentUpload, AttachmentDownloadDescription, CreatedAt)
                    VALUES (@Id, @Title, @TemplatePath, @TargetFolder, @Slug, @PasswordHash,
                            @MaxLimit, @CurrentCount, @ExpiryDate, @IsActive, @VersioningMode,
                            @DownloadsCount, @IsOneTimeLink, @Description, @TaskType,
                            @AllowAttachmentUpload, @AttachmentDownloadDescription, @CreatedAt)";

                var rows = await conn.ExecuteAsync(sql, newTask);

                return rows > 0 ? newTask : null;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && retryCount < maxRetries - 1)
            {
                // 唯一约束违反（Slug 重复），生成新的 slug 并重试
                retryCount++;
                newSlug = _slugGeneratorService.GenerateSlug();
            }

        // 所有重试都失败，抛出异常
        throw new InvalidOperationException($"Failed to copy task after {maxRetries} attempts due to slug conflicts");
    }
}