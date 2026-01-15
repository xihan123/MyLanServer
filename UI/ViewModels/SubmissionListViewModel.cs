using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;
using MyLanServer.UI.Services;
using MyLanServer.UI.Views;

namespace MyLanServer.UI.ViewModels;

/// <summary>
///     提交人列表视图模型
/// </summary>
public partial class SubmissionListViewModel : ObservableObject
{
    private readonly ILogger<SubmissionListViewModel> _logger;
    private readonly ITaskRepository _taskRepository;
    private Action<bool>? _closeDialogCallback;
    [ObservableProperty] private LanTask? _currentTask;
    [ObservableProperty] private ObservableCollection<Submission> _filteredSubmissions = new();
    private DispatcherTimer? _searchDebounceTimer;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ObservableCollection<Submission> _submissions = new();
    [ObservableProperty] private string _taskId = string.Empty;

    [ObservableProperty] private string _taskTitle = string.Empty;
    [ObservableProperty] private int _totalCount;

    public SubmissionListViewModel(ILogger<SubmissionListViewModel> logger, ITaskRepository taskRepository)
    {
        _logger = logger;
        _taskRepository = taskRepository;
    }

    /// <summary>
    ///     初始化视图模型，加载提交记录
    /// </summary>
    public async Task InitializeAsync(LanTask task)
    {
        TaskTitle = task.Title ?? "未知任务";
        TaskId = task.Id;
        CurrentTask = task;

        await LoadSubmissionsAsync();
        InitializeSearchDebouncer();
    }

    /// <summary>
    ///     加载提交记录
    /// </summary>
    private async Task LoadSubmissionsAsync()
    {
        try
        {
            var submissions = await _taskRepository.GetSubmissionsByTaskIdAsync(TaskId);

            Submissions.Clear();
            foreach (var submission in submissions) Submissions.Add(submission);

            FilteredSubmissions.Clear();
            foreach (var submission in submissions) FilteredSubmissions.Add(submission);

            TotalCount = Submissions.Count;

            _logger.LogInformation("已加载 {Count} 条提交记录", TotalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载提交记录失败");
            throw;
        }
    }

    /// <summary>
    ///     初始化搜索防抖定时器
    /// </summary>
    private void InitializeSearchDebouncer()
    {
        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _searchDebounceTimer.Tick += (s, e) =>
        {
            _searchDebounceTimer?.Stop();
            FilterSubmissions();
        };
    }

    /// <summary>
    ///     搜索文本变化时触发
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Start();
    }

    /// <summary>
    ///     根据搜索文本过滤提交记录
    /// </summary>
    private void FilterSubmissions()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredSubmissions.Clear();
            foreach (var submission in Submissions) FilteredSubmissions.Add(submission);

