using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MiniExcelLibs;
using MyLanServer.Core.Enums;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;
using MyLanServer.Infrastructure.Services;
using MyLanServer.UI.Services;

namespace MyLanServer.UI.ViewModels;

/// <summary>
///     字段合并模式配置
/// </summary>
public class FieldMergeMode
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Text";
    public MergeMode MergeMode { get; set; } = MergeMode.Accumulate;
    public string GroupByField { get; set; } = "所属部门";
}

public partial class MergeDialogViewModel : ObservableObject
{
    private readonly ILogger<MergeDialogViewModel> _logger;
    private readonly MainViewModel _mainViewModel;
    private readonly IExcelMergeService _mergeService;
    [ObservableProperty] private ObservableCollection<string> _availableColumns = new();
    private Action<bool>? _closeDialogCallback;

    [ObservableProperty] private string _deduplicateColumn = "Contact";

    // 表头行配置
    [ObservableProperty] private int _headerRowIndex = 1;
    [ObservableProperty] private string _headerRowPreview = string.Empty;
    [ObservableProperty] private bool _isMerging;
    [ObservableProperty] private int _maxHeaderRowIndex = 10;
    [ObservableProperty] private string _mergeOutputPath = string.Empty;
    [ObservableProperty] private string _mergeProgress = string.Empty;
    [ObservableProperty] private double _mergeProgressValue;
    [ObservableProperty] private string _mergeSourceFolder = string.Empty;
    [ObservableProperty] private string _mergeStatistics = string.Empty;
    [ObservableProperty] private string _mergeTemplatePath = string.Empty;
    [ObservableProperty] private bool _removeDuplicates = true;

    // ListBox选中项
    [ObservableProperty] private string? _selectedAvailableColumn;
    [ObservableProperty] private string? _selectedColumn;
    [ObservableProperty] private ObservableCollection<string> _selectedColumns = new();

    [ObservableProperty] private string _separator = "|";
    [ObservableProperty] private bool _showStatistics;

    public MergeDialogViewModel(
        IExcelMergeService mergeService,
        MainViewModel mainViewModel,
        ILogger<MergeDialogViewModel> logger)
    {
        _mergeService = mergeService;
        _mainViewModel = mainViewModel;
        _logger = logger;

        // 默认表头行为第 0 行（第一行作为表头）
        HeaderRowIndex = 0;

        // 如果有选中的任务，使用任务的文件夹作为默认源文件夹
        if (_mainViewModel.SelectedTask != null &&
            !string.IsNullOrWhiteSpace(_mainViewModel.SelectedTask.CollectionPath))
        {
            MergeSourceFolder = _mainViewModel.SelectedTask.CollectionPath;

            // 获取文件夹名称
            var folderName = Path.GetFileName(_mainViewModel.SelectedTask.CollectionPath.TrimEnd('\\', '/'));

            // 生成输出文件名：文件夹名+合并结果.xlsx
            var outputFileName = $"{folderName}合并结果.xlsx";
            var parentDir = Path.GetDirectoryName(_mainViewModel.SelectedTask.CollectionPath);

            if (!string.IsNullOrWhiteSpace(parentDir))
                MergeOutputPath = Path.Combine(parentDir, outputFileName);
            else
                MergeOutputPath = outputFileName;

            // 如果有模板路径，自动加载模板列
            if (!string.IsNullOrWhiteSpace(_mainViewModel.SelectedTask.TemplatePath) &&
                File.Exists(_mainViewModel.SelectedTask.TemplatePath))
            {
                MergeTemplatePath = _mainViewModel.SelectedTask.TemplatePath;
                LoadTemplateColumns();
            }
        }
    }

    // 字段合并模式配置（在线填表）
    public ObservableCollection<FieldMergeMode> FieldMergeModes { get; } = new();

    // 合并模式选项
    public List<MergeMode> MergeModes { get; } = new()
    {
        MergeMode.Accumulate,
        MergeMode.GroupBy
    };

    // 是否为在线填表任务
    public bool IsDataCollection => _mainViewModel.SelectedTask?.TaskType == TaskType.DataCollection;

