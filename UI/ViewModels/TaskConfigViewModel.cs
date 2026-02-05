using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MyLanServer.Core.Enums;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;
using MyLanServer.Infrastructure.Services;
using MyLanServer.UI.Services;

namespace MyLanServer.UI.ViewModels;

// 预设扩展名分组
public class PresetExtensionGroup
{
    public string CategoryName { get; set; } = string.Empty;
    public ObservableCollection<FileExtensionInfo> Extensions { get; set; } = new();
}

public partial class TaskConfigViewModel : ObservableObject
{
    // 预设扩展名常量
    private static readonly List<FileExtensionInfo> PresetExtensions = new()
    {
        new FileExtensionInfo { Extension = ".pdf", DisplayName = "PDF 文档", Category = "文档", IsPreset = true },
        new FileExtensionInfo { Extension = ".doc", DisplayName = "Word 文档", Category = "文档", IsPreset = true },
        new FileExtensionInfo { Extension = ".docx", DisplayName = "Word 文档", Category = "文档", IsPreset = true },
        new FileExtensionInfo { Extension = ".xls", DisplayName = "Excel 表格", Category = "文档", IsPreset = true },
        new FileExtensionInfo { Extension = ".xlsx", DisplayName = "Excel 表格", Category = "文档", IsPreset = true },
        new FileExtensionInfo { Extension = ".txt", DisplayName = "文本文件", Category = "文档", IsPreset = true },
        new FileExtensionInfo { Extension = ".jpg", DisplayName = "JPEG 图片", Category = "图片", IsPreset = true },
        new FileExtensionInfo { Extension = ".jpeg", DisplayName = "JPEG 图片", Category = "图片", IsPreset = true },
        new FileExtensionInfo { Extension = ".png", DisplayName = "PNG 图片", Category = "图片", IsPreset = true },
        new FileExtensionInfo { Extension = ".gif", DisplayName = "GIF 图片", Category = "图片", IsPreset = true },
        new FileExtensionInfo { Extension = ".bmp", DisplayName = "BMP 图片", Category = "图片", IsPreset = true },
        new FileExtensionInfo { Extension = ".zip", DisplayName = "ZIP 压缩包", Category = "压缩包", IsPreset = true },
        new FileExtensionInfo { Extension = ".rar", DisplayName = "RAR 压缩包", Category = "压缩包", IsPreset = true },
        new FileExtensionInfo { Extension = ".7z", DisplayName = "7Z 压缩包", Category = "压缩包", IsPreset = true }
    };

    private readonly IExpiryQuickOptionsService _expiryQuickOptionsService;

    private readonly IFileExtensionService _fileExtensionService;

    private readonly ILogger<TaskConfigViewModel> _logger;
    private readonly MainViewModel _mainViewModel;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ISlugGeneratorService _slugGeneratorService;
    private readonly ITaskAttachmentService _taskAttachmentService;
    private readonly ITaskRepository _taskRepository;

    // 附件上传相关
    [ObservableProperty] private bool _allowAttachmentUpload;

    [ObservableProperty] private string? _attachmentDownloadDescription;

    private Action<bool>? _closeDialogCallback;

    // 当前提交数量
    [ObservableProperty] private int _currentCount;
    [ObservableProperty] private string _customExtensionInput = string.Empty;

    // 下载计数（用于一次性下载任务）
    [ObservableProperty] private int _downloadsCount;
    [ObservableProperty] private DateTime? _expiryDate = DateTime.Now;
    [ObservableProperty] private ObservableCollection<string> _extensionCategories = new();

    /// <summary>
    ///     过滤后的扩展名列表（用于UI显示）
    /// </summary>
    [ObservableProperty] private ObservableCollection<FileExtensionInfo> _filteredExtensions = new();

    [ObservableProperty] private string _generatedLink = string.Empty;

    // 时间部分的独立属性（用于双向绑定）
    [ObservableProperty] private int _hour;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private bool _isAllExtensionsSelected;
    [ObservableProperty] private bool _isAttachmentUploadEnabled; // 用于 UI 绑定

    [ObservableProperty] private bool _isLongTermTask;
    [ObservableProperty] private bool _isOneTimeLink;
    [ObservableProperty] private int _maxLimit; // 0 表示不限制
    [ObservableProperty] private int _minute;

    [ObservableProperty] private bool _requirePassword;
    [ObservableProperty] private int _second;
    [ObservableProperty] private string _selectedCategoryFilter = "全部";

    [ObservableProperty] private ExpiryQuickOption? _selectedExpiryQuickOption;

    [ObservableProperty] private bool _showDescriptionInApi;
    [ObservableProperty] private string _targetFolder = string.Empty;

    // 任务附件相关
    [ObservableProperty] private ObservableCollection<TaskAttachment> _taskAttachments = new();
    [ObservableProperty] private string _taskDescription = string.Empty;

    // 扩展名管理相关
    [ObservableProperty] private ObservableCollection<FileExtensionInfo> _taskExtensions = new(); // 任务的所有扩展名（预设 + 自定义）
    [ObservableProperty] private string? _taskPassword;
    [ObservableProperty] private string _taskSlug = string.Empty;

    // 任务标题（新增）
    [ObservableProperty] private string _taskTitle = string.Empty;

    // 任务类型（新增）
    [ObservableProperty] private TaskType _taskType = TaskType.FileCollection;
    [ObservableProperty] private string _templatePath = string.Empty;
    [ObservableProperty] private VersioningMode _versioningMode = VersioningMode.AutoVersion;

