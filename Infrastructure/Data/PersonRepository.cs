using System.IO;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Data;

public class PersonRepository : IPersonRepository
{
    private readonly DapperContext _context;
    private readonly IIdCardValidationService _idCardValidationService;
    private readonly ILogger<PersonRepository> _logger;

    public PersonRepository(DapperContext context, ILogger<PersonRepository> logger,
        IIdCardValidationService idCardValidationService)
    {
        _context = context;
        _logger = logger;
        _idCardValidationService = idCardValidationService;
    }

    public async Task<IEnumerable<Person>> GetAllAsync()
    {
        try
        {
            using var conn = _context.CreateConnection();
            var sql = "SELECT * FROM Persons ORDER BY CreatedAt DESC";
            return await conn.QueryAsync<Person>(sql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有人员失败");
            throw;
        }
    }

    public async Task<Person?> GetByIdAsync(int id)
    {
        try
        {
            using var conn = _context.CreateConnection();
            var sql = "SELECT * FROM Persons WHERE Id = @Id LIMIT 1";
            return await conn.QueryFirstOrDefaultAsync<Person>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取人员失败，ID: {PersonId}", id);
            throw;
        }
    }

    public async Task<Person?> GetByIdCardAsync(string idCard)
    {
        try
        {
            using var conn = _context.CreateConnection();
            var sql = "SELECT * FROM Persons WHERE IdCard = @IdCard LIMIT 1";
            return await conn.QueryFirstOrDefaultAsync<Person>(sql, new { IdCard = idCard });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "根据身份证号获取人员失败: {IdCard}", idCard);
            throw;
        }
    }

