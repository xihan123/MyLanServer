using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Enums;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;
using MyLanServer.Infrastructure.Services;
using MyLanServer.UI.Services;
using MyLanServer.UI.Views;

namespace MyLanServer.UI.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IExpiryQuickOptionsService _expiryQuickOptionsService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly ISlugGeneratorService _slugGeneratorService;
    private readonly ITaskAttachmentService _taskAttachmentService;
    private readonly ITaskRepository _taskRepository;
    private readonly IWebServerService _webServerService;

    [ObservableProperty] private bool _autoRefreshEnabled;
    [ObservableProperty] private int _autoRefreshInterval = 10;
    [ObservableProperty] private string _autoRefreshStatus = "自动刷新已禁用";

    // --- 服务器状态 ---
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartServerCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopServerCommand))]
    private bool _isServerRunning;

    [ObservableProperty] private int _port = 8080;
    [ObservableProperty] private string _searchText = "";

    // --- 任务列表 ---
    [ObservableProperty] private LanTask? _selectedTask;
    [ObservableProperty] private string _serverStatusColor = "Red";
    [ObservableProperty] private string _serverStatusText = "服务器未启动";
    [ObservableProperty] private string _serverUrl = "未启动";
    [ObservableProperty] private string _statusMessage = "就绪";
    [ObservableProperty] private int _totalSubmissions;

    // --- 状态栏属性 ---
    [ObservableProperty] private int _totalTasks;

    // --- 构造函数 (DI 注入) ---
    public MainViewModel(
        IWebServerService webServerService,
        ITaskRepository taskRepository,
        ITaskAttachmentService taskAttachmentService,
        ISlugGeneratorService slugGeneratorService,
        IExpiryQuickOptionsService expiryQuickOptionsService,
        ILogger<MainViewModel> logger)
    {
        _webServerService = webServerService;
        _taskRepository = taskRepository;
        _taskAttachmentService = taskAttachmentService;
        _slugGeneratorService = slugGeneratorService;
        _expiryQuickOptionsService = expiryQuickOptionsService;
        _logger = logger;
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(AutoRefreshInterval)
        };
        _refreshTimer.Tick += async (s, e) => await LoadTasksAsync();

        // 初始化搜索防抖定时器
        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300) // 300ms 防抖延迟
        };
        _searchDebounceTimer.Tick += async (s, e) =>
        {
            _searchDebounceTimer.Stop();
            await LoadTasksAsync();
        };

        // 加载快捷时间选项
        _ = LoadExpiryQuickOptionsAsync();

        // 订阅配置变更事件（支持热更新）
        _expiryQuickOptionsService.OptionsChanged += (sender, e) =>
        {
            _logger.LogInformation("快捷时间选项配置已更新，重新加载");
            _ = LoadExpiryQuickOptionsAsync();
        };

        // 初始化加载数据
        LoadTasksCommand.Execute(null);
    }

    public ObservableCollection<LanTask> Tasks { get; } = new();

    // 快捷时间选项列表（用于右键菜单）
    public ObservableCollection<ExpiryQuickOption> ExpiryQuickOptions { get; } = new();

    // --- IDisposable 实现 ---
    public void Dispose()
    {
        // 停止定时器并取消订阅事件
        if (_refreshTimer != null)
        {
            _refreshTimer.Stop();
            _refreshTimer.Tick -= async (s, e) => await LoadTasksAsync();
        }

        if (_searchDebounceTimer != null)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Tick -= async (s, e) => await LoadTasksAsync();
        }
    }

    // --- 自动刷新配置改变 ---
    partial void OnAutoRefreshEnabledChanged(bool value)
    {
        if (value)
        {
            _refreshTimer.Interval = TimeSpan.FromSeconds(AutoRefreshInterval);
            _refreshTimer.Start();
            AutoRefreshStatus = $"自动刷新已启用 (间隔: {AutoRefreshInterval}秒)";
            StatusMessage = $"自动刷新已启用，间隔: {AutoRefreshInterval}秒";
        }
        else
        {
            _refreshTimer.Stop();
            AutoRefreshStatus = "自动刷新已禁用";
            StatusMessage = "自动刷新已禁用";
        }
    }

    partial void OnAutoRefreshIntervalChanged(int value)
    {
        if (AutoRefreshEnabled)
        {
            _refreshTimer.Interval = TimeSpan.FromSeconds(value);
            AutoRefreshStatus = $"自动刷新已启用 (间隔: {value}秒)";
            StatusMessage = $"自动刷新间隔已更新为: {value}秒";
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        // 重置防抖定时器
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    // --- 快捷时间选项管理方法 ---

    /// <summary>
    ///     加载快捷时间选项（用于右键菜单）
    /// </summary>
    private async Task LoadExpiryQuickOptionsAsync()
    {
        try
        {
            _logger.LogInformation("MainViewModel - 开始加载快捷时间选项");

            var config = await _expiryQuickOptionsService.LoadConfigAsync();
            var options = _expiryQuickOptionsService.GetOptions();

            // 清空现有选项
            ExpiryQuickOptions.Clear();

            // 添加配置文件中的选项
            foreach (var option in options)
            {
                ExpiryQuickOptions.Add(option);
                _logger.LogDebug("  加载选项: {DisplayName}, CommandParameter: {CommandParameter}",
                    option.DisplayName, option.CommandParameter);
            }

            _logger.LogInformation("MainViewModel - 快捷时间选项加载完成，共 {Count} 个选项",
                ExpiryQuickOptions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MainViewModel - 加载快捷时间选项失败: {Message}", ex.Message);
        }
    }

    // --- 服务器控制命令 ---
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartServerAsync()
    {
        try
        {
            StatusMessage = "正在启动服务...";
            await _webServerService.StartServerAsync(Port);
            IsServerRunning = true;

            var ip = GetLocalIPAddress();
            ServerUrl = $"http://{ip}:{Port}/";
            ServerStatusText = "服务器运行中";
            ServerStatusColor = "Green";
            StatusMessage = $"服务运行中: {ServerUrl}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"启动失败: {ex.Message}";
            DialogService.ShowError($"无法启动 Web 服务: {ex.Message}");
        }
    }

    private bool CanStart()
    {
        return !IsServerRunning;
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopServerAsync()
    {
        StatusMessage = "正在停止服务...";
        await _webServerService.StopServerAsync();
        IsServerRunning = false;
        ServerUrl = "未启动";
        ServerStatusText = "服务器已停止";
        ServerStatusColor = "Red";
        StatusMessage = "服务已停止";
    }

    private bool CanStop()
    {
        return IsServerRunning;
    }

    // --- 任务管理命令 ---
    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        try
        {
            var tasks = await _taskRepository.GetAllTasksAsync();

            // 根据搜索文本过滤任务（同时搜索标题、描述和Slug）
            if (!string.IsNullOrWhiteSpace(SearchText))
                tasks = tasks.Where(t =>
                    (t.Title != null &&
                     t.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (t.Description != null &&
                     t.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (t.Slug != null &&
                     t.Slug.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                ).ToList();

            // 按创建时间降序排序（最新的在前）
            tasks = tasks.OrderByDescending(t => t.CreatedAt).ToList();

            Tasks.Clear();
            foreach (var t in tasks) Tasks.Add(t);

            // 更新统计信息
            TotalTasks = tasks.Count();
            TotalSubmissions = tasks.Sum(t => t.CurrentCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tasks");
            StatusMessage = "加载任务失败";
        }
    }

    [RelayCommand]
    private async Task CreateNewTask()
    {
        // 清除选中的任务，这样TaskConfigViewModel会创建新任务
        SelectedTask = null;

        // 创建并显示任务配置对话框
        var dialog = new TaskConfigDialog(App.ServiceProvider.GetRequiredService<TaskConfigViewModel>());
        var result = dialog.ShowDialog();

        if (result == true)
            // 任务已保存，重新加载列表
            await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task DeleteTask()
    {
        if (SelectedTask == null)
        {
            DialogService.ShowWarning("请先选择要删除的任务");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedTask.Id))
        {
            _logger.LogError("DeleteTask: SelectedTask.Id is null or empty");
            DialogService.ShowError("任务ID无效，无法删除");
            return;
        }

        var taskSlug = SelectedTask.Slug ?? "未知";
        var taskId = SelectedTask.Id;

        // 使用自定义删除确认对话框
        var dialog = new DeleteConfirmDialog($"确定要删除任务 {taskSlug} 吗？\n这将同时删除所有相关的提交记录。");
        var dialogResult = dialog.ShowDialog();

        if (dialogResult == true)
            try
            {
                // 如果用户选择删除文件夹，则删除物理文件夹
                if (dialog.DeleteFolder && !string.IsNullOrWhiteSpace(SelectedTask.CollectionPath))
                    try
                    {
                        if (Directory.Exists(SelectedTask.CollectionPath))
                        {
                            Directory.Delete(SelectedTask.CollectionPath, true);
                            _logger.LogInformation("Deleted folder: {Folder}", SelectedTask.CollectionPath);
                        }
                    }
                    catch (Exception folderEx)
                    {
                        _logger.LogError(folderEx, "Failed to delete folder: {Folder}", SelectedTask.CollectionPath);
                        DialogService.ShowWarning($"删除任务成功，但删除文件夹失败：{folderEx.Message}");
                    }

                await _taskRepository.DeleteTaskAsync(taskId);
                await LoadTasksAsync();
                StatusMessage = $"已删除任务: {taskSlug}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete task with ID: {TaskId}", taskId);
                DialogService.ShowError($"删除失败: {ex.Message}\n任务ID: {taskId}");
            }
    }

    // --- 右键菜单命令 ---
    [RelayCommand]
    private async Task EditTask()
    {
        if (SelectedTask == null)
        {
            DialogService.ShowWarning("请先选择要编辑的任务");
            return;
        }

        // 创建并显示任务配置对话框
        var dialog = new TaskConfigDialog(App.ServiceProvider.GetRequiredService<TaskConfigViewModel>());
        var result = dialog.ShowDialog();

        if (result == true)
            // 任务已保存，重新加载列表
            await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task ToggleTaskActive(LanTask? task)
    {
        // 如果没有传入参数，尝试使用选中的任务
        var targetTask = task ?? SelectedTask;

        if (targetTask == null) return;

        try
        {
            // 如果是通过右键菜单触发的（task 参数不为 null），需要手动切换 IsActive 属性
            // 如果是通过 DataGrid CheckBox 触发的（task 为 null），状态已经由双向绑定改变了，不需要切换
            if (task != null) targetTask.IsActive = !targetTask.IsActive;

            // 调用 Repository 更新数据库
            var updated = await _taskRepository.UpdateTaskAsync(targetTask);

            if (updated)
            {
                // 只有当不是当前行触发的时候才弹提示，避免刷屏
                if (task == null)
                    StatusMessage = $"任务 '{targetTask.Slug}' 状态已更新";
                else
                    StatusMessage = $"任务 '{targetTask.Slug}' 已{(targetTask.IsActive ? "启用" : "禁用")}";
                // LanTask 现在实现了 INotifyPropertyChanged，属性变化会自动通知 UI
                // 不需要重新加载列表
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle task active status");
            DialogService.ShowError($"操作失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void CopyLink(LanTask? task)
    {
        var targetTask = task ?? SelectedTask;

        if (targetTask == null)
        {
            DialogService.ShowWarning("请先选择任务");
            return;
        }

        var ip = GetLocalIPAddress();
        var page = targetTask.TaskType == TaskType.DataCollection ? "distribution.html" : "task.html";
        var link = $"http://{ip}:{Port}/{page}?slug={targetTask.Slug}";
        Clipboard.SetText(link);
        StatusMessage = "链接已复制到剪贴板";
    }

    [RelayCommand]
    private void CopyServerUrl()
    {
        if (!IsServerRunning)
        {
            DialogService.ShowWarning("服务器未启动");
            return;
        }

        Clipboard.SetText(ServerUrl);
        StatusMessage = "服务器地址已复制到剪贴板";
    }

    [RelayCommand]
    private void OpenShareLink()
    {
        if (SelectedTask == null)
        {
            DialogService.ShowWarning("请先选择任务");
            return;
        }

        var ip = GetLocalIPAddress();
        var page = SelectedTask.TaskType == TaskType.DataCollection ? "distribution.html" : "task.html";
        var link = $"http://{ip}:{Port}/{page}?slug={SelectedTask.Slug}";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link,
                UseShellExecute = true
            });
            StatusMessage = "已打开分享网页地址";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open share link");
            DialogService.ShowError($"打开链接失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RemovePassword()
    {
        if (SelectedTask == null)
        {
            DialogService.ShowWarning("请先选择任务");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedTask.PasswordHash))
        {
            DialogService.ShowInfo("该任务没有设置密码");
            return;
        }

        try
        {
            SelectedTask.PasswordHash = null;
            var updated = await _taskRepository.UpdateTaskAsync(SelectedTask);

            if (updated)
                StatusMessage = $"任务 '{SelectedTask.Slug}' 密码已取消";
            // LanTask 现在实现了 INotifyPropertyChanged，属性变化会自动通知 UI
            // 不需要重新加载列表
            else
                DialogService.ShowError("更新失败");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove password");
            DialogService.ShowError($"操作失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RemoveLimit()
    {
        if (SelectedTask == null)
        {
            DialogService.ShowWarning("请先选择任务");
            return;
        }

        if (SelectedTask.MaxLimit == 0)
        {
            DialogService.ShowInfo("该任务没有设置提交上限");
            return;
        }

        try
        {
            SelectedTask.MaxLimit = 0;
            var updated = await _taskRepository.UpdateTaskAsync(SelectedTask);

            if (updated)
                StatusMessage = $"任务 '{SelectedTask.Slug}' 提交上限已取消";
            // LanTask 现在实现了 INotifyPropertyChanged，属性变化会自动通知 UI
            // 不需要重新加载列表
            else
                DialogService.ShowError("更新失败");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove limit");
            DialogService.ShowError($"操作失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExtendExpiry(object? parameter)
    {
        if (SelectedTask == null)
        {
            DialogService.ShowWarning("请先选择任务");
            return;
        }

        // 尝试将参数转换为字符串
        if (parameter is not string strTime)
        {
            DialogService.ShowError("无效的时间参数");
            return;
        }

        try
        {
            // 解析时间参数
            var valueStr = strTime.TrimEnd('m', 'h', 'd', 'M');
            if (!int.TryParse(valueStr, out var value))
            {
                DialogService.ShowError("无效的时间参数");
                return;
            }

            var unit = strTime[^1]; // 获取最后一个字符作为单位
            DateTime newExpiryDate;

            // 如果是长期任务，从当前时间开始计算
            var baseDate = SelectedTask.ExpiryDate ?? DateTime.Now;

            switch (unit)
            {
                case 'm': // 分钟
                    newExpiryDate = baseDate.AddMinutes(value);
                    StatusMessage = $"任务 '{SelectedTask.Slug}' 过期时间已延长 {value} 分钟";
                    break;
                case 'h': // 小时
                    newExpiryDate = baseDate.AddHours(value);
                    StatusMessage = $"任务 '{SelectedTask.Slug}' 过期时间已延长 {value} 小时";
                    break;
                case 'd': // 天
                    newExpiryDate = baseDate.AddDays(value);
                    StatusMessage = $"任务 '{SelectedTask.Slug}' 过期时间已延长 {value} 天";
                    break;
                case 'M': // 月
                    newExpiryDate = baseDate.AddMonths(value);
                    StatusMessage = $"任务 '{SelectedTask.Slug}' 过期时间已延长 {value} 个月";
                    break;
                default:
                    DialogService.ShowError("不支持的时间单位");
                    return;
            }

            SelectedTask.ExpiryDate = newExpiryDate;
            var updated = await _taskRepository.UpdateTaskAsync(SelectedTask);

            if (updated)
            {
                // LanTask 现在实现了 INotifyPropertyChanged，属性变化会自动通知 UI
                // 不需要重新加载列表
            }
            else
            {
                DialogService.ShowError("更新失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend expiry");
            DialogService.ShowError($"操作失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task AddExpiryDuration(string durationStr)
    {
        // 依赖 SelectedTask (右键 DataGrid Row 会自动选中该行)
        if (SelectedTask == null) return;
        await ProcessExtendExpiry(SelectedTask, durationStr);
    }

    private async Task ProcessExtendExpiry(LanTask task, string strTime)
    {
        try
        {
            var valueStr = strTime.TrimEnd('m', 'h', 'd', 'M');
            if (!int.TryParse(valueStr, out var value)) return;

            var unit = strTime[^1];

            // 确定延期基准时间
            DateTime baseDate;

            if (task.ExpiryDate == null)
                // 长期任务，从当前时间开始
                baseDate = DateTime.Now;
            else if (task.ExpiryDate.Value > DateTime.Now)
                // 未过期，在原过期时间上累加
                baseDate = task.ExpiryDate.Value;
            else
                // 已过期，从当前时间开始计算
                baseDate = DateTime.Now;

            var newExpiryDate = unit switch
            {
                'm' => baseDate.AddMinutes(value),
                'h' => baseDate.AddHours(value),
                'd' => baseDate.AddDays(value),
                'M' => baseDate.AddMonths(value),
                _ => baseDate
            };

            task.ExpiryDate = newExpiryDate;
            await _taskRepository.UpdateTaskAsync(task);
            StatusMessage = $"任务 '{task.Slug}' 过期时间更新为: {newExpiryDate:MM-dd HH:mm}";
            // LanTask 现在实现了 INotifyPropertyChanged，属性变化会自动通知 UI
            // 不需要重新加载列表
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove expiry");
            DialogService.ShowError($"操作失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RemoveExpiry(LanTask? task)
    {
        var targetTask = task ?? SelectedTask;
        if (targetTask == null) return;

        try
        {
            targetTask.ExpiryDate = null;
            await _taskRepository.UpdateTaskAsync(targetTask);
            StatusMessage = $"任务 '{targetTask.Slug}' 已设为长期任务";
            // LanTask 现在实现了 INotifyPropertyChanged，属性变化会自动通知 UI
            // 不需要重新加载列表
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove expiry");
        }
    }

    [RelayCommand]
    private void OpenFolder(LanTask? task)
    {
        // 使用传入的 task 参数，如果没有则使用选中的任务
        var targetTask = task ?? SelectedTask;

        if (targetTask == null)
            return;

        try
        {
            var folderPath = targetTask.CollectionPath;

            // 检查文件夹是否存在
            if (!Directory.Exists(folderPath))
            {
                _logger.LogWarning("Folder does not exist: {Path}", folderPath);
                DialogService.ShowError("文件夹不存在，请先上传文件");
                return;
            }

            Process.Start("explorer.exe", folderPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open folder: {Path}", targetTask.CollectionPath);
            DialogService.ShowError($"打开文件夹失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenMergeDialog(LanTask? task = null)
    {
        // 如果传入了任务参数，更新选中的任务
        if (task != null) SelectedTask = task;

        // 创建并显示合并对话框
        var dialog = new MergeDialog(App.ServiceProvider.GetRequiredService<MergeDialogViewModel>());
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenDepartmentManagement()
    {
        // 创建并显示部门管理对话框
        var dialog = new DepartmentManagementDialog(App.ServiceProvider.GetRequiredService<DepartmentViewModel>());
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenPersonManagement()
    {
        // 创建并显示人员管理对话框
        var dialog = new PersonManagementDialog(App.ServiceProvider);
        dialog.ShowDialog();
    }

    // 显示提交人列表
    [RelayCommand]
    private async Task ShowSubmissionsList(LanTask? task)
    {
        if (task == null)
        {
            DialogService.ShowWarning("请先选择任务");
            return;
        }

        try
        {
            var viewModel = App.ServiceProvider.GetRequiredService<SubmissionListViewModel>();
            await viewModel.InitializeAsync(task);

            var dialog = new SubmissionListDialog(viewModel);
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show submissions list");
            DialogService.ShowError($"加载提交列表失败: {ex.Message}");
        }
    }

    // 取消一次性限制
    [RelayCommand]
    private async Task RemoveOneTimeLimit(LanTask? task)
    {
        // 如果是从右键菜单传参（CommandParameter），使用参数；否则使用选中的任务
        var targetTask = task ?? SelectedTask;
        if (targetTask == null) return;

        if (!targetTask.IsOneTimeLink) return;

        try
        {
            targetTask.IsOneTimeLink = false;
            var updated = await _taskRepository.UpdateTaskAsync(targetTask);

            if (updated) StatusMessage = $"任务 '{targetTask.Slug}' 已取消一次性限制";
            // LanTask 现在实现了 INotifyPropertyChanged，属性变化会自动通知 UI
            // 不需要重新加载列表
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove one-time limit");
        }
    }


    // 复制任务命令（用于右键菜单 - 复刻分发）
    [RelayCommand]
    public async Task CopyTaskAsync(LanTask? task)
    {
        if (task == null) return;

        try
        {
            StatusMessage = "正在复刻分发...";

            // 1. 弹出输入对话框，让用户输入新标题
            var newTitle = DialogService.ShowInputDialog(
                "请输入新任务的标题",
                "复刻分发",
                "任务标题",
                task.Title + " (副本)"
            );

            if (string.IsNullOrWhiteSpace(newTitle))
            {
                DialogService.ShowWarning("请输入任务标题", "提示");
                return;
            }

            // 2. 生成唯一 Slug
            var newSlug = _slugGeneratorService.GenerateSlug();

            // 3. 复制任务配置（使用新标题）
            var newTask = await _taskRepository.CopyTaskAsync(task.Id, newSlug, newTitle);
            if (newTask == null)
            {
                DialogService.ShowError("复制任务失败");
                return;
            }

            // 4. 创建目录结构（不复制模板文件，直接使用源任务的模板路径）
            var directories = new List<string>
            {
                newTask.ConfigPath,
                newTask.FileCollectionPath,
                newTask.DataCollectionPath
            };

            // 只在需要时创建 attachments 文件夹
            if (newTask.TaskType == TaskType.DataCollection && newTask.AllowAttachmentUpload)
                directories.Add(newTask.AttachmentsPath);

            DirectoryHelper.EnsureDirectoriesExist(directories.ToArray());

            // 5. 复制附件文件（如果允许上传附件）
            if (task.AllowAttachmentUpload)
                await _taskAttachmentService.CopyAttachmentsAsync(
                    task.Id,
                    newTask.Id,
                    task.AttachmentsPath,
                    newTask.AttachmentsPath
                );

            // 6. 刷新任务列表
            await LoadTasksCommand.ExecuteAsync(null);

            // 7. 生成下载链接
            var ip = GetLocalIPAddress();
            var page = newTask.TaskType == TaskType.DataCollection ? "distribution.html" : "task.html";
            var link = $"http://{ip}:{Port}/{page}?slug={newSlug}";

            // 8. 在显示对话框前复制链接到剪贴板（避免剪贴板冲突）
            try
            {
                Clipboard.SetText(link);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy link to clipboard");
            }

            // 9. 显示成功提示
            DialogService.ShowInfo("任务复制成功", "复制成功");

            StatusMessage = $"已复制任务: {newSlug}";
            _logger.LogInformation("Task copied successfully: {OldSlug} -> {NewSlug}", task.Slug, newSlug);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy task: {Message}", ex.Message);
            DialogService.ShowError($"复制任务失败: {ex.Message}");
            StatusMessage = "复制任务失败";
        }
    }

    // 复制任务命令（用于另存为，支持自定义标题）
    public async Task CopyTaskAsync(LanTask? task, string? newTitle = null)
    {
        if (task == null) return;

        try
        {
            StatusMessage = "正在复制任务...";

            // 1. 生成唯一 Slug
            var newSlug = _slugGeneratorService.GenerateSlug();

            // 2. 复制任务配置（使用新标题）
            var newTask = await _taskRepository.CopyTaskAsync(task.Id, newSlug, newTitle);
            if (newTask == null)
            {
                DialogService.ShowError("复制任务失败");
                return;
            }

            // 3. 创建目录结构（不复制模板文件，直接使用源任务的模板路径）
            var directories = new List<string>
            {
                newTask.ConfigPath,
                newTask.FileCollectionPath,
                newTask.DataCollectionPath
            };

            // 只在需要时创建 attachments 文件夹
            if (newTask.TaskType == TaskType.DataCollection && newTask.AllowAttachmentUpload)
                directories.Add(newTask.AttachmentsPath);

            DirectoryHelper.EnsureDirectoriesExist(directories.ToArray());

            // 4. 复制附件文件（如果允许上传附件）
            if (task.AllowAttachmentUpload)
                await _taskAttachmentService.CopyAttachmentsAsync(
                    task.Id,
                    newTask.Id,
                    task.AttachmentsPath,
                    newTask.AttachmentsPath
                );

            // 5. 刷新任务列表
            await LoadTasksCommand.ExecuteAsync(null);

            // 6. 生成下载链接
            var ip = GetLocalIPAddress();
            var page = newTask.TaskType == TaskType.DataCollection ? "distribution.html" : "task.html";
            var link = $"http://{ip}:{Port}/{page}?slug={newSlug}";

            // 7. 在显示对话框前复制链接到剪贴板（避免剪贴板冲突）
            try
            {
                Clipboard.SetText(link);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy link to clipboard");
            }

            // 8. 显示成功提示
            DialogService.ShowInfo("任务复制成功", "复制成功");

            StatusMessage = $"已复制任务: {newSlug}";
            _logger.LogInformation("Task copied successfully: {OldSlug} -> {NewSlug}", task.Slug, newSlug);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy task: {Message}", ex.Message);
            DialogService.ShowError($"复制任务失败: {ex.Message}");
            StatusMessage = "复制任务失败";
        }
    }

    // --- 辅助方法 ---
    private string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
        }
        catch
        {
            // 忽略异常
        }

        return "127.0.0.1";
    }
}