    public TaskConfigViewModel(
        ITaskRepository taskRepository,
        MainViewModel mainViewModel,
        IPasswordHashService passwordHashService,
        ITaskAttachmentService taskAttachmentService,
        IFileExtensionService fileExtensionService,
        ISlugGeneratorService slugGeneratorService,
        IExpiryQuickOptionsService expiryQuickOptionsService,
        ILogger<TaskConfigViewModel> logger)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
        _passwordHashService = passwordHashService ?? throw new ArgumentNullException(nameof(passwordHashService));
        _taskAttachmentService =
            taskAttachmentService ?? throw new ArgumentNullException(nameof(taskAttachmentService));
        _fileExtensionService = fileExtensionService ?? throw new ArgumentNullException(nameof(fileExtensionService));
        _slugGeneratorService = slugGeneratorService ?? throw new ArgumentNullException(nameof(slugGeneratorService));
        _expiryQuickOptionsService = expiryQuickOptionsService ??
                                     throw new ArgumentNullException(nameof(expiryQuickOptionsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // 从配置文件加载快捷时间选项
        _ = LoadExpiryQuickOptionsAsync();

        // 订阅配置变更事件（支持热更新）
        _expiryQuickOptionsService.OptionsChanged += (sender, e) =>
        {
            _logger.LogInformation("快捷时间选项配置已更新，重新加载");
            _ = LoadExpiryQuickOptionsAsync();
        };

        // 初始化列操作命令
        AddColumnCommand = new RelayCommand(() =>
        {
            ColumnDefinitions.Add(new ColumnDefinition
            {
                Name = $"列{ColumnDefinitions.Count + 1}",
                Type = "文本",
                Required = false
            });
        });

        RemoveColumnCommand = new RelayCommand<ColumnDefinition>(column =>
        {
            if (column != null)
            {
                ColumnDefinitions.Remove(column);
                _logger.LogInformation("Removed column {Name}", column.Name);
            }
        });

        // 初始化列排序命令
        MoveColumnUpCommand = new RelayCommand<ColumnDefinition>(column =>
        {
            if (column == null) return;

            var index = ColumnDefinitions.IndexOf(column);
            if (index > 0)
            {
                ColumnDefinitions.Move(index, index - 1);
                _logger.LogInformation("Moved column {Name} up", column.Name);
            }
        });

        MoveColumnDownCommand = new RelayCommand<ColumnDefinition>(column =>
        {
            if (column == null) return;

            var index = ColumnDefinitions.IndexOf(column);
            if (index < ColumnDefinitions.Count - 1)
            {
                ColumnDefinitions.Move(index, index + 1);
                _logger.LogInformation("Moved column {Name} down", column.Name);
            }
        });

        // 重置下载次数命令
        ResetDownloadsCountCommand = new RelayCommand(() =>
        {
            DownloadsCount = 0;
            _logger.LogInformation("Downloads count reset for task: {Slug}", TaskSlug);
        }, () => IsOneTimeLink && DownloadsCount > 0);

        // 初始化扩展名管理命令
        SelectAllExtensionsCommand = new RelayCommand(ToggleSelectAllExtensions);
        InvertSelectionCommand = new RelayCommand(InvertSelection);
        ToggleExtensionCommand = new RelayCommand<FileExtensionInfo>(ToggleExtension);
        AddCustomExtensionCommand =
            new RelayCommand(AddCustomExtension, () => !string.IsNullOrWhiteSpace(CustomExtensionInput));

        // 如果有选中的任务，加载任务数据
        if (_mainViewModel.SelectedTask != null)
        {
            LoadFromTask(_mainViewModel.SelectedTask);
        }
        else
        {
            // 新建任务
            TaskSlug = _slugGeneratorService.GenerateSlug();
            ExpiryDate = DateTime.Now.AddDays(30);
        }

        // 加载扩展名配置
        _ = LoadExtensionsAsync();
    }

    // 预设扩展名分组（用于UI显示）
    public ObservableCollection<PresetExtensionGroup> PresetGroups { get; } = new();

    // 编辑模式相关
    /// <summary>
    ///     是否为编辑模式
    /// </summary>
    public bool IsEditMode => _mainViewModel.SelectedTask != null;

    public bool HasAttachments => TaskAttachments.Count > 0;

    // UI 显隐控制属性
    public bool IsFileCollection => TaskType == TaskType.FileCollection;
    public bool IsDataCollection => TaskType == TaskType.DataCollection;
    public bool CanResetDownloadsCount => IsOneTimeLink && DownloadsCount > 0;

    // 模板文件路径提示文本
    public string TemplatePathHint => TaskType == TaskType.DataCollection
        ? "表格结构文件路径（自动生成）"
        : "Excel 模板文件路径";

    // 选择按钮文本
    public string SelectButtonLabel => TaskType == TaskType.DataCollection
        ? "选择 Schema 文件"
        : "选择 Excel 模板";

    // --- 路径预览属性 ---
    public string ConfigPathPreview => string.IsNullOrWhiteSpace(TaskTitle)
        ? "请输入标题"
        : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", TaskTitle);

    public string CollectionPathPreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(TaskTitle))
                return "请输入标题";

