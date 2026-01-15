using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;
using MyLanServer.UI.Services;
using MyLanServer.UI.Views;

namespace MyLanServer.UI.ViewModels;

public partial class PersonViewModel : ObservableObject
{
    // 列配置文件路径（使用绝对路径，指向项目根目录）
    private static readonly string ColumnConfigFilePath = GetProjectConfigFilePath();
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IIdCardValidationService _idCardValidationService;
    private readonly ILogger<PersonViewModel> _logger;
    private readonly IPersonRepository _personRepository;

    // 只读字段（自动计算）
    [ObservableProperty] private int? _age;
    [ObservableProperty] private DateTime? _birthDate;

    // 关闭对话框的回调
    private Action<bool>? _closeDialogCallback;

    // 列配置集合
    [ObservableProperty] private ObservableCollection<ColumnConfig> _columnConfigs = new();

    [ObservableProperty] private string? _contact1;
    [ObservableProperty] private string? _contact2;
    [ObservableProperty] private string? _currentAddress;
    [ObservableProperty] private string? _department;

    // 部门列表（用于下拉选择）
    [ObservableProperty] private ObservableCollection<string> _departments = new();
    [ObservableProperty] private string? _employeeNumber;

    // 过滤后的人员列表（用于搜索）
    [ObservableProperty] private ObservableCollection<Person> _filteredPersons = new();
    [ObservableProperty] private string? _gender;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SavePersonCommand))]
    private string _idCard = string.Empty;

    // 身份证号验证消息
    [ObservableProperty] private string? _idCardValidationMessage;

    // 编辑模式
    [ObservableProperty] private bool _isEditMode;

    // 编辑字段
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(SavePersonCommand))]
    private string _name = string.Empty;

    // 人员列表
    [ObservableProperty] private ObservableCollection<Person> _persons = new();
    [ObservableProperty] private string? _position;
    [ObservableProperty] private string? _rank1;
    [ObservableProperty] private string? _rank2;
    [ObservableProperty] private string? _registeredAddress;

    // 搜索文本
    [ObservableProperty] private string _searchText = string.Empty;

    // 当前选中的人员
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditPersonCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeletePersonCommand))]
    private Person? _selectedPerson;

    [ObservableProperty] private DateTime? _workStartDate;

    public PersonViewModel(
        IPersonRepository personRepository,
        IIdCardValidationService idCardValidationService,
        IDepartmentRepository departmentRepository,
        ILogger<PersonViewModel> logger)
    {
        _personRepository = personRepository;
        _idCardValidationService = idCardValidationService;
        _departmentRepository = departmentRepository;
        _logger = logger;

        LoadColumnConfig();
        LoadPersons();
        LoadDepartments();

        // 监听ColumnConfigs集合变更
        ColumnConfigs.CollectionChanged += (s, e) =>
        {
            _logger.LogInformation(
                "ColumnConfigs集合变更: Action={Action}, NewItemsCount={NewCount}, OldItemsCount={OldCount}",
                e.Action, e.NewItems?.Count ?? 0, e.OldItems?.Count ?? 0);
        };
    }

    // 是否正在从 DataGrid 拖拽更新列配置（避免无限循环）
    public bool IsUpdatingFromDataGrid { get; private set; }

    /// <summary>
    ///     获取项目根目录的配置文件路径
    /// </summary>
    private static string GetProjectConfigFilePath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var projectRoot = baseDir;

        // 如果当前在 bin 目录下，向上查找项目根目录
        while (projectRoot != null && Directory.Exists(projectRoot))
        {
            // 检查是否包含项目文件或 .git 目录
            if (Directory.GetFiles(projectRoot, "*.csproj").Length > 0 ||
                Directory.Exists(Path.Combine(projectRoot, ".git")))
                break;

            var parent = Directory.GetParent(projectRoot);
            if (parent == null) break;

            projectRoot = parent.FullName;
        }

        return Path.Combine(projectRoot ?? baseDir, "config", "person_column_config.json");
    }

    /// <summary>
    ///     选中人员变化时自动填充到输入框
    /// </summary>
    partial void OnSelectedPersonChanged(Person? value)
    {
        if (value != null)
        {
            // 选中人员时，自动填充所有字段并进入编辑模式
            Name = value.Name;
            IdCard = value.IdCard;
            Contact1 = value.Contact1 ?? string.Empty;
            Contact2 = value.Contact2 ?? string.Empty;
            Department = value.Department ?? string.Empty;
            RegisteredAddress = value.RegisteredAddress ?? string.Empty;
            CurrentAddress = value.CurrentAddress ?? string.Empty;
            EmployeeNumber = value.EmployeeNumber ?? string.Empty;
            Rank1 = value.Rank1 ?? string.Empty;
            Rank2 = value.Rank2 ?? string.Empty;
            Position = value.Position ?? string.Empty;
            Age = value.Age;
            Gender = value.Gender ?? string.Empty;
            BirthDate = value.BirthDate;
            WorkStartDate = value.WorkStartDate;
            IsEditMode = true;
        }
        else
        {
            // 取消选中时，清空输入框并退出编辑模式
            ClearEditFields();
            IsEditMode = false;
        }
    }

    /// <summary>
    ///     身份证号变化时自动计算年龄、性别、出生日期
    /// </summary>
    partial void OnIdCardChanged(string value)
    {
        _logger.LogDebug("身份证号变化: '{IdCard}'", value);

        if (string.IsNullOrWhiteSpace(value))
        {
            _logger.LogDebug("身份证号为空，清空自动计算字段和验证消息");
            Age = null;
            Gender = null;
            BirthDate = null;
            IdCardValidationMessage = null;
            return;
        }

        var validationError = _idCardValidationService.GetValidationError(value);

        if (validationError == null)
        {
            Age = _idCardValidationService.GetAge(value);
            Gender = _idCardValidationService.GetGender(value);
            BirthDate = _idCardValidationService.GetBirthDate(value);
            IdCardValidationMessage = null;
            _logger.LogDebug("身份证号有效: Age={Age}, Gender={Gender}, BirthDate={BirthDate}",
                Age, Gender, BirthDate);
        }
        else
        {
            IdCardValidationMessage = validationError;
            Age = null;
            Gender = null;
            BirthDate = null;
            _logger.LogDebug("身份证号无效: {Error}", validationError);
        }
    }

    /// <summary>
    ///     加载人员列表
    /// </summary>
    private async void LoadPersons()
    {
        _logger.LogInformation("=== 开始加载人员列表 ===");
        try
        {
            var persons = await _personRepository.GetAllAsync();
            _logger.LogInformation("从数据库获取到 {Count} 个人员", persons.Count());

            Persons.Clear();
            foreach (var person in persons) Persons.Add(person);

            _logger.LogInformation("已添加 {Count} 个人员到列表", Persons.Count);

            // 同时更新过滤后的列表
            ApplySearchFilter();
            _logger.LogInformation("人员列表加载完成，过滤后: {Count}", FilteredPersons.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载人员列表失败");
            DialogService.ShowError("加载人员列表失败", ex.Message);
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
            foreach (var dept in departments) Departments.Add(dept.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载部门列表失败");
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
        FilteredPersons.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            // 如果搜索文本为空，显示所有人员
            foreach (var person in Persons) FilteredPersons.Add(person);
        }
        else
        {
            // 否则只显示匹配的人员
            var searchLower = SearchText.ToLower();
            foreach (var person in Persons)
                if (person.Name.ToLower().Contains(searchLower) ||
                    person.IdCard.ToLower().Contains(searchLower) ||
                    (person.Contact1 != null && person.Contact1.ToLower().Contains(searchLower)) ||
                    (person.Contact2 != null && person.Contact2.ToLower().Contains(searchLower)))
                    FilteredPersons.Add(person);
        }
    }

    /// <summary>
    ///     清空编辑字段
    /// </summary>
    private void ClearEditFields()
    {
        Name = string.Empty;
        IdCard = string.Empty;
        IdCardValidationMessage = null;
        Contact1 = null;
        Contact2 = null;
        Department = null;
        RegisteredAddress = null;
        CurrentAddress = null;
        EmployeeNumber = null;
        Rank1 = null;
        Rank2 = null;
        Position = null;
        Age = null;
        Gender = null;
        BirthDate = null;
        WorkStartDate = null;
    }

    /// <summary>
    ///     添加人员命令
    /// </summary>
    [RelayCommand]
    private void AddPerson()
    {
        _logger.LogInformation("=== AddPerson 命令被触发 ===");
        _logger.LogInformation("当前状态: IsEditMode={IsEditMode}", IsEditMode);
        IsEditMode = false;
        ClearEditFields();
        SelectedPerson = null;
        _logger.LogInformation("AddPerson 完成: IsEditMode={IsEditMode}, SelectedPerson={SelectedPerson}", IsEditMode,
            SelectedPerson);
    }

    /// <summary>
    ///     编辑人员命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditDelete))]
    private void EditPerson()
    {
        if (SelectedPerson == null)
        {
            DialogService.ShowWarning("请先选择要编辑的人员");
            return;
        }

        IsEditMode = true;
    }

    /// <summary>
    ///     保存人员命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSavePerson))]
    private async Task SavePerson()
    {
        _logger.LogInformation("=== SavePerson 命令被触发 ===");
        _logger.LogInformation(
            "输入数据: Name='{Name}', IdCard='{IdCard}', Contact1='{Contact1}', Contact2='{Contact2}', Department='{Department}', Age={Age}, Gender={Gender}",
            Name, IdCard, Contact1, Contact2, Department, Age, Gender);
        _logger.LogInformation("当前状态: IsEditMode={IsEditMode}, SelectedPerson={SelectedPerson}",
            IsEditMode, SelectedPerson?.Id);

        if (string.IsNullOrWhiteSpace(Name))
        {
            _logger.LogWarning("验证失败: 姓名为空");
            DialogService.ShowWarning("姓名不能为空");
            return;
        }

        if (string.IsNullOrWhiteSpace(IdCard))
        {
            _logger.LogWarning("验证失败: 身份证号为空");
            DialogService.ShowWarning("身份证号不能为空");
            return;
        }

        // 验证身份证号
        var validationError = _idCardValidationService.GetValidationError(IdCard);
        if (validationError != null)
        {
            _logger.LogWarning("验证失败: 身份证号验证失败 - {Error}", validationError);
            DialogService.ShowWarning($"身份证号验证失败：{validationError}");
            return;
        }

        _logger.LogInformation("验证通过，准备保存人员信息");

        try
        {
            if (IsEditMode && SelectedPerson != null)
            {
                _logger.LogInformation("模式: 更新现有人员 - Id={Id}", SelectedPerson.Id);
                // 更新现有人员
                SelectedPerson.Name = Name.Trim();
                SelectedPerson.IdCard = IdCard.Trim();
                SelectedPerson.Contact1 = Contact1?.Trim();
                SelectedPerson.Contact2 = Contact2?.Trim();
                SelectedPerson.Department = Department?.Trim();
                SelectedPerson.RegisteredAddress = RegisteredAddress?.Trim();
                SelectedPerson.CurrentAddress = CurrentAddress?.Trim();
                SelectedPerson.EmployeeNumber = EmployeeNumber?.Trim();
                SelectedPerson.Rank1 = Rank1?.Trim();
                SelectedPerson.Rank2 = Rank2?.Trim();
                SelectedPerson.Position = Position?.Trim();
                SelectedPerson.Age = Age;
                SelectedPerson.Gender = Gender;
                SelectedPerson.BirthDate = BirthDate;
                SelectedPerson.WorkStartDate = WorkStartDate;

                var success = await _personRepository.UpdateAsync(SelectedPerson);
                if (!success)
                {
                    _logger.LogWarning("更新失败: 可能身份证号已存在");
                    DialogService.ShowError("更新人员失败，身份证号可能已存在");
                    return;
                }

                _logger.LogInformation("更新成功: Id={Id}", SelectedPerson.Id);
                DialogService.ShowInfo("人员更新成功");
            }
            else
            {
                _logger.LogInformation("模式: 创建新人员");
                // 创建新人员
                var newPerson = new Person
                {
                    Name = Name.Trim(),
                    IdCard = IdCard.Trim(),
                    Contact1 = Contact1?.Trim(),
                    Contact2 = Contact2?.Trim(),
                    Department = Department?.Trim(),
                    RegisteredAddress = RegisteredAddress?.Trim(),
                    CurrentAddress = CurrentAddress?.Trim(),
                    EmployeeNumber = EmployeeNumber?.Trim(),
                    Rank1 = Rank1?.Trim(),
                    Rank2 = Rank2?.Trim(),
                    Position = Position?.Trim(),
                    Age = Age,
                    Gender = Gender,
                    BirthDate = BirthDate,
                    WorkStartDate = WorkStartDate,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _logger.LogInformation(
                    "准备创建人员: Name={Name}, IdCard={IdCard}, Contact1={Contact1}, Contact2={Contact2}, Department={Department}, Age={Age}, Gender={Gender}, BirthDate={BirthDate}",
                    newPerson.Name, newPerson.IdCard, newPerson.Contact1, newPerson.Contact2, newPerson.Department,
                    newPerson.Age, newPerson.Gender, newPerson.BirthDate);

                var success = await _personRepository.CreateAsync(newPerson);
                _logger.LogInformation("CreateAsync 返回结果: {Success}", success);

                if (!success)
                {
                    _logger.LogWarning("创建失败: 身份证号已存在 - {IdCard}", newPerson.IdCard);
                    DialogService.ShowWarning("该身份证号已存在，请使用其他身份证号");
                    return;
                }

                _logger.LogInformation("创建成功: Name={Name}, IdCard={IdCard}", newPerson.Name, newPerson.IdCard);
                DialogService.ShowInfo("人员创建成功");
            }

            // 重新加载人员列表
            _logger.LogInformation("重新加载人员列表");
            LoadPersons();

            // 清空编辑状态
            _logger.LogInformation("清空编辑状态");
            IsEditMode = false;
            ClearEditFields();
            SelectedPerson = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存人员失败");
            DialogService.ShowError($"保存人员失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     是否可以保存人员
    /// </summary>
    private bool CanSavePerson()
    {
        return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(IdCard);
    }

    /// <summary>
    ///     是否可以编辑或删除人员
    /// </summary>
    private bool CanEditDelete()
    {
        return SelectedPerson != null;
    }

    /// <summary>
    ///     删除人员命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditDelete))]
    private async Task DeletePerson()
    {
        if (SelectedPerson == null)
        {
            DialogService.ShowWarning("请先选择要删除的人员");
            return;
        }

        var result = DialogService.ShowConfirm(
            $"确定要删除人员 \"{SelectedPerson.Name}\" 吗？\n\n身份证号：{SelectedPerson.IdCard}");

        if (!result) return;

        try
        {
            var success = await _personRepository.DeleteAsync(SelectedPerson.Id);
            if (!success)
            {
                DialogService.ShowError("删除人员失败");
                return;
            }

            DialogService.ShowInfo("人员删除成功");
            LoadPersons();

            // 清空选择
            SelectedPerson = null;
            ClearEditFields();
            IsEditMode = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除人员失败");
            DialogService.ShowError($"删除人员失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     清空全部人员命令
    /// </summary>
    [RelayCommand]
    private async Task ClearAllPersons()
    {
        if (Persons.Count == 0)
        {
            DialogService.ShowWarning("当前没有人员可清空");
            return;
        }

        var result = DialogService.ShowConfirm(
            $"确定要清空全部 {Persons.Count} 个人员吗？\n\n⚠️ 此操作不可恢复！");

        if (!result) return;

        try
        {
            var success = await _personRepository.ClearAllAsync();
            if (!success)
            {
                DialogService.ShowError("清空人员失败");
                return;
            }

            DialogService.ShowInfo($"成功清空全部 {Persons.Count} 个人员");
            LoadPersons();

            // 清空选择和编辑状态
            SelectedPerson = null;
            ClearEditFields();
            IsEditMode = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空人员失败");
            DialogService.ShowError($"清空人员失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     导入人员命令
    /// </summary>
    [RelayCommand]
    private async Task ImportPersons()
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
            var importedCount = await _personRepository.ImportFromExcelAsync(filePath);

            DialogService.ShowInfo($"成功导入 {importedCount} 个人员");
            LoadPersons();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入人员失败");
            DialogService.ShowError($"导入人员失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     导出人员命令
    /// </summary>
    [RelayCommand]
    private async Task ExportPersons()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel 文件|*.xlsx",
            Title = "保存人员列表",
            FileName = "人员列表.xlsx"
        };

        if (saveFileDialog.ShowDialog() != true) return;

        try
        {
            var filePath = saveFileDialog.FileName;
            var success = await _personRepository.ExportToExcelAsync(filePath);

            if (!success)
            {
                DialogService.ShowWarning("导出失败，可能没有人员数据");
                return;
            }

            DialogService.ShowInfo($"人员列表已导出到：{filePath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出人员失败");
            DialogService.ShowError($"导出人员失败：{ex.Message}");
        }
    }

    /// <summary>
    ///     取消编辑命令
    /// </summary>
    [RelayCommand]
    private void CancelEdit()
    {
        IsEditMode = false;
        ClearEditFields();
        SelectedPerson = null;
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

    /// <summary>
    ///     加载列配置
    /// </summary>
    private void LoadColumnConfig()
    {
        _logger.LogInformation("=== LoadColumnConfig 开始 ===");
        _logger.LogInformation("配置文件路径: {FilePath}", ColumnConfigFilePath);
        _logger.LogInformation("配置文件存在: {Exists}", File.Exists(ColumnConfigFilePath));

        try
        {
            if (File.Exists(ColumnConfigFilePath))
            {
                var json = File.ReadAllText(ColumnConfigFilePath);
                var configs = JsonSerializer.Deserialize<List<ColumnConfig>>(json);
                if (configs != null)
                {
                    // 清空现有集合并重新添加，保持对象引用不变
                    ColumnConfigs.Clear();
                    foreach (var c in configs)
                        ColumnConfigs.Add(new ColumnConfig
                        {
                            Header = c.Header,
                            BindingPath = c.BindingPath,
                            IsVisible = c.IsVisible,
                            Width = c.Width,
                            MinWidth = c.MinWidth,
                            StringFormat = c.StringFormat,
                            SortOrder = c.SortOrder,
                            AllowTextWrapping = c.AllowTextWrapping,
                            TextTrimming = c.TextTrimming
                        });

                    _logger.LogInformation("从文件加载列配置成功，共 {Count} 列", ColumnConfigs.Count);
                    for (var i = 0; i < ColumnConfigs.Count; i++)
                        _logger.LogInformation("  [{Index}] {Header} - IsVisible={IsVisible}",
                            i, ColumnConfigs[i].Header, ColumnConfigs[i].IsVisible);

                    _logger.LogInformation("=== LoadColumnConfig 完成（从文件） ===");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载列配置失败，使用默认配置");
        }

        // 使用默认配置
        _logger.LogInformation("配置文件不存在或加载失败，使用默认列配置");
        InitializeDefaultColumnConfig();
    }

    /// <summary>
    ///     初始化默认列配置
    /// </summary>
    private void InitializeDefaultColumnConfig()
    {
        _logger.LogInformation("=== InitializeDefaultColumnConfig 开始 ===");
        _logger.LogInformation("使用默认列配置");

        // 清空现有集合并重新添加，保持对象引用不变
        ColumnConfigs.Clear();
        ColumnConfigs.Add(new ColumnConfig
            { Header = "姓名", BindingPath = "Name", IsVisible = true, Width = 100, MinWidth = 80, SortOrder = 0 });
        ColumnConfigs.Add(new ColumnConfig
            { Header = "身份证号", BindingPath = "IdCard", IsVisible = true, Width = 150, MinWidth = 120, SortOrder = 1 });
        ColumnConfigs.Add(new ColumnConfig
        {
            Header = "联系方式1", BindingPath = "Contact1", IsVisible = true, Width = 120, MinWidth = 100, SortOrder = 2
        });
        ColumnConfigs.Add(new ColumnConfig
        {
            Header = "联系方式2", BindingPath = "Contact2", IsVisible = true, Width = 120, MinWidth = 100, SortOrder = 3
        });
        ColumnConfigs.Add(new ColumnConfig
            { Header = "部门", BindingPath = "Department", IsVisible = true, Width = 100, MinWidth = 80, SortOrder = 4 });
        ColumnConfigs.Add(new ColumnConfig
            { Header = "性别", BindingPath = "Gender", IsVisible = true, Width = 60, MinWidth = 50, SortOrder = 5 });
        ColumnConfigs.Add(new ColumnConfig
            { Header = "年龄", BindingPath = "Age", IsVisible = true, Width = 60, MinWidth = 50, SortOrder = 6 });
        ColumnConfigs.Add(new ColumnConfig
        {
            Header = "出生日期", BindingPath = "BirthDate", IsVisible = true, Width = 110, MinWidth = 100,
            StringFormat = "yyyy-MM-dd", SortOrder = 7
        });
        ColumnConfigs.Add(new ColumnConfig
        {
            Header = "户籍地址", BindingPath = "RegisteredAddress", IsVisible = true, Width = 200, MinWidth = 150,
            AllowTextWrapping = true, TextTrimming = "CharacterEllipsis", SortOrder = 8
        });
        ColumnConfigs.Add(new ColumnConfig
        {
            Header = "现住址", BindingPath = "CurrentAddress", IsVisible = true, Width = 200, MinWidth = 150,
            AllowTextWrapping = true, TextTrimming = "CharacterEllipsis", SortOrder = 9
        });
        ColumnConfigs.Add(new ColumnConfig
        {
            Header = "工号", BindingPath = "EmployeeNumber", IsVisible = true, Width = 100, MinWidth = 80, SortOrder = 10
        });
        ColumnConfigs.Add(new ColumnConfig
            { Header = "职级1", BindingPath = "Rank1", IsVisible = true, Width = 80, MinWidth = 60, SortOrder = 11 });
        ColumnConfigs.Add(new ColumnConfig
            { Header = "职级2", BindingPath = "Rank2", IsVisible = true, Width = 80, MinWidth = 60, SortOrder = 12 });
        ColumnConfigs.Add(new ColumnConfig
            { Header = "职务", BindingPath = "Position", IsVisible = true, Width = 100, MinWidth = 80, SortOrder = 13 });
        ColumnConfigs.Add(new ColumnConfig
        {
            Header = "参与工作时间", BindingPath = "WorkStartDate", IsVisible = true, Width = 120, MinWidth = 100,
            StringFormat = "yyyy-MM-dd", SortOrder = 14
        });
        ColumnConfigs.Add(new ColumnConfig
        {
            Header = "创建时间", BindingPath = "CreatedAt", IsVisible = true, Width = 140, MinWidth = 130,
            StringFormat = "yyyy-MM-dd HH:mm", SortOrder = 15
        });
        ColumnConfigs.Add(new ColumnConfig
        {
            Header = "更新时间", BindingPath = "UpdatedAt", IsVisible = true, Width = 140, MinWidth = 130,
            StringFormat = "yyyy-MM-dd HH:mm", SortOrder = 16
        });

        _logger.LogInformation("默认列配置初始化完成，共 {Count} 列", ColumnConfigs.Count);
        for (var i = 0; i < ColumnConfigs.Count; i++)
            _logger.LogInformation("  [{Index}] {Header} - IsVisible={IsVisible}",
                i, ColumnConfigs[i].Header, ColumnConfigs[i].IsVisible);

        _logger.LogInformation("=== InitializeDefaultColumnConfig 完成 ===");
    }

    /// <summary>
    ///     保存列配置
    /// </summary>
    private void SaveColumnConfig()
    {
        _logger.LogInformation("=== SaveColumnConfig 开始 ===");
        _logger.LogInformation("保存列配置到: {FilePath}", ColumnConfigFilePath);
        _logger.LogInformation("当前列配置状态:");
        for (var i = 0; i < ColumnConfigs.Count; i++)
            _logger.LogInformation("  [{Index}] {Header} - IsVisible={IsVisible}",
                i, ColumnConfigs[i].Header, ColumnConfigs[i].IsVisible);

        try
        {
            var directory = Path.GetDirectoryName(ColumnConfigFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(ColumnConfigs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ColumnConfigFilePath, json);
            _logger.LogInformation("列配置保存成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存列配置失败");
        }

        _logger.LogInformation("=== SaveColumnConfig 完成 ===");
    }

    /// <summary>
    ///     打开列选择器对话框命令
    /// </summary>
    [RelayCommand]
    private void OpenColumnSelector()
    {
        _logger.LogInformation("=== OpenColumnSelector 开始 ===");
        _logger.LogInformation("当前列配置状态:");
        for (var i = 0; i < ColumnConfigs.Count; i++)
            _logger.LogInformation("  [{Index}] {Header} - IsVisible={IsVisible}",
                i, ColumnConfigs[i].Header, ColumnConfigs[i].IsVisible);

        try
        {
            var selectorViewModel = App.ServiceProvider.GetRequiredService<ColumnSelectorViewModel>();
            selectorViewModel.SetColumnConfigs(new ObservableCollection<ColumnConfig>(ColumnConfigs));

            var dialog = new ColumnSelectorDialog(selectorViewModel);
            var result = dialog.ShowDialog();

            if (result == true)
            {
                _logger.LogInformation("用户确认保存列配置");
                _logger.LogInformation("更新后的列配置状态:");
                for (var i = 0; i < selectorViewModel.ColumnConfigs.Count; i++)
                    _logger.LogInformation("  [{Index}] {Header} - IsVisible={IsVisible}",
                        i, selectorViewModel.ColumnConfigs[i].Header, selectorViewModel.ColumnConfigs[i].IsVisible);

                // 按照新的顺序重新排列 ColumnConfigs 集合中的元素
                var reorderedConfigs = new List<ColumnConfig>();
                foreach (var selectorConfig in selectorViewModel.ColumnConfigs)
                {
                    // 在 ColumnConfigs 中找到对应的元素
                    var originalConfig = ColumnConfigs.FirstOrDefault(c => c.Header == selectorConfig.Header);
                    if (originalConfig != null)
                    {
                        // 更新属性值
                        originalConfig.IsVisible = selectorConfig.IsVisible;
                        originalConfig.Width = selectorConfig.Width;
                        originalConfig.MinWidth = selectorConfig.MinWidth;
                        originalConfig.SortOrder = selectorConfig.SortOrder;
                        reorderedConfigs.Add(originalConfig);
                    }
                }

                // 清空并重新添加，保持对象引用不变
                ColumnConfigs.Clear();
                foreach (var config in reorderedConfigs) ColumnConfigs.Add(config);

                SaveColumnConfig();
                _logger.LogInformation("列配置已更新（重新排序）");
            }
            else
            {
                _logger.LogInformation("用户取消列配置修改");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开列选择器失败");
            DialogService.ShowError($"打开列选择器失败：{ex.Message}");
        }

        _logger.LogInformation("=== OpenColumnSelector 完成 ===");
    }

    /// <summary>
    ///     从 DataGrid 的列顺序更新 ColumnConfigs 集合
    /// </summary>
    public void UpdateColumnOrderFromDataGrid(List<string> newColumnOrder)
    {
        _logger.LogInformation("=== UpdateColumnOrderFromDataGrid 开始 ===");
        _logger.LogInformation("新列顺序: {Order}", string.Join(", ", newColumnOrder));

        IsUpdatingFromDataGrid = true;
        try
        {
            // 创建临时列表保存重新排序后的配置
            var reorderedConfigs = new List<ColumnConfig>();

            foreach (var header in newColumnOrder)
            {
                // 在 ColumnConfigs 中找到对应的配置
                var config = ColumnConfigs.FirstOrDefault(c => c.Header == header);
                if (config != null) reorderedConfigs.Add(config);
            }

            // 如果列数不匹配，记录警告
            if (reorderedConfigs.Count != ColumnConfigs.Count)
                _logger.LogWarning("列数不匹配: DataGrid={DataGridCount}, ColumnConfigs={ConfigsCount}",
                    reorderedConfigs.Count, ColumnConfigs.Count);

            // 清空并重新添加
            ColumnConfigs.Clear();
            foreach (var config in reorderedConfigs) ColumnConfigs.Add(config);

            // 更新排序顺序
            UpdateSortOrders();

            // 保存配置
            SaveColumnConfig();

            _logger.LogInformation("列顺序已更新并保存");
        }
        finally
        {
            IsUpdatingFromDataGrid = false;
        }

        _logger.LogInformation("=== UpdateColumnOrderFromDataGrid 完成 ===");
    }

    /// <summary>
    ///     更新排序顺序
    /// </summary>
    private void UpdateSortOrders()
    {
        for (var i = 0; i < ColumnConfigs.Count; i++) ColumnConfigs[i].SortOrder = i;
    }
}