    // --- 文件选择命令 ---
    [RelayCommand]
    private void SelectMergeSourceFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择包含待合并 Excel 文件的文件夹"
        };

        if (dialog.ShowDialog() == true)
        {
            MergeSourceFolder = dialog.FolderName;

            // 优先使用模板列，其次从源文件夹加载
            if (!string.IsNullOrWhiteSpace(MergeTemplatePath) && File.Exists(MergeTemplatePath))
                LoadTemplateColumns();
            else
                LoadAvailableColumns();
        }
    }

    [RelayCommand]
    private void SelectMergeOutputFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "选择合并后的输出文件",
            Filter = "Excel 文件 (*.xlsx)|*.xlsx",
            DefaultExt = "xlsx",
            FileName = "合并结果.xlsx"
        };

        if (dialog.ShowDialog() == true) MergeOutputPath = dialog.FileName;
    }

    [RelayCommand]
    private void SelectMergeTemplateFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择合并模板文件",
            Filter = "Excel 文件 (*.xlsx)|*.xlsx|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            MergeTemplatePath = dialog.FileName;
            _logger.LogInformation("Template file selected: {Path}", MergeTemplatePath);
            LoadTemplateColumns();

            if (AvailableColumns.Count > 0)
                DialogService.ShowInfo($"成功加载模板文件，共{AvailableColumns.Count}个列");
            else
                DialogService.ShowWarning("模板文件加载失败或无列数据");
        }
    }

    // --- 加载可用列 ---
    private void LoadAvailableColumns()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(MergeSourceFolder) || !Directory.Exists(MergeSourceFolder))
                return;

            var files = Directory.GetFiles(MergeSourceFolder, "*.xlsx").FirstOrDefault();
            if (string.IsNullOrEmpty(files)) return;

            using var stream = File.OpenRead(files);

            // 使用 Query(false) 读取所有行
            var allRows = stream.Query().ToList();

            // 检查行索引是否有效
            if (HeaderRowIndex < 0 || HeaderRowIndex >= allRows.Count)
            {
                _logger.LogWarning("Header row index {Index} is out of range. Total rows: {Count}",
                    HeaderRowIndex, allRows.Count);
                return;
            }

            // 获取表头行
            var headerRow = (IDictionary<string, object>)allRows[HeaderRowIndex];
            var headers = headerRow.Values
                .Where(v => v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                .Select(v => v!.ToString()!)
                .ToList();

            if (headers.Count == 0)
            {
                _logger.LogWarning("No valid headers found at row {Index}", HeaderRowIndex);
                return;
            }

            // 清空可用列
            AvailableColumns.Clear();
            SelectedColumns.Clear();
            SelectedAvailableColumn = null;
            SelectedColumn = null;

            // 添加新列（避免重复）
            foreach (var header in headers)
                // 使用 Contains 检查避免添加重复列
                if (!AvailableColumns.Contains(header))
                    AvailableColumns.Add(header);

            _logger.LogInformation("Loaded {Count} columns from source folder at row {Index}: {Columns}",
                AvailableColumns.Count, HeaderRowIndex, string.Join(", ", AvailableColumns));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load columns from source folder");
        }
    }

    private void LoadTemplateColumns()
    {
        try
        {
            _logger.LogInformation("LoadTemplateColumns called: TemplatePath={Path}, Exists={Exists}",
                MergeTemplatePath,
                File.Exists(MergeTemplatePath));

            if (string.IsNullOrWhiteSpace(MergeTemplatePath) || !File.Exists(MergeTemplatePath))
            {
                _logger.LogWarning("Template file path is invalid or does not exist");
                return;
            }

            // 检查文件大小
            var fileInfo = new FileInfo(MergeTemplatePath);
            if (fileInfo.Length == 0)
            {
                _logger.LogError("Template file is empty: {Path}", MergeTemplatePath);
                DialogService.ShowError("模板文件为空或已损坏，请重新选择有效的模板文件");
                return;
            }

            List<string>? headers = null;
            TableSchema? schema = null;

            // 检查文件类型
            var fileExtension = Path.GetExtension(MergeTemplatePath).ToLowerInvariant();
            _logger.LogInformation("Template file extension: {Extension}", fileExtension);

            if (fileExtension == ".json")
            {
                // JSON 格式：读取 schema.json
                _logger.LogInformation("Loading JSON schema file");
                var jsonContent = File.ReadAllText(MergeTemplatePath);
                schema = JsonSerializer.Deserialize<TableSchema>(jsonContent);

                if (schema?.Columns != null && schema.Columns.Count > 0)
                {
                    headers = schema.Columns.Select(c => c.Name).ToList();
                    _logger.LogInformation("Found {Count} columns from JSON schema: {Columns}",
                        headers.Count, string.Join(", ", headers));

                    // 从实际数据文件中提取额外的字段（如"所属部门"等系统字段）
                    // 排除 schema.json 的元数据字段（title、columns）
                    var excludedFields = new HashSet<string> { "title", "columns" };

                    // 优先使用 MergeSourceFolder，如果未设置则使用模板路径所在目录
                    var searchFolder = MergeSourceFolder;
                    if (string.IsNullOrWhiteSpace(searchFolder))
                    {
                        searchFolder = Path.GetDirectoryName(MergeTemplatePath);
                        _logger.LogInformation("Using template directory for data files: {Folder}", searchFolder);
                    }

                    if (!string.IsNullOrWhiteSpace(searchFolder) && Directory.Exists(searchFolder))
                    {
                        var dataFiles = Directory.GetFiles(searchFolder, "*.json")
                            .Where(f => !f.Equals(MergeTemplatePath, StringComparison.OrdinalIgnoreCase))
                            .FirstOrDefault();

                        if (!string.IsNullOrEmpty(dataFiles))
                            try
                            {
                                var dataJson = File.ReadAllText(dataFiles);
                                var dataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(dataJson);
                                if (dataDict != null)
                                    foreach (var key in dataDict.Keys)
                                        // 只添加非空、非排除字段、且不在schema中的字段
                                        if (!string.IsNullOrEmpty(key) &&
                                            !excludedFields.Contains(key.ToLower()) &&
                                            !headers.Contains(key))
                                        {
                                            headers.Add(key);
                                            _logger.LogDebug("Added extra field from data: {Field}", key);
                                        }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to read data file for field extraction");
                            }
                        else
                            _logger.LogWarning("No data files found in folder: {Folder}", searchFolder);
                    }
                }
                else
                {
                    _logger.LogWarning("JSON schema is empty or invalid");
                }
            }
            else if (fileExtension == ".xlsx")
            {
                // Excel 格式：使用自定义表头行读取
                _logger.LogInformation("Loading Excel template file with header row index: {Index}", HeaderRowIndex);

                using var stream = File.OpenRead(MergeTemplatePath);

                // 使用 Query(false) 读取所有行
                var allRows = stream.Query().ToList();

                // 检查行索引是否有效
                if (HeaderRowIndex < 0 || HeaderRowIndex >= allRows.Count)
                {
                    _logger.LogWarning("Header row index {Index} is out of range. Total rows: {Count}",
                        HeaderRowIndex, allRows.Count);
                    return;
                }

                // 获取表头行
                var headerRow = (IDictionary<string, object>)allRows[HeaderRowIndex];
                var headerValues = headerRow.Values
                    .Where(v => v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                    .Select(v => v!.ToString())
                    .Where(v => v != null)
                    .Cast<string>()
                    .ToList();

                // 确保有非空值
                if (headerValues.Count > 0)
                {
                    headers = headerValues;
                    _logger.LogInformation("Found headers from row {Index}: {Headers}",
                        HeaderRowIndex, string.Join(", ", headers));
                }
                else
                {
                    _logger.LogWarning("No valid headers found at row {Index}", HeaderRowIndex);
                }
            }
            else
            {
                _logger.LogWarning("Unsupported template file format: {Extension}", fileExtension);
                DialogService.ShowWarning($"不支持的模板文件格式：{fileExtension}");
                return;
            }

            if (headers == null || headers.Count == 0)
            {
                _logger.LogWarning("Template file is completely empty or has no valid headers");
                DialogService.ShowWarning("模板文件为空或没有有效的列名");
                return;
            }

            // 清空可用列和字段合并模式配置
            AvailableColumns.Clear();
            SelectedColumns.Clear();
            SelectedAvailableColumn = null;
            SelectedColumn = null;
            FieldMergeModes.Clear();

            // 添加新列（避免重复）
            foreach (var header in headers)
                if (!AvailableColumns.Contains(header))
                {
                    AvailableColumns.Add(header);
                    _logger.LogDebug("Added column: {Column}", header);

                    // 为每个字段添加合并模式配置（仅用于在线填表）
                    if (IsDataCollection)
                    {
                        // 从 schema 中获取列的类型
                        var columnType = "文本";
                        if (schema?.Columns != null)
                        {
                            var columnDef = schema.Columns.FirstOrDefault(c => c.Name == header);
                            if (columnDef != null) columnType = columnDef.Type;
                        }

                        FieldMergeModes.Add(new FieldMergeMode
                        {
                            FieldName = header,
                            FieldType = columnType,
                            MergeMode = MergeMode.Accumulate,
                            GroupByField = "所属部门"
                        });
                    }
                }

            _logger.LogInformation("Loaded {Count} columns from template file: {Columns}",
                AvailableColumns.Count, string.Join(", ", AvailableColumns));

            // 加载表头预览
            LoadHeaderPreview();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load columns from template file: {Path}. Error: {Error}",
                MergeTemplatePath, ex.Message);
            DialogService.ShowError($"加载模板文件失败：{ex.Message}");
        }
    }

    // --- 合并命令 ---
    [RelayCommand]
    private async Task MergeFiles()
    {
        // 防止重复点击
        if (IsMerging)
        {
            DialogService.ShowInfo("合并正在进行中，请稍候...");
            return;
        }

        if (string.IsNullOrWhiteSpace(MergeSourceFolder))
        {
            DialogService.ShowWarning("请选择源文件夹", "提示");
            return;
        }

        if (string.IsNullOrWhiteSpace(MergeOutputPath))
        {
            DialogService.ShowWarning("请选择输出文件", "提示");
            return;
        }

        try
        {
            IsMerging = true;
            MergeProgressValue = 0;
            MergeProgress = "正在合并...";
            MergeStatistics = string.Empty;
            ShowStatistics = false;
            _mainViewModel.StatusMessage = "合并进行中...";

            // 根据模板文件类型选择合并方法
            var fileExtension = Path.GetExtension(MergeTemplatePath).ToLowerInvariant();
            var isJsonTemplate = fileExtension == ".json";

            MergeResult? result;
            if (isJsonTemplate || _mainViewModel.SelectedTask?.TaskType == TaskType.DataCollection)
            {
                // JSON 模板或在线填表任务：使用统计合并（传入字段合并模式配置）
                _logger.LogInformation("Using JSON merge method. Template: {Template}, TaskType: {TaskType}",
                    MergeTemplatePath, _mainViewModel.SelectedTask?.TaskType);

                var fieldMergeModes = FieldMergeModes.ToDictionary(f => f.FieldName, f => new ColumnDefinition
                {
                    Name = f.FieldName,
                    Type = f.FieldType,
                    MergeMode = f.MergeMode,
                    GroupByField = f.GroupByField
                });

                result = await _mergeService.MergeJsonFilesWithStatisticsAsync(
                    MergeTemplatePath,
                    MergeSourceFolder,
                    MergeOutputPath,
                    fieldMergeModes);
            }
            else
            {
                // Excel 模板或文件收集任务：使用版本选择合并
                _logger.LogInformation(
                    "Using Excel merge method. Template: {Template}, TaskType: {TaskType}, HeaderRow: {Index}",
                    MergeTemplatePath, _mainViewModel.SelectedTask?.TaskType, HeaderRowIndex);

                var dedupCols = SelectedColumns.ToList();
                result = await _mergeService.MergeWithLatestVersionAsync(
                    MergeSourceFolder,
                    MergeOutputPath,
                    RemoveDuplicates,
                    dedupCols,
                    Separator,
                    MergeTemplatePath,
                    HeaderRowIndex);
            }

            if (result.IsSuccess)
            {
                MergeProgress = $"合并完成: {Path.GetFileName(MergeOutputPath)}";
                MergeStatistics = result.GetSummary();
                ShowStatistics = true;
                _mainViewModel.StatusMessage = result.GetSummary();
            }
            else
            {
                throw new Exception(result.ErrorMessage);
            }

            MergeProgressValue = 100;

            // 根据不同模式显示不同的成功消息
            if (ShowStatistics && !string.IsNullOrWhiteSpace(MergeStatistics))
                DialogService.ShowInfo(
                    $"合并成功！\n\n{MergeStatistics}\n\n输出文件: {MergeOutputPath}",
                    "完成");
            else
                DialogService.ShowInfo(
                    $"合并成功！\n输出文件: {MergeOutputPath}",
                    "完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Merge failed");
            MergeProgress = "合并失败";
            MergeStatistics = string.Empty;
            ShowStatistics = false;
            _mainViewModel.StatusMessage = $"合并失败: {ex.Message}";
            DialogService.ShowError($"合并失败: {ex.Message}");
        }
        finally
        {
            IsMerging = false;
        }
    }

    [RelayCommand]
    private void AddColumn()
    {
        if (string.IsNullOrWhiteSpace(SelectedAvailableColumn))
            return;

        // 保存列名用于日志
        var columnName = SelectedAvailableColumn;

        // 从可用列移除
        AvailableColumns.Remove(columnName);

        // 添加到已选列
        SelectedColumns.Add(columnName);

        // 清空选中项，避免引用已不存在的项
        SelectedAvailableColumn = null;

        _logger.LogInformation("Added column: {Column}", columnName);
    }

    [RelayCommand]
    private void RemoveColumn()
    {
        if (string.IsNullOrWhiteSpace(SelectedColumn))
            return;

        // 保存列名用于日志
        var columnName = SelectedColumn;

        // 从已选列移除
        SelectedColumns.Remove(columnName);

        // 添加回可用列
        AvailableColumns.Add(columnName);

        // 清空选中项，避免引用已不存在的项
        SelectedColumn = null;

        _logger.LogInformation("Removed column: {Column}", columnName);
    }

    [RelayCommand]
    private void Cancel()
    {
        _closeDialogCallback?.Invoke(false);
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(MergeOutputPath))
            return;

        try
        {
            var folderPath = Path.GetDirectoryName(MergeOutputPath);
            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                Process.Start("explorer.exe", folderPath);
                _logger.LogInformation("Opened output folder: {Folder}", folderPath);
            }
            else
            {
                DialogService.ShowWarning("文件夹不存在", "提示");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open output folder");
            DialogService.ShowError($"打开文件夹失败: {ex.Message}");
        }
    }

    public void SetCloseCallback(Action<bool> callback)
    {
        _closeDialogCallback = callback;
    }

    /// <summary>
    ///     加载表头行预览
    /// </summary>
    private void LoadHeaderPreview()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(MergeTemplatePath) || !File.Exists(MergeTemplatePath))
            {
                HeaderRowPreview = "请先选择模板文件";
                return;
            }

            var fileExtension = Path.GetExtension(MergeTemplatePath).ToLowerInvariant();

            if (fileExtension != ".xlsx")
            {
                HeaderRowPreview = "仅支持 Excel 文件的表头预览";
                return;
            }

            using var stream = File.OpenRead(MergeTemplatePath);
            var allRows = stream.Query().ToList();

            // 更新最大行号
            MaxHeaderRowIndex = Math.Max(0, allRows.Count - 1);

            // 检查行索引是否有效
            if (HeaderRowIndex < 0 || HeaderRowIndex >= allRows.Count)
            {
                HeaderRowPreview = $"行号 {HeaderRowIndex} 超出范围（共 {allRows.Count} 行）";
                return;
            }

            // 获取表头行
            var headerRow = (IDictionary<string, object>)allRows[HeaderRowIndex];
            var headers = headerRow.Values
                .Where(v => v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                .Select(v => v!.ToString()!)
                .ToList();

            if (headers.Count == 0)
            {
                HeaderRowPreview = $"第 {HeaderRowIndex} 行没有有效的列名";
                return;
            }

            // 显示前 5 个列名，超过则显示省略号
            var previewText = headers.Count <= 5
                ? string.Join(" | ", headers)
                : string.Join(" | ", headers.Take(5)) + " ...";

            HeaderRowPreview = $"第 {HeaderRowIndex} 行：{previewText}（共 {headers.Count} 列）";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load header preview");
            HeaderRowPreview = "加载预览失败";
        }
    }

    /// <summary>
    ///     表头行索引变更时更新预览和可用列
    /// </summary>
    partial void OnHeaderRowIndexChanged(int value)
    {
        LoadHeaderPreview();

        // 如果已选择模板文件，重新加载可用列
        if (!string.IsNullOrWhiteSpace(MergeTemplatePath) && File.Exists(MergeTemplatePath))
            LoadTemplateColumns();
        // 否则从源文件夹加载可用列
        else if (!string.IsNullOrWhiteSpace(MergeSourceFolder) && Directory.Exists(MergeSourceFolder))
            LoadAvailableColumns();
    }
}