            var taskId = string.IsNullOrWhiteSpace(TaskSlug) ? "[任务ID]" : TaskSlug;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "收集", TaskTitle, taskId,
                TaskType == TaskType.FileCollection ? "文件收集" : "在线填表");
        }
    }

    /// <summary>
    ///     是否显示任务附件区域（仅在线填表模式且勾选了允许上传附件）
    /// </summary>
    public bool ShowTaskAttachments => TaskType == TaskType.DataCollection && AllowAttachmentUpload;

    // 列定义集合（仅用于 DataCollection 任务）
    public ObservableCollection<ColumnDefinition> ColumnDefinitions { get; } = new();

    // 列类型选项（用于 DataGrid 类型选择）
    public List<string> ColumnTypes { get; } = new() { "文本", "数字", "双选框(是/否)" };

    // 添加列和删除列命令
    public IRelayCommand AddColumnCommand { get; }
    public IRelayCommand<ColumnDefinition> RemoveColumnCommand { get; }
    public IRelayCommand<ColumnDefinition> MoveColumnUpCommand { get; }
    public IRelayCommand<ColumnDefinition> MoveColumnDownCommand { get; }
    public IRelayCommand ResetDownloadsCountCommand { get; }

    // 扩展名管理命令
    public IRelayCommand SelectAllExtensionsCommand { get; }
    public IRelayCommand InvertSelectionCommand { get; }
    public IRelayCommand<FileExtensionInfo> ToggleExtensionCommand { get; }
    public IRelayCommand AddCustomExtensionCommand { get; }

    public ObservableCollection<ExpiryQuickOption> ExpiryQuickOptions { get; } = new();

    /// <summary>
    ///     已选择的扩展名数量
    /// </summary>
    public int SelectedExtensionsCount => TaskExtensions.Count(e => e.IsSelected);

    // --- TaskTitle 改变时触发 ---
    partial void OnTaskTitleChanged(string value)
    {
        // 通知路径预览属性已改变
        OnPropertyChanged(nameof(ConfigPathPreview));
        OnPropertyChanged(nameof(CollectionPathPreview));
        OnPropertyChanged(nameof(SelectedExtensionsCount));

        // 如果是编辑模式且标题已修改，迁移扩展名配置
        if (IsEditMode && _mainViewModel.SelectedTask != null &&
            !string.IsNullOrWhiteSpace(_mainViewModel.SelectedTask.Title) &&
            !string.IsNullOrWhiteSpace(value) &&
            _mainViewModel.SelectedTask.Title != value)
            _ = _fileExtensionService.MigrateTaskExtensionsAsync(_mainViewModel.SelectedTask.Title, value);
    }

    // --- TaskType 改变时触发 ---
    partial void OnTaskTypeChanged(TaskType value)
    {
        // 通知 UI 更新显隐控制属性
        OnPropertyChanged(nameof(IsFileCollection));
        OnPropertyChanged(nameof(IsDataCollection));
        OnPropertyChanged(nameof(SelectButtonLabel));
        OnPropertyChanged(nameof(CollectionPathPreview));
        OnPropertyChanged(nameof(ShowTaskAttachments));

        // 更新附件上传启用状态（文件收集和在线填表都支持附件上传）
        IsAttachmentUploadEnabled = AllowAttachmentUpload;

        // 切换任务类型时清空列定义和模板路径（仅在新建任务时）
        if (_mainViewModel.SelectedTask == null)
        {
            ColumnDefinitions.Clear();
            TemplatePath = string.Empty;
            _logger.LogInformation(
                "Task type changed to {TaskType}, cleared column definitions and template path (new task)", value);
        }
        else
        {
            // 编辑任务时，切换到 FileCollection 清空列定义，切换到 DataCollection 尝试加载列定义
            if (value == TaskType.FileCollection)
            {
                ColumnDefinitions.Clear();
                _logger.LogInformation("Task type changed to FileCollection, cleared column definitions (edit task)");
            }
            else if (value == TaskType.DataCollection && !string.IsNullOrEmpty(TemplatePath))
            {
                // 尝试从 TemplatePath 加载列定义
                LoadColumnDefinitionsFromSchema(TemplatePath);
                _logger.LogInformation(
                    "Task type changed to DataCollection, loaded column definitions from {TemplatePath}", TemplatePath);
            }
        }
    }

    // --- AllowAttachmentUpload 改变时触发 ---
    partial void OnAllowAttachmentUploadChanged(bool value)
    {
        IsAttachmentUploadEnabled = value;
        OnPropertyChanged(nameof(ShowTaskAttachments));
        _logger.LogDebug("AllowAttachmentUpload changed to {Value}, IsAttachmentUploadEnabled={Enabled}",
            value, IsAttachmentUploadEnabled);
    }

    // --- 长期任务选项改变 ---
    partial void OnIsLongTermTaskChanged(bool value)
    {
        if (value)
        {
            ExpiryDate = null;
            SelectedExpiryQuickOption = null;
        }
        else
        {
            ExpiryDate = DateTime.Now;
        }
    }

    // --- 快捷时间选项改变 ---
    partial void OnSelectedExpiryQuickOptionChanged(ExpiryQuickOption? value)
    {
        if (value != null)
        {
            IsLongTermTask = false;
            ExpiryDate = DateTime.Now.Add(value.TimeSpan);
        }
    }

    // --- ExpiryDate 改变时触发 ---
    partial void OnExpiryDateChanged(DateTime? value)
    {
        // 当 ExpiryDate 改变时，更新时间部分的属性
        if (value.HasValue)
        {
            Hour = value.Value.Hour;
            Minute = value.Value.Minute;
            Second = value.Value.Second;
        }
        else
        {
            Hour = 0;
            Minute = 0;
            Second = 0;
        }
    }

    // --- 时间部分改变时触发 ---
    partial void OnHourChanged(int value)
    {
        UpdateExpiryDateFromTime();
    }

    partial void OnMinuteChanged(int value)
    {
        UpdateExpiryDateFromTime();
    }

    partial void OnSecondChanged(int value)
    {
        UpdateExpiryDateFromTime();
    }

    // 验证并修正小时值（由 View 层在失去焦点时调用）
    public void ValidateAndFixHour(int value)
    {
        Hour = Math.Clamp(value, 0, 23);
    }

    // 验证并修正分钟值（由 View 层在失去焦点时调用）
    public void ValidateAndFixMinute(int value)
    {
        Minute = Math.Clamp(value, 0, 59);
    }

    // 验证并修正秒值（由 View 层在失去焦点时调用）
    public void ValidateAndFixSecond(int value)
    {
        Second = Math.Clamp(value, 0, 59);
    }

    // 从时间部分更新 ExpiryDate
    private void UpdateExpiryDateFromTime()
    {
        if (ExpiryDate == null || IsLongTermTask)
            return;

        var currentDate = ExpiryDate.Value;
        ExpiryDate = new DateTime(currentDate.Year, currentDate.Month, currentDate.Day, Hour, Minute, Second);
    }

    // --- 密码要求改变 ---
    partial void OnRequirePasswordChanged(bool value)
    {
        if (!value)
            // 取消密码要求时，清空密码
            TaskPassword = null;
    }

    partial void OnIsOneTimeLinkChanged(bool value)
    {
        // 更新重置下载次数按钮的启用状态
        ((RelayCommand)ResetDownloadsCountCommand).NotifyCanExecuteChanged();
    }

    partial void OnDownloadsCountChanged(int value)
    {
        // 更新重置下载次数按钮的启用状态
        ((RelayCommand)ResetDownloadsCountCommand).NotifyCanExecuteChanged();
    }

    // --- 命令 ---
    [RelayCommand]
    private void SelectTemplateFile()
    {
        string title;
        string filter;

        if (TaskType == TaskType.DataCollection)
        {
            title = "选择 Schema 文件";
            filter = "Schema 文件 (*.json)|*.json|所有文件 (*.*)|*.*";
        }
        else
        {
            title = "选择 Excel 模板文件";
            filter = "Excel 文件 (*.xlsx)|*.xlsx|所有文件 (*.*)|*.*";
        }

        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            TemplatePath = dialog.FileName;

            // 如果是 DataCollection 任务，尝试加载列定义
            if (TaskType == TaskType.DataCollection) LoadColumnDefinitionsFromSchema(TemplatePath);
        }
    }

    [RelayCommand]
    private void ClearTemplatePath()
    {
        TemplatePath = string.Empty;
        _logger.LogInformation("Cleared template path");
    }

    /// <summary>
    ///     处理拖拽的模板文件
    /// </summary>
    public void HandleTemplateFileDrop(string filePath)
    {
        try
        {
            _logger.LogInformation("Handling template file drop: {FilePath}", filePath);

            // 验证文件是否存在
            if (!File.Exists(filePath))
            {
                DialogService.ShowError("文件不存在");
                return;
            }

            // 验证文件类型
            var extension = Path.GetExtension(filePath).ToLower();
            var expectedExtension = TaskType == TaskType.DataCollection ? ".json" : ".xlsx";

            if (extension != expectedExtension)
            {
                var expectedTypeName = TaskType == TaskType.DataCollection ? "JSON" : "Excel";
                DialogService.ShowError(
                    $"不支持的文件类型，请选择 {expectedTypeName} 文件");
                return;
            }

            // 检查是否输入了标题
            if (string.IsNullOrWhiteSpace(TaskTitle))
            {
                DialogService.ShowWarning(
                    "请先输入任务标题",
                    "提示");
                return;
            }

            // 临时保存源文件路径（稍后在SaveAndClose中复制到config目录）
            TemplatePath = filePath;

            // 如果是 DataCollection 任务，加载列定义
            if (TaskType == TaskType.DataCollection) LoadColumnDefinitionsFromSchema(filePath);

            _logger.LogInformation("Template file dropped successfully: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle template file drop: {Message}", ex.Message);
            DialogService.ShowError(
                $"处理文件失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SelectTargetFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择文件保存文件夹"
        };

        if (dialog.ShowDialog() == true) TargetFolder = dialog.FolderName;
    }

    [RelayCommand]
    private async Task SaveAndClose()
    {
        try
        {
            // 验证标题
            if (string.IsNullOrWhiteSpace(TaskTitle))
            {
                DialogService.ShowWarning("请输入任务标题", "提示");
                return;
            }

            // 验证标题特殊字符
            var invalidChars = Path.GetInvalidFileNameChars();
            if (TaskTitle.Any(c => invalidChars.Contains(c)))
            {
                MessageBox.Show("标题包含非法字符，请避免使用以下字符：\\ / : * ? \" < > |", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (TaskType == TaskType.FileCollection && string.IsNullOrWhiteSpace(TemplatePath))
            {
                DialogService.ShowWarning("请选择模板文件", "提示");
                return;
            }

            if (TaskType == TaskType.DataCollection && ColumnDefinitions.Count == 0)
            {
                DialogService.ShowWarning("请至少添加一个列定义", "提示");
                return;
            }

            // 验证过期时间
            if (!IsLongTermTask && ExpiryDate.HasValue)
            {
                // 检查过期时间是否是过去的时间
                if (ExpiryDate.Value <= DateTime.Now)
                {
                    DialogService.ShowError("过期时间不能是过去的时间，请重新设置");
                    return;
                }
            }
            else if (!IsLongTermTask && !ExpiryDate.HasValue)
            {
                MessageBox.Show("请选择过期时间或勾选长期任务", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 处理 DataCollection 任务的列定义
            var templatePath = TemplatePath;
            if (TaskType == TaskType.DataCollection)
            {
                _logger.LogInformation("Saving DataCollection task schema with {Count} columns",
                    ColumnDefinitions.Count);

                var schema = new TableSchema
                {
                    Title = TaskTitle,
                    Columns = ColumnDefinitions.ToList()
                };

                // schema保存到config目录
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", TaskTitle);
                DirectoryHelper.EnsureDirectoryExists(configPath);

                var schemaPath = Path.Combine(configPath, "schema.json");
                var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(schemaPath, json);
                templatePath = schemaPath;

                _logger.LogInformation("Schema saved to: {SchemaPath}", schemaPath);
            }

            var isNewTask = _mainViewModel.SelectedTask == null;

            // 如果是编辑模式，显示确认对话框
            if (!isNewTask)
            {
                var confirmed = DialogService.ShowConfirm(
                    "是否确认保存任务配置？",
                    "确认保存");

                if (!confirmed)
                {
                    _logger.LogInformation("User cancelled save operation");
                    return;
                }
            }

            if (isNewTask)
            {
                // 如果 TaskSlug 为空，生成新的 slug
                if (string.IsNullOrEmpty(TaskSlug))
                {
                    TaskSlug = _slugGeneratorService.GenerateSlug();
                    _logger.LogInformation("Generated new slug for task: {Slug}", TaskSlug);
                }

                // 创建新任务
                var passwordHash = RequirePassword && !string.IsNullOrEmpty(TaskPassword)
                    ? _passwordHashService.HashPassword(TaskPassword)
                    : null;

                var newTask = new LanTask
                {
                    Id = Guid.NewGuid().ToString(),
                    Slug = TaskSlug,
                    Title = TaskTitle,
                    TemplatePath = templatePath,
                    TargetFolder = string.Empty, // 不再使用TargetFolder
                    PasswordHash = passwordHash,
                    MaxLimit = MaxLimit,
                    ExpiryDate = ExpiryDate,
                    IsOneTimeLink = IsOneTimeLink,
                    VersioningMode = VersioningMode,
                    IsActive = IsActive,
                    Description = TaskDescription,
                    TaskType = TaskType,
                    AllowAttachmentUpload = AllowAttachmentUpload,
                    AttachmentDownloadDescription = AttachmentDownloadDescription,
                    ShowDescriptionInApi = ShowDescriptionInApi
                };

                // 创建目录结构
                var directories = new List<string>
                {
                    newTask.ConfigPath,
                    newTask.FileCollectionPath,
                    newTask.DataCollectionPath
                };

                // 只在需要时创建 attachments 文件夹
                if (TaskType == TaskType.DataCollection && AllowAttachmentUpload)
                    directories.Add(newTask.AttachmentsPath);

                DirectoryHelper.EnsureDirectoriesExist(directories.ToArray());
                _logger.LogInformation("Created directory structure for task: {TaskId}", newTask.Id);

                // 复制模板文件到config目录（FileCollection任务）
                if (TaskType == TaskType.FileCollection && !string.IsNullOrEmpty(TemplatePath))
                {
                    var templateFileName = Path.GetFileName(TemplatePath);
                    var destTemplatePath = Path.Combine(newTask.ConfigPath, templateFileName);
                    File.Copy(TemplatePath, destTemplatePath, true);
                    newTask.TemplatePath = destTemplatePath;
                    _logger.LogInformation("Copied template file to: {TemplatePath}", destTemplatePath);
                }

                var created = await _taskRepository.CreateTaskAsync(newTask);
                if (!created)
                {
                    _logger.LogError("Failed to create task in database");
                    MessageBox.Show("创建任务失败，请重试", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 保存扩展名配置
                await SaveExtensionsAsync(newTask.Id, newTask.Title);

                _logger.LogInformation("Task created successfully: {Id} - {Slug}", newTask.Id, newTask.Slug);
                _mainViewModel.StatusMessage = $"已创建任务: {TaskSlug}";

                // 刷新主窗口任务列表
                await _mainViewModel.LoadTasksCommand.ExecuteAsync(null);
            }
            else
            {
                // 编辑任务：检查 Slug 是否被其他任务占用
                var existingTask = await _taskRepository.GetTaskBySlugAsync(TaskSlug);
                if (existingTask != null && existingTask.Id != _mainViewModel.SelectedTask?.Id)
                {
                    MessageBox.Show($"任务ID '{TaskSlug}' 已被其他任务使用，请修改任务ID", "错误", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // 更新现有任务
                // 如果设置了新密码，则哈希新密码；否则保持原有密码
                var passwordHash = RequirePassword && !string.IsNullOrEmpty(TaskPassword)
                    ? _passwordHashService.HashPassword(TaskPassword)
                    : _mainViewModel.SelectedTask!.PasswordHash;

                var task = new LanTask
                {
                    Id = _mainViewModel.SelectedTask!.Id,
                    Slug = TaskSlug,
                    Title = TaskTitle,
                    TemplatePath = templatePath,
                    TargetFolder = _mainViewModel.SelectedTask.TargetFolder, // 保持原有TargetFolder
                    PasswordHash = passwordHash,
                    MaxLimit = MaxLimit,
                    ExpiryDate = ExpiryDate,
                    IsOneTimeLink = IsOneTimeLink,
                    VersioningMode = VersioningMode,
                    IsActive = IsActive,
                    Description = TaskDescription,
                    TaskType = TaskType,
                    DownloadsCount = DownloadsCount,
                    CurrentCount = CurrentCount,
                    AllowAttachmentUpload = AllowAttachmentUpload,
                    AttachmentDownloadDescription = AttachmentDownloadDescription,
                    ShowDescriptionInApi = ShowDescriptionInApi
                };

                var updated = await _taskRepository.UpdateTaskAsync(task);
                if (!updated)
                {
                    _logger.LogError("Failed to update task in database: {Id}", task.Id);
                    DialogService.ShowError("更新任务失败，请重试");
                    return;
                }

                // 保存扩展名配置
                await SaveExtensionsAsync(task.Id, task.Title);

                _logger.LogInformation("Task updated successfully: {Id} - {Slug}", task.Id, task.Slug);
                _mainViewModel.StatusMessage = $"已更新任务: {TaskSlug}";
                await _mainViewModel.LoadTasksCommand.ExecuteAsync(null);
            }

            // 关闭对话框
            _closeDialogCallback?.Invoke(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save task: {Message}", ex.Message);
            DialogService.ShowError($"保存任务失败: {ex.Message}\n详细信息: {ex.StackTrace}");
        }
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        if (_mainViewModel.SelectedTask == null)
        {
            DialogService.ShowWarning("当前没有选中的任务", "提示");
            return;
        }

        try
        {
            // 弹出输入对话框，让用户输入新标题
            var newTitle = DialogService.ShowInputDialog(
                "请输入新任务的标题",
                "另存为",
                "任务标题",
                _mainViewModel.SelectedTask.Title + " (副本)"
            );

            if (string.IsNullOrWhiteSpace(newTitle))
            {
                DialogService.ShowWarning("请输入任务标题", "提示");
                return;
            }

            await _mainViewModel.CopyTaskAsync(_mainViewModel.SelectedTask, newTitle);
            _closeDialogCallback?.Invoke(false); // 关闭对话框
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save as: {Message}", ex.Message);
            DialogService.ShowError($"另存为失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _closeDialogCallback?.Invoke(false);
    }

    public void SetCloseCallback(Action<bool> callback)
    {
        _closeDialogCallback = callback;
    }

    [RelayCommand]
    private void GenerateDownloadLink()
    {
        var ip = GetLocalIpAddress();
        var page = TaskType == TaskType.DataCollection ? "distribution.html" : "task.html";
        var link = $"http://{ip}:{_mainViewModel.Port}/{page}?slug={TaskSlug}";
        GeneratedLink = link;
    }

    [RelayCommand]
    private void CopyLinkToClipboard()
    {
        if (string.IsNullOrWhiteSpace(GeneratedLink)) GenerateDownloadLink();

        Clipboard.SetText(GeneratedLink);
        _mainViewModel.StatusMessage = "链接已复制到剪贴板";
    }

    // --- 辅助方法 ---

    private void LoadFromTask(LanTask task)
    {
        TaskTitle = task.Title ?? string.Empty;
        TaskDescription = task.Description ?? string.Empty;
        TaskSlug = task.Slug;
        TemplatePath = task.TemplatePath;
        TargetFolder = task.TargetFolder;
        TaskPassword = string.Empty;
        MaxLimit = task.MaxLimit;
        ExpiryDate = task.ExpiryDate;
        IsOneTimeLink = task.IsOneTimeLink;
        VersioningMode = task.VersioningMode;
        IsActive = task.IsActive;
        TaskType = task.TaskType;
        DownloadsCount = task.DownloadsCount;
        CurrentCount = task.CurrentCount;

        // 根据ExpiryDate判断是否是长期任务
        IsLongTermTask = task.ExpiryDate == null;

        // 根据是否有密码设置RequirePassword
        RequirePassword = !string.IsNullOrWhiteSpace(task.PasswordHash);

        // 重置快捷时间选项选择
        SelectedExpiryQuickOption = null;

        // 加载附件上传配置
        AllowAttachmentUpload = task.AllowAttachmentUpload;
        AttachmentDownloadDescription = task.AttachmentDownloadDescription;
        IsAttachmentUploadEnabled = AllowAttachmentUpload;

        // 加载描述公开配置
        ShowDescriptionInApi = task.ShowDescriptionInApi;

        // 加载任务附件列表
        TaskAttachments.Clear();
        var attachments = _taskAttachmentService.GetAttachmentsByTaskIdAsync(task.Id).GetAwaiter().GetResult();
        foreach (var att in attachments) TaskAttachments.Add(att);

        _logger.LogInformation("Loaded {Count} attachments for task {TaskId}", attachments.Count, task.Id);

        // 如果是 DataCollection 任务，加载列定义
        _logger.LogInformation("Loading task: TaskType={TaskType}, TemplatePath={TemplatePath}", task.TaskType,
            task.TemplatePath);
        if (task.TaskType == TaskType.DataCollection)
        {
            _logger.LogInformation("Task is DataCollection, checking TemplatePath");
            if (!string.IsNullOrEmpty(task.TemplatePath))
            {
                _logger.LogInformation("TemplatePath is not empty, loading column definitions");
                LoadColumnDefinitionsFromSchema(task.TemplatePath);
            }
            else
            {
                _logger.LogWarning("TemplatePath is empty for DataCollection task");
            }
        }
        else
        {
            _logger.LogInformation("Task is FileCollection, skipping column definitions");
        }

        _logger.LogInformation("Task loaded: {TaskId}", task.Id);
    }

    private string GetLocalIpAddress()
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

    /// <summary>
    ///     从 JSON 文件加载列定义
    /// </summary>
    private void LoadColumnDefinitionsFromSchema(string schemaPath)
    {
        try
        {
            _logger.LogInformation("Loading column definitions from schema: {SchemaPath}", schemaPath);

            if (!File.Exists(schemaPath))
            {
                _logger.LogWarning("Schema file does not exist: {SchemaPath}", schemaPath);
                MessageBox.Show($"表格结构文件不存在: {schemaPath}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var json = File.ReadAllText(schemaPath);
            _logger.LogDebug("Schema JSON content: {Json}", json);

            var schema = JsonSerializer.Deserialize<TableSchema>(json);

            if (schema == null)
            {
                _logger.LogWarning("Failed to deserialize schema from: {SchemaPath}", schemaPath);
                MessageBox.Show("无法解析表格结构文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _logger.LogDebug("Deserialized schema: Title={Title}, ColumnsCount={ColumnsCount}",
                schema.Title, schema.Columns?.Count ?? 0);

            if (schema.Columns == null || schema.Columns.Count == 0)
            {
                _logger.LogWarning("Schema has no columns: {SchemaPath}", schemaPath);
                MessageBox.Show("表格结构文件中没有列定义", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 清空现有列定义并加载新列
            ColumnDefinitions.Clear();
            foreach (var col in schema.Columns)
            {
                ColumnDefinitions.Add(col);
                _logger.LogDebug("Loaded column: {Name}, Type: {Type}, Required: {Required}",
                    col.Name, col.Type, col.Required);
            }

            _logger.LogInformation("Successfully loaded {Count} column definitions", ColumnDefinitions.Count);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error in schema file: {SchemaPath}", schemaPath);
            DialogService.ShowError($"表格结构文件格式错误: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load column definitions from schema: {SchemaPath}", schemaPath);
            DialogService.ShowError($"加载列定义失败: {ex.Message}");
        }
    }

    // --- 任务附件管理命令 ---

    /// <summary>
    ///     处理附件文件的通用方法（支持多文件）
    /// </summary>
    private async Task ProcessAttachmentsAsync(string[] filePaths)
    {
        var selectedTask = _mainViewModel.SelectedTask;
        if (selectedTask == null)
        {
            DialogService.ShowWarning("请先保存任务后再添加附件", "提示");
            return;
        }

        var attachmentsFolder = selectedTask.AttachmentsPath;
        DirectoryHelper.EnsureDirectoryExists(attachmentsFolder);

        var successCount = 0;
        var skippedCount = 0;
        var errorMessages = new List<string>();

        foreach (var filePath in filePaths)
            try
            {
                if (!File.Exists(filePath))
                {
                    errorMessages.Add($"文件不存在: {Path.GetFileName(filePath)}");
                    skippedCount++;
                    continue;
                }

                var fileInfo = new FileInfo(filePath);

                var destPath = await _taskAttachmentService.CopyFileToTaskFolderAsync(
                    filePath,
                    attachmentsFolder,
                    fileInfo.Name);

                var attachment = new TaskAttachment
                {
                    TaskId = selectedTask.Id,
                    FileName = fileInfo.Name,
                    FilePath = destPath,
                    DisplayName = Path.GetFileNameWithoutExtension(fileInfo.Name),
                    FileSize = fileInfo.Length,
                    UploadDate = DateTime.Now,
                    SortOrder = TaskAttachments.Count
                };

                await _taskAttachmentService.AddAttachmentAsync(attachment);
                TaskAttachments.Add(attachment);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process attachment: {FilePath}", filePath);
                errorMessages.Add($"文件 \"{Path.GetFileName(filePath)}\" 添加失败: {ex.Message}");
                skippedCount++;
            }

        _logger.LogInformation("Processed attachments: Success={SuccessCount}, Skipped={SkippedCount}",
            successCount, skippedCount);

        if (successCount > 0) _mainViewModel.StatusMessage = $"成功添加 {successCount} 个附件";

        if (errorMessages.Count > 0)
        {
            var message =
                $"成功添加 {successCount} 个附件，跳过 {skippedCount} 个文件。\n\n错误详情：\n{string.Join("\n", errorMessages)}";
            DialogService.ShowWarning(message, "提示");
        }
    }

    [RelayCommand]
    private async Task AddAttachment()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择附件文件",
            Filter = "所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog() == true) await ProcessAttachmentsAsync(dialog.FileNames);
    }

    /// <summary>
    ///     处理拖拽的多个附件文件
    /// </summary>
    public async void HandleAttachmentsDrop(string[] filePaths)
    {
        await ProcessAttachmentsAsync(filePaths);
    }

    [RelayCommand]
    private async Task RemoveAttachment(TaskAttachment? attachment)
    {
        if (attachment == null) return;

        var confirmed = DialogService.ShowConfirm(
            $"确定要删除附件 \"{attachment.FileName}\" 吗？",
            "确认删除");

        if (confirmed)
            try
            {
                var success = await _taskAttachmentService.DeleteAttachmentAsync(attachment.Id);
                if (success)
                {
                    TaskAttachments.Remove(attachment);
                    // 重新更新排序
                    for (var i = 0; i < TaskAttachments.Count; i++) TaskAttachments[i].SortOrder = i;

                    await _taskAttachmentService.UpdateAttachmentsOrderAsync(TaskAttachments.ToList());
                    _logger.LogInformation("Removed attachment {Id}", attachment.Id);
                    _mainViewModel.StatusMessage = "删除附件成功";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove attachment");
                DialogService.ShowError($"删除附件失败: {ex.Message}");
            }
    }

    [RelayCommand]
    private async Task ClearAllAttachments()
    {
        if (TaskAttachments.Count == 0)
        {
            DialogService.ShowWarning("当前没有附件可清空", "提示");
            return;
        }

        var confirmed = DialogService.ShowConfirm(
            $"确定要清空所有 {TaskAttachments.Count} 个附件吗？此操作不可恢复。",
            "确认清空");

        if (confirmed)
            try
            {
                var selectedTask = _mainViewModel.SelectedTask;
                if (selectedTask == null)
                {
                    DialogService.ShowWarning("请先选择任务", "提示");
                    return;
                }

                // 删除所有附件
                foreach (var attachment in TaskAttachments.ToList())
                    await _taskAttachmentService.DeleteAttachmentAsync(attachment.Id);

                TaskAttachments.Clear();
                _logger.LogInformation("Cleared all attachments for task {TaskId}", selectedTask.Id);
                _mainViewModel.StatusMessage = "已清空所有附件";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear all attachments");
                DialogService.ShowError($"清空附件失败: {ex.Message}");
            }
    }

    [RelayCommand]
    private async Task UpdateAttachment(TaskAttachment attachment)
    {
        try
        {
            await _taskAttachmentService.UpdateAttachmentAsync(attachment);
            _logger.LogInformation("Updated attachment {Id}", attachment.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update attachment");
            DialogService.ShowError($"更新附件失败: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task MoveAttachmentUp(TaskAttachment? attachment)
    {
        if (attachment == null) return;

        var index = TaskAttachments.IndexOf(attachment);
        if (index > 0)
        {
            TaskAttachments.Move(index, index - 1);
            for (var i = 0; i < TaskAttachments.Count; i++) TaskAttachments[i].SortOrder = i;

            await _taskAttachmentService.UpdateAttachmentsOrderAsync(TaskAttachments.ToList());
            _logger.LogInformation("Moved attachment {Id} up", attachment.Id);
        }
    }

    [RelayCommand]
    private async Task MoveAttachmentDown(TaskAttachment? attachment)
    {
        if (attachment == null) return;

        var index = TaskAttachments.IndexOf(attachment);
        if (index < TaskAttachments.Count - 1)
        {
            TaskAttachments.Move(index, index + 1);
            for (var i = 0; i < TaskAttachments.Count; i++) TaskAttachments[i].SortOrder = i;

            await _taskAttachmentService.UpdateAttachmentsOrderAsync(TaskAttachments.ToList());
            _logger.LogInformation("Moved attachment {Id} down", attachment.Id);
        }
    }

    // --- 快捷时间选项管理方法 ---

    /// <summary>
    ///     加载快捷时间选项
    /// </summary>
    private async Task LoadExpiryQuickOptionsAsync()
    {
        try
        {
            _logger.LogInformation("TaskConfigViewModel - 开始加载快捷时间选项");

            var config = await _expiryQuickOptionsService.LoadConfigAsync();
            var options = _expiryQuickOptionsService.GetOptions();

            // 清空现有选项
            ExpiryQuickOptions.Clear();

            // 添加配置文件中的选项
            foreach (var option in options)
            {
                ExpiryQuickOptions.Add(option);
                _logger.LogDebug("  加载选项: {DisplayName}, TimeSpan: {TimeSpan}",
                    option.DisplayName, option.TimeSpan);
            }

            _logger.LogInformation("TaskConfigViewModel - 快捷时间选项加载完成，共 {Count} 个选项",
                ExpiryQuickOptions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TaskConfigViewModel - 加载快捷时间选项失败: {Message}", ex.Message);
            // 加载失败时，添加默认选项
            ExpiryQuickOptions.Clear();
            ExpiryQuickOptions.Add(new ExpiryQuickOption { DisplayName = "1小时", Hours = 1 });
            ExpiryQuickOptions.Add(new ExpiryQuickOption { DisplayName = "12小时", Hours = 12 });
            ExpiryQuickOptions.Add(new ExpiryQuickOption { DisplayName = "1天", Days = 1 });
            ExpiryQuickOptions.Add(new ExpiryQuickOption { DisplayName = "3天", Days = 3 });
            ExpiryQuickOptions.Add(new ExpiryQuickOption { DisplayName = "7天", Days = 7 });
            ExpiryQuickOptions.Add(new ExpiryQuickOption { DisplayName = "15天", Days = 15 });
            ExpiryQuickOptions.Add(new ExpiryQuickOption { DisplayName = "30天", Days = 30 });
            ExpiryQuickOptions.Add(new ExpiryQuickOption { DisplayName = "60天", Days = 60 });
            ExpiryQuickOptions.Add(new ExpiryQuickOption { DisplayName = "90天", Days = 90 });
        }
    }

    // --- 扩展名管理方法 ---

    /// <summary>
    ///     加载扩展名配置
    /// </summary>
    private async Task LoadExtensionsAsync()
    {
        try
        {
            _logger.LogInformation("=== 开始加载扩展名配置 ===");
            _logger.LogInformation("是否编辑模式: {IsEditMode}", IsEditMode);

            if (IsEditMode && _mainViewModel.SelectedTask != null)
                _logger.LogInformation("编辑模式 - 任务ID: {TaskId}, 任务标题: {TaskTitle}",
                    _mainViewModel.SelectedTask.Id, _mainViewModel.SelectedTask.Title);
            else
                _logger.LogInformation("新建模式 - 使用默认配置");

            // 创建预设扩展名分组
            PresetGroups.Clear();
            var groupedPresets = PresetExtensions.GroupBy(e => e.Category);
            foreach (var group in groupedPresets)
                PresetGroups.Add(new PresetExtensionGroup
                {
                    CategoryName = group.Key,
                    Extensions = new ObservableCollection<FileExtensionInfo>(group.ToList())
                });

            // 设置分类列表
            ExtensionCategories = new ObservableCollection<string> { "文档", "图片", "压缩包", "自定义" };
            ExtensionCategories.Insert(0, "全部"); // 添加"全部"选项

            // 如果是编辑模式，加载任务的扩展名配置
            if (IsEditMode && _mainViewModel.SelectedTask != null)
            {
                _logger.LogInformation("准备从文件加载扩展名配置");
                var taskConfig = await _fileExtensionService.GetTaskExtensionsAsync(
                    _mainViewModel.SelectedTask.Id,
                    _mainViewModel.SelectedTask.Title);

                _logger.LogInformation("加载到的扩展名数量: {Count}", taskConfig.Extensions.Count);

                // 如果配置文件不存在或没有扩展名，默认全选
                var useDefaultSelection = taskConfig.Extensions.Count == 0;
                if (useDefaultSelection) _logger.LogInformation("配置文件不存在或为空，使用默认全选");

                // 构建任务扩展名列表
                TaskExtensions = new ObservableCollection<FileExtensionInfo>();

                // 添加预设扩展名（根据任务配置设置选中状态）
                foreach (var presetExt in PresetExtensions)
                {
                    var ext = new FileExtensionInfo
                    {
                        Extension = presetExt.Extension,
                        DisplayName = presetExt.DisplayName,
                        Category = presetExt.Category,
                        IsPreset = true,
                        IsSelected = useDefaultSelection ||
                                     taskConfig.Extensions.Any(e => e.Extension == presetExt.Extension && e.IsSelected)
                    };

                    // 订阅 PropertyChanged 事件，当 IsSelected 改变时更新计数
                    ext.PropertyChanged += (sender, e) =>
                    {
                        if (e.PropertyName == nameof(FileExtensionInfo.IsSelected)) UpdateSelectAllState();
                    };

                    TaskExtensions.Add(ext);
                    _logger.LogInformation("  加载预设扩展名: {Extension}, 选中状态: {IsSelected}",
                        ext.Extension, ext.IsSelected);
                }

                // 添加自定义扩展名
                var customExts = taskConfig.Extensions.Where(e => !e.IsPreset);
                foreach (var customExt in customExts)
                {
                    var ext = new FileExtensionInfo
                    {
                        Extension = customExt.Extension,
                        DisplayName = customExt.DisplayName,
                        Category = "自定义",
                        IsPreset = false,
                        IsSelected = customExt.IsSelected
                    };

                    // 订阅 PropertyChanged 事件，当 IsSelected 改变时更新计数
                    ext.PropertyChanged += (sender, e) =>
                    {
                        if (e.PropertyName == nameof(FileExtensionInfo.IsSelected)) UpdateSelectAllState();
                    };

                    TaskExtensions.Add(ext);
                    _logger.LogInformation("  加载自定义扩展名: {Extension}, 选中状态: {IsSelected}",
                        ext.Extension, ext.IsSelected);
                }
            }
            else
            {
                _logger.LogInformation("新建模式 - 默认选择所有预设扩展名");
                // 新建任务，默认选择所有预设扩展名
                TaskExtensions = new ObservableCollection<FileExtensionInfo>(
                    PresetExtensions.Select(e =>
                    {
                        var ext = new FileExtensionInfo
                        {
                            Extension = e.Extension,
                            DisplayName = e.DisplayName,
                            Category = e.Category,
                            IsPreset = true,
                            IsSelected = true
                        };

                        // 订阅 PropertyChanged 事件，当 IsSelected 改变时更新计数
                        ext.PropertyChanged += (sender, ev) =>
                        {
                            if (ev.PropertyName == nameof(FileExtensionInfo.IsSelected)) UpdateSelectAllState();
                        };

                        return ext;
                    }));
            }

            _logger.LogInformation("加载完成 - 扩展名总数: {TotalCount}, 已选数: {SelectedCount}",
                TaskExtensions.Count, TaskExtensions.Count(e => e.IsSelected));

            UpdateFilteredExtensions();
            UpdateSelectAllState();

            _logger.LogInformation("=== 扩展名配置加载成功 ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== 扩展名配置加载失败 ===");
            _logger.LogError("错误信息: {Message}", ex.Message);
            _logger.LogError("堆栈跟踪: {StackTrace}", ex.StackTrace);
            DialogService.ShowError("加载扩展名配置失败，错误: " + ex.Message);
        }
    }

    /// <summary>
    ///     保存扩展名配置
    /// </summary>
    private async Task SaveExtensionsAsync(string taskId, string taskTitle)
    {
        try
        {
            _logger.LogInformation("=== 开始保存扩展名配置 ===");
            _logger.LogInformation("任务ID: {TaskId}", taskId);
            _logger.LogInformation("任务标题: {TaskTitle}", taskTitle);
            _logger.LogInformation("是否允许上传附件: {AllowAttachmentUpload}", AllowAttachmentUpload);
            _logger.LogInformation("扩展名总数: {TotalCount}", TaskExtensions.Count);
            _logger.LogInformation("已选扩展名数: {SelectedCount}", TaskExtensions.Count(e => e.IsSelected));

            // 记录所有扩展名的详细信息
            foreach (var ext in TaskExtensions)
                _logger.LogInformation(
                    "  扩展名: {Extension}, 显示名: {DisplayName}, 分类: {Category}, 是否预设: {IsPreset}, 是否选中: {IsSelected}",
                    ext.Extension, ext.DisplayName, ext.Category, ext.IsPreset, ext.IsSelected);

            var config = new TaskFileExtensionsConfig
            {
                TaskId = taskId,
                TaskTitle = taskTitle,
                Extensions = TaskExtensions.Select(e => new FileExtensionInfo
                {
                    Extension = e.Extension,
                    DisplayName = e.DisplayName,
                    Category = e.Category,
                    IsPreset = e.IsPreset,
                    IsSelected = e.IsSelected
                }).ToList()
            };

            _logger.LogInformation("准备调用 FileExtensionService.SaveTaskExtensionsAsync");
            await _fileExtensionService.SaveTaskExtensionsAsync(config);
            _logger.LogInformation("=== 扩展名配置保存成功 ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== 扩展名配置保存失败 ===");
            _logger.LogError("任务ID: {TaskId}, 错误信息: {Message}", taskId, ex.Message);
            _logger.LogError("堆栈跟踪: {StackTrace}", ex.StackTrace);
        }
    }

    /// <summary>
    ///     全选/取消全选扩展名
    /// </summary>
    private void ToggleSelectAllExtensions()
    {
        var allSelected = TaskExtensions.All(e => e.IsSelected);
        foreach (var ext in TaskExtensions) ext.IsSelected = !allSelected;

        UpdateFilteredExtensions();
        UpdateSelectAllState();
    }

    /// <summary>
    ///     反选扩展名
    /// </summary>
    private void InvertSelection()
    {
        foreach (var ext in TaskExtensions) ext.IsSelected = !ext.IsSelected;

        UpdateFilteredExtensions();
        UpdateSelectAllState();
    }

    /// <summary>
    ///     切换单个扩展名的选中状态
    /// </summary>
    private void ToggleExtension(FileExtensionInfo? extension)
    {
        if (extension == null) return;

        extension.IsSelected = !extension.IsSelected;
        UpdateSelectAllState();
    }

    /// <summary>
    ///     添加自定义扩展名
    /// </summary>
    private void AddCustomExtension()
    {
        if (string.IsNullOrWhiteSpace(CustomExtensionInput))
            return;

        var input = CustomExtensionInput.Trim();
        if (!input.StartsWith(".")) input = "." + input;

        // 检查是否已存在
        if (TaskExtensions.Any(e => e.Extension.Equals(input, StringComparison.OrdinalIgnoreCase)))
        {
            DialogService.ShowWarning("该扩展名已存在", "提示");
            return;
        }

        // 添加新扩展名
        var newExt = new FileExtensionInfo
        {
            Extension = input.ToLower(),
            DisplayName = input.ToUpper() + " 文件",
            Category = "自定义",
            IsPreset = false,
            IsSelected = true,
            AddedAt = DateTime.UtcNow
        };

        // 订阅 PropertyChanged 事件，当 IsSelected 改变时更新计数
        newExt.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(FileExtensionInfo.IsSelected)) UpdateSelectAllState();
        };

        TaskExtensions.Add(newExt);
        UpdateFilteredExtensions();
        UpdateSelectAllState();

        CustomExtensionInput = string.Empty;
        _logger.LogInformation("Added custom extension: {Extension}", input);
    }

    /// <summary>
    ///     获取过滤后的扩展名列表
    /// </summary>
    private List<FileExtensionInfo> GetFilteredExtensions()
    {
        var filtered = TaskExtensions.AsEnumerable();

        // 分类过滤
        if (!string.IsNullOrWhiteSpace(SelectedCategoryFilter) && SelectedCategoryFilter != "全部")
            filtered = filtered.Where(e => e.Category == SelectedCategoryFilter);

        return filtered.ToList();
    }

    /// <summary>
    ///     更新全选状态
    /// </summary>
    private void UpdateSelectAllState()
    {
        IsAllExtensionsSelected = TaskExtensions.All(e => e.IsSelected);
        OnPropertyChanged(nameof(SelectedExtensionsCount));
    }

    /// <summary>
    ///     更新过滤后的扩展名列表
    /// </summary>
    private void UpdateFilteredExtensions()
    {
        FilteredExtensions = new ObservableCollection<FileExtensionInfo>(GetFilteredExtensions());
    }

    /// <summary>
    ///     OnSelectedCategoryFilterChanged - 选择分类后自动选中该分类下的所有扩展名
    /// </summary>
    partial void OnSelectedCategoryFilterChanged(string value)
    {
        if (value == "全部")
        {
            UpdateFilteredExtensions();
            return;
        }

        // 选择特定分类时，自动选中该分类下的所有扩展名
        foreach (var ext in TaskExtensions)
            if (ext.Category == value)
                ext.IsSelected = true;

        UpdateFilteredExtensions();
        UpdateSelectAllState();
    }

    /// <summary>
    ///     OnCustomExtensionInputChanged - 通知命令状态更新
    /// </summary>
    partial void OnCustomExtensionInputChanged(string value)
    {
        ((RelayCommand)AddCustomExtensionCommand).NotifyCanExecuteChanged();
    }
}