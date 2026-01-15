using System.IO;
using Dapper;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;
using MyLanServer.Infrastructure.Data;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     任务附件服务实现
/// </summary>
public class TaskAttachmentService : ITaskAttachmentService
{
    private readonly DapperContext _context;
    private readonly ILogger<TaskAttachmentService> _logger;
    private readonly ITaskRepository _taskRepository;

    public TaskAttachmentService(
        DapperContext context,
        ITaskRepository taskRepository,
        ILogger<TaskAttachmentService> logger)
    {
        _context = context;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    /// <summary>
    ///     获取任务的所有附件
    /// </summary>
    public async Task<List<TaskAttachment>> GetAttachmentsByTaskIdAsync(string taskId)
    {
        using var conn = _context.CreateConnection();
        var sql = "SELECT * FROM TaskAttachments WHERE TaskId = @TaskId ORDER BY SortOrder, UploadDate";
        var result = await conn.QueryAsync<TaskAttachment>(sql, new { TaskId = taskId });
        return result.ToList();
    }

    /// <summary>
    ///     根据任务 Slug 获取附件列表（用于 API）
    /// </summary>
    public async Task<List<TaskAttachmentDto>> GetAttachmentsBySlugAsync(string slug)
    {
        var task = await _taskRepository.GetTaskBySlugAsync(slug);
        if (task == null) return new List<TaskAttachmentDto>();

        var attachments = await GetAttachmentsByTaskIdAsync(task.Id);
        return attachments.Select(a => new TaskAttachmentDto
        {
            Id = a.Id,
            FileName = a.FileName,
            DisplayName = a.DisplayName,
            FileSize = a.FileSize,
            UploadDate = a.UploadDate,
            SortOrder = a.SortOrder
        }).ToList();
    }

    /// <summary>
    ///     添加附件到任务
    /// </summary>
    public async Task<TaskAttachment> AddAttachmentAsync(TaskAttachment attachment)
    {
        using var conn = _context.CreateConnection();
        var sql = @"
            INSERT INTO TaskAttachments (TaskId, FileName, FilePath, DisplayName, Description, FileSize, UploadDate, SortOrder)
            VALUES (@TaskId, @FileName, @FilePath, @DisplayName, @Description, @FileSize, @UploadDate, @SortOrder);
            SELECT last_insert_rowid();";

        var id = await conn.ExecuteScalarAsync<int>(sql, attachment);
        attachment.Id = id;
        _logger.LogInformation("Added attachment {Id} to task {TaskId}", id, attachment.TaskId);
        return attachment;
    }

    /// <summary>
    ///     删除附件
    /// </summary>
    public async Task<bool> DeleteAttachmentAsync(int attachmentId)
    {
        using var conn = _context.CreateConnection();

        // 先获取文件路径
        var getSql = "SELECT FilePath, TaskId FROM TaskAttachments WHERE Id = @Id";
        var attachment =
            await conn.QueryFirstOrDefaultAsync<(string FilePath, string TaskId)>(getSql, new { Id = attachmentId });

        if (attachment.FilePath == null) return false;

        // 删除数据库记录
        var deleteSql = "DELETE FROM TaskAttachments WHERE Id = @Id";
        var rows = await conn.ExecuteAsync(deleteSql, new { Id = attachmentId });

        // 删除物理文件
        if (rows > 0 && File.Exists(attachment.FilePath))
            try
            {
                File.Delete(attachment.FilePath);
                _logger.LogInformation("Deleted attachment file: {FilePath}", attachment.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete attachment file: {FilePath}", attachment.FilePath);
            }

        return rows > 0;
    }

    /// <summary>
    ///     更新附件信息
    /// </summary>
    public async Task<bool> UpdateAttachmentAsync(TaskAttachment attachment)
    {
        using var conn = _context.CreateConnection();
        var sql = @"
            UPDATE TaskAttachments
            SET DisplayName = @DisplayName,
                SortOrder = @SortOrder
            WHERE Id = @Id";
        var rows = await conn.ExecuteAsync(sql, attachment);
        return rows > 0;
    }

    /// <summary>
    ///     更新附件排序
    /// </summary>
    public async Task<bool> UpdateAttachmentsOrderAsync(List<TaskAttachment> attachments)
    {
        using var conn = _context.CreateConnection();
        using var trans = conn.BeginTransaction();
        try
        {
            foreach (var attachment in attachments)
            {
                var sql = "UPDATE TaskAttachments SET SortOrder = @SortOrder WHERE Id = @Id";
                await conn.ExecuteAsync(sql, attachment, trans);
            }

            trans.Commit();
            return true;
        }
        catch
        {
            trans.Rollback();
            return false;
        }
    }

    /// <summary>
    ///     复制文件到目标目录并返回路径
    /// </summary>
    public async Task<string> CopyFileToTaskFolderAsync(string sourcePath, string targetFolder, string fileName)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("源文件不存在", sourcePath);

        // 确保目标目录存在
        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

        // 直接使用原文件名，如果文件已存在则覆盖
        var destPath = Path.Combine(targetFolder, fileName);

        // 复制文件（覆盖已存在的文件）
        await Task.Run(() => File.Copy(sourcePath, destPath, true));

        _logger.LogInformation("Copied file from {Source} to {Dest}", sourcePath, destPath);
        return destPath;
    }

    /// <summary>
    ///     复制任务的所有附件到新任务
    /// </summary>
    public async Task<int> CopyAttachmentsAsync(
        string sourceTaskId,
        string targetTaskId,
        string sourceAttachmentsPath,
        string targetAttachmentsPath)
    {
        using var conn = _context.CreateConnection();

        // 1. 确保目标目录存在
        if (!Directory.Exists(targetAttachmentsPath)) Directory.CreateDirectory(targetAttachmentsPath);

        // 2. 查询源任务的所有附件
        var sourceAttachments = await conn.QueryAsync<TaskAttachment>(
            "SELECT * FROM TaskAttachments WHERE TaskId = @TaskId ORDER BY SortOrder",
            new { TaskId = sourceTaskId });

        var attachmentList = sourceAttachments.ToList();
        if (attachmentList.Count == 0) return 0;

        var copiedCount = 0;
        var lockObj = new object();

        // 3. 并行复制每个附件
        await Parallel.ForEachAsync(attachmentList, async (attachment, cancellationToken) =>
        {
            var sourceFilePath = Path.Combine(sourceAttachmentsPath, attachment.FileName);

            if (!File.Exists(sourceFilePath))
            {
                _logger.LogWarning("Attachment file not found: {FilePath}", sourceFilePath);
                return;
            }

            // 复制物理文件（使用共享读取模式，避免"文件被另一个进程使用"错误）
            var destFilePath = Path.Combine(targetAttachmentsPath, attachment.FileName);
            using var sourceStream =
                new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var destStream = new FileStream(destFilePath, FileMode.Create, FileAccess.Write);
            await sourceStream.CopyToAsync(destStream, cancellationToken);

            // 插入新附件记录（需要在事务中执行）
            var newAttachment = new TaskAttachment
            {
                TaskId = targetTaskId,
                FileName = attachment.FileName,
                FilePath = destFilePath,
                DisplayName = attachment.DisplayName,
                FileSize = attachment.FileSize,
                UploadDate = DateTime.UtcNow, // 使用当前时间
                SortOrder = attachment.SortOrder
            };

            var sql = @"
                INSERT INTO TaskAttachments (TaskId, FileName, FilePath, DisplayName,
                                            FileSize, UploadDate, SortOrder)
                VALUES (@TaskId, @FileName, @FilePath, @DisplayName,
                        @FileSize, @UploadDate, @SortOrder);
                SELECT last_insert_rowid();";

            await conn.ExecuteScalarAsync<int>(sql, newAttachment);

            lock (lockObj)
            {
                copiedCount++;
            }
        });

        _logger.LogInformation("Copied {Count} attachments from task {SourceTaskId} to {TargetTaskId}",
            copiedCount, sourceTaskId, targetTaskId);

        return copiedCount;
    }
}