    public async Task<bool> CreateAsync(Person person)
    {
        try
        {
            using var conn = _context.CreateConnection();

            var sql = @"
                INSERT INTO Persons (Name, IdCard, Contact1, Contact2, Department, Age, Gender, BirthDate,
                                    RegisteredAddress, CurrentAddress, EmployeeNumber, Rank1, Rank2, Position, WorkStartDate, CreatedAt, UpdatedAt)
                VALUES (@Name, @IdCard, @Contact1, @Contact2, @Department, @Age, @Gender, @BirthDate,
                        @RegisteredAddress, @CurrentAddress, @EmployeeNumber, @Rank1, @Rank2, @Position, @WorkStartDate, @CreatedAt, @UpdatedAt)";

            person.CreatedAt = DateTime.UtcNow;
            person.UpdatedAt = DateTime.UtcNow;

            var rows = await conn.ExecuteAsync(sql, person);
            if (rows > 0) _logger.LogInformation("创建人员成功: {Name}, {IdCard}", person.Name, person.IdCard);

            return rows > 0;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // 唯一约束违反（身份证号重复）
            _logger.LogWarning("人员已存在: {IdCard}", person.IdCard);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建人员失败: {Name}", person.Name);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(Person person)
    {
        try
        {
            using var conn = _context.CreateConnection();
            person.UpdatedAt = DateTime.UtcNow;

            var sql = @"
                UPDATE Persons
                SET Name = @Name,
                    IdCard = @IdCard,
                    Contact1 = @Contact1,
                    Contact2 = @Contact2,
                    Department = @Department,
                    Age = @Age,
                    Gender = @Gender,
                    BirthDate = @BirthDate,
                    RegisteredAddress = @RegisteredAddress,
                    CurrentAddress = @CurrentAddress,
                    EmployeeNumber = @EmployeeNumber,
                    Rank1 = @Rank1,
                    Rank2 = @Rank2,
                    Position = @Position,
                    WorkStartDate = @WorkStartDate,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            var rows = await conn.ExecuteAsync(sql, person);
            if (rows > 0) _logger.LogInformation("更新人员成功: {Name}, {IdCard}", person.Name, person.IdCard);

            return rows > 0;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // 唯一约束违反（身份证号重复）
            _logger.LogWarning("更新人员失败，身份证号已存在: {IdCard}", person.IdCard);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新人员失败，ID: {PersonId}", person.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            using var conn = _context.CreateConnection();
            var sql = "DELETE FROM Persons WHERE Id = @Id";
            var rows = await conn.ExecuteAsync(sql, new { Id = id });
            if (rows > 0) _logger.LogInformation("删除人员成功，ID: {PersonId}", id);

            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除人员失败，ID: {PersonId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<Person>> SearchAsync(string keyword, int limit = 10, string? fields = null)
    {
        try
        {
            using var conn = _context.CreateConnection();

            // 定义可搜索字段白名单（防止SQL注入）
            var allowedFields = new Dictionary<string, string>
            {
                { "name", "Name" },
                { "idcard", "IdCard" },
                { "contact1", "Contact1" },
                { "contact2", "Contact2" },
                { "employeenumber", "EmployeeNumber" },
                { "department", "Department" },
                { "rank1", "Rank1" },
                { "rank2", "Rank2" },
                { "position", "Position" },
                { "registeredaddress", "RegisteredAddress" },
                { "currentaddress", "CurrentAddress" }
            };

            // 解析字段列表
            var fieldList = new List<string>();
            if (string.IsNullOrWhiteSpace(fields))
            {
                // 默认搜索字段：姓名、身份证号、联系方式、工号
                fieldList.AddRange(new[] { "Name", "IdCard", "Contact1", "Contact2", "EmployeeNumber" });
            }
            else
            {
                var inputFields = fields.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var field in inputFields)
                {
                    var lowerField = field.ToLowerInvariant();
                    if (allowedFields.TryGetValue(lowerField, out var dbField)) fieldList.Add(dbField);
                }

                // 如果没有有效字段，使用默认字段
                if (fieldList.Count == 0) fieldList.AddRange(new[] { "Name", "IdCard", "Contact", "EmployeeNumber" });
            }

            // 动态构建SQL WHERE条件
            var whereConditions = fieldList.Select(f => $"{f} LIKE @Keyword");
            var sql = $@"
                SELECT * FROM Persons
                WHERE {string.Join(" OR ", whereConditions)}
                ORDER BY CreatedAt DESC
                LIMIT @Limit";

            var searchPattern = $"%{keyword}%";
            return await conn.QueryAsync<Person>(sql, new { Keyword = searchPattern, Limit = limit });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索人员失败: {Keyword}", keyword);
            throw;
        }
    }

    public async Task<int> ImportFromExcelAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("Excel 文件不存在", filePath);

            // 读取 Excel 文件（使用第一行作为表头）
            var rows = MiniExcel.Query(filePath, true)?.ToList();
            _logger.LogInformation("读取 Excel 文件: {FilePath}", filePath);
            _logger.LogInformation("读取到 {RowCount} 行数据", rows?.Count ?? 0);

            if (rows == null || rows.Count == 0)
            {
                _logger.LogWarning("Excel 文件为空: {FilePath}", filePath);
                return 0;
            }

            using var conn = _context.CreateConnection();
            using var trans = conn.BeginTransaction();

            try
            {
                var importedCount = 0;
                var skippedCount = 0;

                foreach (var row in rows)
                {
                    var dict = (IDictionary<string, object>)row;

                    // 提取姓名
                    var name = GetExcelValue(dict,
                        new[] { "姓名", "名字", "Name", "name", "NAME", "职工姓名", "员工姓名", "人员姓名" });
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        _logger.LogWarning("跳过无效行（姓名为空）");
                        skippedCount++;
                        continue;
                    }

                    // 提取身份证号
                    var idCard = GetExcelValue(dict,
                        new[] { "身份证号", "身份证", "IdCard", "idCard", "IDCard", "身份证号码", "证件号", "ID", "id" });
                    if (string.IsNullOrWhiteSpace(idCard))
                    {
                        _logger.LogWarning("跳过无效行（身份证号为空）: {Name}", name);
                        skippedCount++;
                        continue;
                    }

                    // 提取其他字段
                    var contact1 = GetExcelValue(dict, new[]
                    {
                        "联系方式1", "联系方式一", "Contact1", "contact1", "电话1", "手机1", "联系电话1", "手机号1",
                        "联系方式", "电话", "手机", "Phone", "phone", "Telephone", "telephone", "Tel", "tel",
                        "Mobile", "mobile", "联系电话", "手机号", "Contact", "contact"
                    });
                    var contact2 = GetExcelValue(dict, new[]
                    {
                        "联系方式2", "联系方式二", "Contact2", "contact2", "电话2", "手机2", "联系电话2", "手机号2"
                    });
                    var department = GetExcelValue(dict, new[]
                    {
                        "所属部门", "部门", "Department", "department", "Dept", "dept", "单位", "科室",
                        "部门名称", "所在部门"
                    });
                    var registeredAddress = GetExcelValue(dict, new[]
                    {
                        "户籍地址", "RegisteredAddress", "registeredAddress"
                    });
                    var currentAddress = GetExcelValue(dict, new[]
                    {
                        "现住址", "居住地址", "当前住址", "CurrentAddress", "currentAddress", "Address",
                        "address", "家庭住址", "住址", "详细地址", "现居住地"
                    });
                    var employeeNumber = GetExcelValue(dict, new[]
                    {
                        "工号", "EmployeeNumber", "employeeNumber", "EmployeeNo", "employeeNo", "EmpNo",
                        "empNo", "员工编号", "编号", "No", "no", "职工号"
                    });
                    var rank1 = GetExcelValue(dict, new[]
                    {
                        "职级1", "职级一", "Rank1", "rank1", "职级", "Rank", "rank", "职级等级", "Level", "level"
                    });
                    var rank2 = GetExcelValue(dict, new[]
                    {
                        "职级2", "职级二", "Rank2", "rank2"
                    });
                    var position = GetExcelValue(dict, new[]
                    {
                        "职务", "岗位", "Position", "position", "职位", "行政职务", "技术职务", "职称"
                    });

                    // 提取年龄、性别、出生日期（如果Excel中有这些列）
                    var ageStr = GetExcelValue(dict, new[] { "年龄", "Age", "age", "岁" });
                    var gender = GetExcelValue(dict, new[] { "性别", "Gender", "gender", "男女", "男女性别" });
                    var birthDateStr = GetExcelValue(dict, new[]
                    {
                        "出生日期", "Birthday", "birthday", "出生", "生日", "BirthDate", "birthDate", "出生时间"
                    });

                    // 提取参与工作时间
                    var workStartDateStr = GetExcelValue(dict, new[]
                    {
                        "参与工作时间", "工作时间", "WorkStartDate", "workStartDate", "入职时间", "参加工作时间",
                        "工作日期", "StartDate", "startDate"
                    });

                    // 解析Excel中的年龄、出生日期和工作时间
                    int? age = null;
                    DateTime? birthDate = null;
                    DateTime? workStartDate = null;
                    if (!string.IsNullOrWhiteSpace(ageStr) && int.TryParse(ageStr, out var parsedAge))
                        age = parsedAge;
                    if (!string.IsNullOrWhiteSpace(birthDateStr) &&
                        DateTime.TryParse(birthDateStr, out var parsedDate))
                        birthDate = parsedDate;
                    if (!string.IsNullOrWhiteSpace(workStartDateStr) &&
                        DateTime.TryParse(workStartDateStr, out var parsedWorkStartDate))
                        workStartDate = parsedWorkStartDate;

                    // 如果Excel中没有这些字段，从身份证号自动计算
                    if (age == null || birthDate == null || string.IsNullOrWhiteSpace(gender))
                    {
                        if (_idCardValidationService.IsValidIdCard(idCard))
                        {
                            age ??= _idCardValidationService.GetAge(idCard);
                            gender ??= _idCardValidationService.GetGender(idCard);
                            birthDate ??= _idCardValidationService.GetBirthDate(idCard);
                        }
                    }

                    // 检查身份证号是否已存在
                    var existingPerson = await conn.QueryFirstOrDefaultAsync<Person>(
                        "SELECT * FROM Persons WHERE IdCard = @IdCard",
                        new { IdCard = idCard },
                        trans);

                    if (existingPerson != null)
                    {
                        _logger.LogInformation("跳过已存在的人员: {Name}, {IdCard}", name, idCard);
                        skippedCount++;
                        continue;
                    }

                    // 创建人员对象
                    var person = new Person
                    {
                        Name = name.Trim(),
                        IdCard = idCard.Trim(),
                        Contact1 = contact1?.Trim(),
                        Contact2 = contact2?.Trim(),
                        Department = department?.Trim(),
                        RegisteredAddress = registeredAddress?.Trim(),
                        CurrentAddress = currentAddress?.Trim(),
                        EmployeeNumber = employeeNumber?.Trim(),
                        Rank1 = rank1?.Trim(),
                        Rank2 = rank2?.Trim(),
                        Position = position?.Trim(),
                        Age = age,
                        Gender = gender?.Trim(),
                        BirthDate = birthDate,
                        WorkStartDate = workStartDate,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // 插入数据库
                    var insertedRows = await conn.ExecuteAsync(@"
                        INSERT INTO Persons (Name, IdCard, Contact1, Contact2, Department, Age, Gender, BirthDate,
                                            RegisteredAddress, CurrentAddress, EmployeeNumber, Rank1, Rank2, Position, WorkStartDate, CreatedAt, UpdatedAt)
                        VALUES (@Name, @IdCard, @Contact1, @Contact2, @Department, @Age, @Gender, @BirthDate,
                                @RegisteredAddress, @CurrentAddress, @EmployeeNumber, @Rank1, @Rank2, @Position, @WorkStartDate, @CreatedAt, @UpdatedAt)",
                        person, trans);

                    if (insertedRows > 0) importedCount++;
                }

                trans.Commit();
                _logger.LogInformation("成功导入 {Count} 个人员，跳过 {Skipped} 个已存在或无效的人员",
                    importedCount, skippedCount);
                return importedCount;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入人员失败: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> ExportToExcelAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("FilePath 不能为空", nameof(filePath));

        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            // 获取所有人员
            var persons = await GetAllAsync();

            if (!persons.Any())
            {
                _logger.LogWarning("没有人员可导出");
                return false;
            }

            // 转换为匿名对象列表（用于导出）
            var exportData = persons.Select(p => new
            {
                姓名 = p.Name,
                身份证号 = p.IdCard,
                联系方式1 = p.Contact1,
                联系方式2 = p.Contact2,
                所属部门 = p.Department,
                年龄 = p.Age,
                性别 = p.Gender,
                出生日期 = p.BirthDate?.ToString("yyyy-MM-dd"),
                户籍地址 = p.RegisteredAddress,
                现住址 = p.CurrentAddress,
                工号 = p.EmployeeNumber,
                职级1 = p.Rank1,
                职级2 = p.Rank2,
                职务 = p.Position,
                参与工作时间 = p.WorkStartDate?.ToString("yyyy-MM-dd"),
                创建时间 = p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                更新时间 = p.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToList();

            // 导出到 Excel
            await MiniExcel.SaveAsAsync(filePath, exportData);

            _logger.LogInformation("成功导出 {Count} 个人员到: {FilePath}", persons.Count(), filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出人员失败: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> ClearAllAsync()
    {
        try
        {
            using var conn = _context.CreateConnection();
            var sql = "DELETE FROM Persons";
            var rows = await conn.ExecuteAsync(sql);
            if (rows > 0) _logger.LogInformation("清空所有人员成功，共删除 {Count} 个人员", rows);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空所有人员失败");
            throw;
        }
    }

    /// <summary>
    ///     从 Excel 行中获取值（支持多种列名）
    /// </summary>
    private static string? GetExcelValue(IDictionary<string, object> dict, string[] possibleColumnNames)
    {
        foreach (var columnName in possibleColumnNames)
            if (dict.TryGetValue(columnName, out var value) && value != null && !(value is DBNull))
                return value.ToString();

        return null;
    }
}