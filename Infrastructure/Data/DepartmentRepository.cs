using System.IO;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Data;

public class DepartmentRepository : IDepartmentRepository
{
    private readonly DapperContext _context;
    private readonly ILogger<DepartmentRepository> _logger;

    public DepartmentRepository(DapperContext context, ILogger<DepartmentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Department>> GetAllAsync()
    {
        try
        {
            using var conn = _context.CreateConnection();
            var sql = "SELECT * FROM Departments ORDER BY SortOrder ASC";
            return await conn.QueryAsync<Department>(sql);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有部门失败");
            throw;
        }
    }

    public async Task<Department?> GetByIdAsync(int id)
    {
        try
        {
            using var conn = _context.CreateConnection();
            var sql = "SELECT * FROM Departments WHERE Id = @Id LIMIT 1";
            return await conn.QueryFirstOrDefaultAsync<Department>(sql, new { Id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取部门失败，ID: {DepartmentId}", id);
            throw;
        }
    }

    public async Task<bool> CreateAsync(Department department)
    {
        try
        {
            using var conn = _context.CreateConnection();

            var sql = @"
                INSERT INTO Departments (Name, SortOrder)
                VALUES (@Name, @SortOrder)";

            var rows = await conn.ExecuteAsync(sql, department);
            if (rows > 0) _logger.LogInformation("创建部门成功: {DepartmentName}", department.Name);

            return rows > 0;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // 唯一约束违反（部门名称重复）
            _logger.LogWarning("部门已存在: {DepartmentName}", department.Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建部门失败: {DepartmentName}", department.Name);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(Department department)
    {
        try
        {
            using var conn = _context.CreateConnection();
            var sql = @"
                UPDATE Departments
                SET Name = @Name,
                    SortOrder = @SortOrder
                WHERE Id = @Id";

            var rows = await conn.ExecuteAsync(sql, department);
            if (rows > 0) _logger.LogInformation("更新部门成功: {DepartmentName}", department.Name);

            return rows > 0;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // 唯一约束违反（部门名称重复）
            _logger.LogWarning("更新部门失败，部门名称已存在: {DepartmentName}", department.Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新部门失败，ID: {DepartmentId}", department.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            using var conn = _context.CreateConnection();
            var sql = "DELETE FROM Departments WHERE Id = @Id";
            var rows = await conn.ExecuteAsync(sql, new { Id = id });
            if (rows > 0) _logger.LogInformation("删除部门成功，ID: {DepartmentId}", id);

            return rows > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除部门失败，ID: {DepartmentId}", id);
            throw;
        }
    }

    public async Task<bool> MoveUpAsync(int id)
    {
        try
        {
            using var conn = _context.CreateConnection();
            using var trans = conn.BeginTransaction();

            try
            {
                // 获取当前部门
                var current = await conn.QueryFirstOrDefaultAsync<Department>(
                    "SELECT * FROM Departments WHERE Id = @Id",
                    new { Id = id },
                    trans);

                if (current == null)
                {
                    trans.Rollback();
                    return false;
                }

                // 获取上一个部门（SortOrder 小于当前的最大值）
                var previous = await conn.QueryFirstOrDefaultAsync<Department>(
                    "SELECT * FROM Departments WHERE SortOrder < @SortOrder ORDER BY SortOrder DESC LIMIT 1",
                    new { current.SortOrder },
                    trans);

                if (previous == null)
                {
                    trans.Rollback();
                    return false; // 已经是第一个，无法上移
                }

                // 交换 SortOrder
                await conn.ExecuteAsync(
                    "UPDATE Departments SET SortOrder = @NewSortOrder WHERE Id = @Id",
                    new { current.Id, NewSortOrder = previous.SortOrder },
                    trans);

                await conn.ExecuteAsync(
                    "UPDATE Departments SET SortOrder = @NewSortOrder WHERE Id = @Id",
                    new { previous.Id, NewSortOrder = current.SortOrder },
                    trans);

                trans.Commit();
                _logger.LogInformation("上移部门成功，ID: {DepartmentId}", id);
                return true;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上移部门失败，ID: {DepartmentId}", id);
            throw;
        }
    }

    public async Task<bool> MoveDownAsync(int id)
    {
        try
        {
            using var conn = _context.CreateConnection();
            using var trans = conn.BeginTransaction();

            try
            {
                // 获取当前部门
                var current = await conn.QueryFirstOrDefaultAsync<Department>(
                    "SELECT * FROM Departments WHERE Id = @Id",
                    new { Id = id },
                    trans);

                if (current == null)
                {
                    trans.Rollback();
                    return false;
                }

                // 获取下一个部门（SortOrder 大于当前的最小值）
                var next = await conn.QueryFirstOrDefaultAsync<Department>(
                    "SELECT * FROM Departments WHERE SortOrder > @SortOrder ORDER BY SortOrder ASC LIMIT 1",
                    new { current.SortOrder },
                    trans);

                if (next == null)
                {
                    trans.Rollback();
                    return false; // 已经是最后一个，无法下移
                }

                // 交换 SortOrder
                await conn.ExecuteAsync(
                    "UPDATE Departments SET SortOrder = @NewSortOrder WHERE Id = @Id",
                    new { current.Id, NewSortOrder = next.SortOrder },
                    trans);

                await conn.ExecuteAsync(
                    "UPDATE Departments SET SortOrder = @NewSortOrder WHERE Id = @Id",
                    new { next.Id, NewSortOrder = current.SortOrder },
                    trans);

                trans.Commit();
                _logger.LogInformation("下移部门成功，ID: {DepartmentId}", id);
                return true;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下移部门失败，ID: {DepartmentId}", id);
            throw;
        }
    }

    public async Task<bool> MoveToTopAsync(int id)
    {
        try
        {
            using var conn = _context.CreateConnection();
            using var trans = conn.BeginTransaction();

            try
            {
                // 获取当前部门
                var current = await conn.QueryFirstOrDefaultAsync<Department>(
                    "SELECT * FROM Departments WHERE Id = @Id",
                    new { Id = id },
                    trans);

                if (current == null)
                {
                    trans.Rollback();
                    return false;
                }

                // 获取第一个部门（SortOrder 最小）
                var first = await conn.QueryFirstOrDefaultAsync<Department>(
                    "SELECT * FROM Departments ORDER BY SortOrder ASC LIMIT 1",
                    transaction: trans);

                if (first == null || first.Id == current.Id)
                {
                    trans.Rollback();
                    return false; // 已经是第一个部门
                }

                // 交换 SortOrder
                await conn.ExecuteAsync(
                    "UPDATE Departments SET SortOrder = @NewSortOrder WHERE Id = @Id",
                    new { current.Id, NewSortOrder = first.SortOrder },
                    trans);

                await conn.ExecuteAsync(
                    "UPDATE Departments SET SortOrder = @NewSortOrder WHERE Id = @Id",
                    new { first.Id, NewSortOrder = current.SortOrder },
                    trans);

                trans.Commit();
                _logger.LogInformation("置顶部门成功，ID: {DepartmentId}", id);
                return true;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "置顶部门失败，ID: {DepartmentId}", id);
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
                // 收集所有有效的部门名称
                var departmentNames = new List<string>();

                // 定义可能的列名集合（按优先级排序）
                var possibleColumnNames = new[] { "Name", "部门名称", "单位", "单位名称", "名称" };

                foreach (var row in rows)
                {
                    // 将 ExpandoObject 转换为字典以访问属性
                    var dict = (IDictionary<string, object>)row;
                    _logger.LogInformation("读取行数据 - 键: {Keys}", string.Join(", ", dict.Keys));

                    // 记录每个键的值和类型
                    // foreach (var key in dict.Keys)
                    // {
                    //     var value = dict[key];
                    //     // _logger.LogInformation("  {Key}: {Value} (Type: {Type})",
                    //     //     key,
                    //     //     value.ToString() ?? "null",
                    //     //     value.GetType().Name);
                    // }

                    // 遍历列名集合查找第一个非空值
                    string? name = null;
                    foreach (var columnName in possibleColumnNames)
                        if (dict.TryGetValue(columnName, out var value) && !(value is DBNull))
                        {
                            name = value.ToString()!;
                            // _logger.LogInformation("从列 {ColumnName} 找到部门名称: {DepartmentName}", columnName, name);
                            break;
                        }

                    if (!string.IsNullOrWhiteSpace(name))
                        departmentNames.Add(name);
                    else
                        _logger.LogWarning("跳过无效行（所有可能的列名都为空或无效）");
                }

                // 去重
                var uniqueNames = departmentNames.Distinct().ToList();

                if (uniqueNames.Count == 0)
                {
                    _logger.LogWarning("Excel 文件中没有有效的部门名称: {FilePath}", filePath);
                    return 0;
                }

                // 获取当前最大的 SortOrder
                var maxSortOrder = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT MAX(SortOrder) FROM Departments",
                    transaction: trans);
                var nextSortOrder = (maxSortOrder ?? 0) + 1;

                // 准备批量插入的数据
                var departmentsToInsert = uniqueNames.Select((name, index) => new
                {
                    Name = name,
                    SortOrder = nextSortOrder + index
                }).ToList();

                // 批量插入（使用 INSERT OR IGNORE 跳过已存在的部门）
                var insertedCount = await conn.ExecuteAsync(
                    "INSERT OR IGNORE INTO Departments (Name, SortOrder) VALUES (@Name, @SortOrder)",
                    departmentsToInsert,
                    trans);

                trans.Commit();
                _logger.LogInformation("成功导入 {Count} 个部门，跳过 {Skipped} 个已存在的部门",
                    insertedCount, uniqueNames.Count - insertedCount);
                return insertedCount;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导入部门失败: {FilePath}", filePath);
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

            // 获取所有部门
            var departments = await GetAllAsync();

            if (!departments.Any())
            {
                _logger.LogWarning("没有部门可导出");
                return false;
            }

            // 转换为匿名对象列表（用于导出）
            var exportData = departments.Select(d => new
            {
                部门名称 = d.Name,
                排序 = d.SortOrder
            }).ToList();

            // 导出到 Excel
            await MiniExcel.SaveAsAsync(filePath, exportData);

            _logger.LogInformation("成功导出 {Count} 个部门到: {FilePath}", departments.Count(), filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出部门失败: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> ClearAllAsync()
    {
        try
        {
            using var conn = _context.CreateConnection();
            var sql = "DELETE FROM Departments";
            var rows = await conn.ExecuteAsync(sql);
            if (rows > 0) _logger.LogInformation("清空所有部门成功，共删除 {Count} 个部门", rows);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空所有部门失败");
            throw;
        }
    }
}