            return;
        }

        var searchLower = SearchText.ToLowerInvariant();

        var filtered = Submissions.Where(s =>
            s.SubmitterName.ToLowerInvariant().Contains(searchLower) ||
            s.Contact.ToLowerInvariant().Contains(searchLower) ||
            (s.Department?.ToLowerInvariant().Contains(searchLower) ?? false)
        ).ToList();

        FilteredSubmissions.Clear();
        foreach (var submission in filtered) FilteredSubmissions.Add(submission);

        _logger.LogInformation("搜索 '{SearchText}' 找到 {Count} 条记录", SearchText, FilteredSubmissions.Count);
    }

    /// <summary>
    ///     打开文件夹并选中文件
    /// </summary>
    [RelayCommand]
    private void OpenFileAndSelect(Submission? submission)
    {
        if (submission == null || CurrentTask == null) return;

        try
        {
            var filePath = Path.Combine(CurrentTask.CollectionPath, submission.StoredFilename);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File does not exist: {Path}", filePath);
                DialogService.ShowError("文件不存在");
                return;
            }

            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            _logger.LogInformation("Opened folder and selected file: {Path}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file: {Path}", submission.StoredFilename);
            DialogService.ShowError($"打开文件失败: {ex.Message}");
        }
    }

    /// <summary>
    ///     删除单个提交记录
    /// </summary>
    [RelayCommand]
    private async Task DeleteSubmission(Submission? submission)
    {
        if (submission == null || CurrentTask == null) return;

        var message = $"确定要删除 {submission.SubmitterName} 的提交记录吗？\n" +
                      $"文件名：{submission.OriginalFilename}";

        var dialog = new DeleteConfirmDialog(message);
        var dialogResult = dialog.ShowDialog();

        if (dialogResult == true)
            try
            {
                // 如果用户选择删除文件，则删除物理文件
                if (dialog.DeleteFolder)
                {
                    var filePath = Path.Combine(CurrentTask.CollectionPath, submission.StoredFilename);
                    if (File.Exists(filePath)) File.Delete(filePath);

                    // 删除附件文件夹（如果有）
                    if (!string.IsNullOrWhiteSpace(submission.AttachmentPath))
                        try
                        {
                            // 解析 JSON 数组
                            var attachmentPaths = JsonSerializer.Deserialize<List<string>>(submission.AttachmentPath);
                            if (attachmentPaths != null && attachmentPaths.Count > 0)
                            {
                                // 从第一个附件路径提取文件夹路径
                                var folderPath = Path.GetDirectoryName(attachmentPaths[0]);
                                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                                {
                                    Directory.Delete(folderPath, true);
                                    _logger.LogInformation("Deleted attachment folder: {Folder}", folderPath);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to delete attachment folder for submission: {Submitter}",
                                submission.SubmitterName);
                        }
                }

                // 删除数据库记录并更新任务计数器
                await _taskRepository.DeleteSubmissionAsync(submission.Id);
                await _taskRepository.DecrementCurrentCountAsync(CurrentTask.Id);

                await LoadSubmissionsAsync();
                _logger.LogInformation("Deleted submission: {Submitter}", submission.SubmitterName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete submission");
                DialogService.ShowError($"删除失败: {ex.Message}");
            }
    }

    /// <summary>
    ///     清空所有提交记录
    /// </summary>
    [RelayCommand]
    private async Task ClearAllSubmissions()
    {
        if (CurrentTask == null) return;

        if (Submissions.Count == 0)
        {
            DialogService.ShowInfo("没有提交记录可清空");
            return;
        }

        var message = $"确定要清空所有提交记录吗？\n" +
                      $"共 {Submissions.Count} 条记录将被删除。";

        var dialog = new DeleteConfirmDialog(message);
        var dialogResult = dialog.ShowDialog();

        if (dialogResult == true)
            try
            {
                // 如果用户选择删除文件，则删除所有物理文件
                if (dialog.DeleteFolder)
                    foreach (var submission in Submissions)
                    {
                        var filePath = Path.Combine(CurrentTask.CollectionPath, submission.StoredFilename);
                        if (File.Exists(filePath)) File.Delete(filePath);

                        // 删除附件文件夹（如果有）
                        if (!string.IsNullOrWhiteSpace(submission.AttachmentPath))
                            try
                            {
                                // 解析 JSON 数组
                                var attachmentPaths =
                                    JsonSerializer.Deserialize<List<string>>(submission.AttachmentPath);
                                if (attachmentPaths != null && attachmentPaths.Count > 0)
                                {
                                    // 从第一个附件路径提取文件夹路径
                                    var folderPath = Path.GetDirectoryName(attachmentPaths[0]);
                                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                                    {
                                        Directory.Delete(folderPath, true);
                                        _logger.LogInformation("Deleted attachment folder: {Folder}", folderPath);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete attachment folder for submission: {Submitter}",
                                    submission.SubmitterName);
                            }
                    }

                // 删除数据库记录并更新任务计数器
                await _taskRepository.ClearSubmissionsAsync(CurrentTask.Id);
                await _taskRepository.UpdateCurrentCountAsync(CurrentTask.Id, 0);

                await LoadSubmissionsAsync();
                _logger.LogInformation("Cleared all submissions for task: {TaskId}", CurrentTask.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear submissions");
                DialogService.ShowError($"清空失败: {ex.Message}");
            }
    }

    /// <summary>
    ///     关闭对话框
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        _closeDialogCallback?.Invoke(false);
    }

    /// <summary>
    ///     设置关闭回调
    /// </summary>
    public void SetCloseCallback(Action<bool> callback)
    {
        _closeDialogCallback = callback;
    }
}