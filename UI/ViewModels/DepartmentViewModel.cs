using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;
using MyLanServer.UI.Services;

namespace MyLanServer.UI.ViewModels;

public partial class DepartmentViewModel : ObservableObject
{
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILogger<DepartmentViewModel> _logger;

    // 关闭对话框的回调
    private Action<bool>? _closeDialogCallback;

    // 部门名称（用于编辑）
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SaveDepartmentCommand))]
    private string _departmentName = string.Empty;

    // 部门列表
    [ObservableProperty] private ObservableCollection<Department> _departments = new();

    // 过滤后的部门列表（用于搜索）
    [ObservableProperty] private ObservableCollection<Department> _filteredDepartments = new();

    // 编辑模式
    [ObservableProperty] private bool _isEditMode;

    // 搜索文本
    [ObservableProperty] private string _searchText = string.Empty;

    // 当前选中的部门
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditDepartmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteDepartmentCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveToTopCommand))]
    private Department? _selectedDepartment;

    public DepartmentViewModel(
        IDepartmentRepository departmentRepository,
        ILogger<DepartmentViewModel> logger)
    {
        _departmentRepository = departmentRepository;
        _logger = logger;

        LoadDepartments();
    }

    /// <summary>
    ///     选中部门变化时自动填充到输入框
    /// </summary>
    partial void OnSelectedDepartmentChanged(Department? value)
    {
        if (value != null)
        {
            // 选中部门时，自动填充部门名称并进入编辑模式
            DepartmentName = value.Name;
            IsEditMode = true;
        }
        else
        {
            // 取消选中时，清空输入框并退出编辑模式
            DepartmentName = string.Empty;
            IsEditMode = false;
        }
    }

    /// <summary>
    ///     加载部门列表
    /// </summary>
    private async void LoadDepartments()
    {
        try
        {
            var departments = await _departmentRepository.GetAllAsync();
            Departments.Clear();
            foreach (var dept in departments) Departments.Add(dept);

            // 同时更新过滤后的列表
            ApplySearchFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载部门列表失败");
            DialogService.ShowError("加载部门列表失败", ex.Message);
        }
    }

    /// <summary>
    ///     搜索文本变化时应用过滤
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        ApplySearchFilter();
    }

    /// <summary>
    ///     应用搜索过滤
    /// </summary>
    private void ApplySearchFilter()
    {
        FilteredDepartments.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // 如果搜索文本为空，显示所有部门
            foreach (var dept in Departments) FilteredDepartments.Add(dept);
        }
        else
        {
            // 否则只显示匹配的部门
            var searchLower = SearchText.ToLower();
            foreach (var dept in Departments)
                if (dept.Name.ToLower().Contains(searchLower))
                    FilteredDepartments.Add(dept);
        }
    }

    /// <summary>
    ///     添加部门命令
    /// </summary>
    [RelayCommand]
    private void AddDepartment()
    {
        IsEditMode = false;
        DepartmentName = string.Empty;
        SelectedDepartment = null;
    }

    /// <summary>
    ///     编辑部门命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditDeleteMove))]
    private void EditDepartment()
    {
        if (SelectedDepartment == null)
        {
            DialogService.ShowWarning("请先选择要编辑的部门");
            return;
        }

        IsEditMode = true;
        DepartmentName = SelectedDepartment.Name;
    }

    /// <summary>
    ///     保存部门命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveDepartment))]
    private async Task SaveDepartment()
    {
        if (string.IsNullOrWhiteSpace(DepartmentName))
        {
            DialogService.ShowWarning("部门名称不能为空");
            return;
        }

        try
        {
            if (IsEditMode && SelectedDepartment != null)
            {
                // 更新现有部门
                SelectedDepartment.Name = DepartmentName.Trim();
                var success = await _departmentRepository.UpdateAsync(SelectedDepartment);
                if (!success)
                {
                    DialogService.ShowError("更新部门失败");
                    return;
                }

                DialogService.ShowInfo("部门更新成功");
            }
            else
            {
                // 创建新部门
                var newDepartment = new Department
                {
                    Name = DepartmentName.Trim(),
                    SortOrder = Departments.Count
                };

                var success = await _departmentRepository.CreateAsync(newDepartment);
                if (!success)
                {
                    DialogService.ShowWarning("该部门名称已存在，请使用其他名称");
                    return;
                }

                DialogService.ShowInfo("部门创建成功");
            }

            // 重新加载部门列表
            LoadDepartments();

            // 清空编辑状态
            IsEditMode = false;
            DepartmentName = string.Empty;
            SelectedDepartment = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存部门失败");
            DialogService.ShowError($"保存部门失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     是否可以保存部门
    /// </summary>
    private bool CanSaveDepartment()
    {
        return !string.IsNullOrWhiteSpace(DepartmentName);
    }

    /// <summary>
    ///     是否可以编辑、删除或移动部门
    /// </summary>
    private bool CanEditDeleteMove()
    {
        return SelectedDepartment != null;
    }

    /// <summary>
    ///     删除部门命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditDeleteMove))]
    private async Task DeleteDepartment()
    {
        if (SelectedDepartment == null)
        {
            DialogService.ShowWarning("请先选择要删除的部门");
            return;
        }

        var result = DialogService.ShowConfirm(
            $"确定要删除部门 \"{SelectedDepartment.Name}\" 吗？\n\n注意：删除部门后，历史提交记录中的部门名称将保持不变，只是无法再选择该部门。");

        if (!result) return;

        try
        {
            var success = await _departmentRepository.DeleteAsync(SelectedDepartment.Id);
            if (!success)
            {
                DialogService.ShowError("删除部门失败");
                return;
            }

            DialogService.ShowInfo("部门删除成功");
            LoadDepartments();

            // 清空选择
            SelectedDepartment = null;
            DepartmentName = string.Empty;
            IsEditMode = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除部门失败");
            DialogService.ShowError($"删除部门失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     清空全部部门命令
    /// </summary>
    [RelayCommand]
    private async Task ClearAllDepartments()
    {
        if (Departments.Count == 0)
        {
            DialogService.ShowWarning("当前没有部门可清空");
            return;
        }

        var result = DialogService.ShowConfirm(
            $"确定要清空全部 {Departments.Count} 个部门吗？\n\n⚠️ 此操作不可恢复！\n\n注意：清空部门后，历史提交记录中的部门名称将保持不变，只是无法再选择这些部门。");

        if (!result) return;

        try
        {
            var success = await _departmentRepository.ClearAllAsync();
            if (!success)
            {
                DialogService.ShowError("清空部门失败");
                return;
            }

            DialogService.ShowInfo($"成功清空全部 {Departments.Count} 个部门");
            LoadDepartments();

            // 清空选择和编辑状态
            SelectedDepartment = null;
            DepartmentName = string.Empty;
            IsEditMode = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空部门失败");
            DialogService.ShowError($"清空部门失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     上移部门命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditDeleteMove))]
    private async Task MoveUp()
    {
        if (SelectedDepartment == null)
        {
            DialogService.ShowWarning("请先选择要移动的部门");
            return;
        }

        try
        {
            var success = await _departmentRepository.MoveUpAsync(SelectedDepartment.Id);
            if (!success)
            {
                DialogService.ShowWarning("上移失败，可能已经是第一个部门");
                return;
            }

            LoadDepartments();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上移部门失败");
            DialogService.ShowError($"上移部门失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     下移部门命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditDeleteMove))]
    private async Task MoveDown()
    {
        if (SelectedDepartment == null)
        {
            DialogService.ShowWarning("请先选择要移动的部门");
            return;
        }

        try
        {
            var success = await _departmentRepository.MoveDownAsync(SelectedDepartment.Id);
            if (!success)
            {
                DialogService.ShowWarning("下移失败，可能已经是最后一个部门");
                return;
            }

            LoadDepartments();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下移部门失败");
            DialogService.ShowError($"下移部门失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     置顶部门命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditDeleteMove))]
    private async Task MoveToTop()
    {
        if (SelectedDepartment == null)
        {
            DialogService.ShowWarning("请先选择要置顶的部门");
            return;
        }

        try
        {
            var success = await _departmentRepository.MoveToTopAsync(SelectedDepartment.Id);
            if (!success)
            {
                DialogService.ShowWarning("置顶失败，可能已经是第一个部门");
                return;
            }

            LoadDepartments();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "置顶部门失败");
            DialogService.ShowError($"置顶部门失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     导入部门命令
    /// </summary>
    [RelayCommand]
    private async Task ImportDepartments()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Excel 文件|*.xlsx;*.xls|所有文件|*.*",
            Title = "选择要导入的 Excel 文件"
        };

        if (openFileDialog.ShowDialog() != true) return;

        try
        {
            var filePath = openFileDialog.FileName;
            var importedCount = await _departmentRepository.ImportFromExcelAsync(filePath);

            DialogService.ShowInfo($"成功导入 {importedCount} 个部门");
            LoadDepartments();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入部门失败");
            DialogService.ShowError($"导入部门失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     导出部门命令
    /// </summary>
    [RelayCommand]
    private async Task ExportDepartments()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel 文件|*.xlsx",
            Title = "保存部门列表",
            FileName = "部门列表.xlsx"
        };

        if (saveFileDialog.ShowDialog() != true) return;

        try
        {
            var filePath = saveFileDialog.FileName;
            var success = await _departmentRepository.ExportToExcelAsync(filePath);

            if (!success)
            {
                DialogService.ShowWarning("导出失败，可能没有部门数据");
                return;
            }

            DialogService.ShowInfo($"部门列表已导出到：{filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出部门失败");
            DialogService.ShowError($"导出部门失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     取消编辑命令
    /// </summary>
    [RelayCommand]
    private void CancelEdit()
    {
        IsEditMode = false;
        DepartmentName = string.Empty;
        SelectedDepartment = null;
    }

    /// <summary>
    ///     关闭对话框命令
    /// </summary>
    [RelayCommand]
    private void CloseDialog()
    {
        _closeDialogCallback?.Invoke(true);
    }

    /// <summary>
    ///     设置关闭对话框的回调
    /// </summary>
    public void SetCloseDialogCallback(Action<bool> callback)
    {
        _closeDialogCallback = callback;
    }
}