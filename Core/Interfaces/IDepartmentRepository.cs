using MyLanServer.Core.Models;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     部门仓储接口：负责部门的持久化操作
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>
    ///     获取所有部门（按 SortOrder 排序）
    /// </summary>
    Task<IEnumerable<Department>> GetAllAsync();

    /// <summary>
    ///     根据 ID 获取单个部门
    /// </summary>
    Task<Department?> GetByIdAsync(int id);

    /// <summary>
    ///     创建新部门
    /// </summary>
    Task<bool> CreateAsync(Department department);

    /// <summary>
    ///     更新部门信息
    /// </summary>
    Task<bool> UpdateAsync(Department department);

    /// <summary>
    ///     删除部门（硬删除，不影响历史提交记录）
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    ///     上移部门（交换 SortOrder）
    /// </summary>
    Task<bool> MoveUpAsync(int id);

    /// <summary>
    ///     下移部门（交换 SortOrder）
    /// </summary>
    Task<bool> MoveDownAsync(int id);

    /// <summary>
    ///     置顶部门（将部门移动到列表最顶部）
    /// </summary>
    Task<bool> MoveToTopAsync(int id);

    /// <summary>
    ///     从 Excel 文件导入部门列表
    /// </summary>
    Task<int> ImportFromExcelAsync(string filePath);

    /// <summary>
    ///     导出部门列表到 Excel 文件
    /// </summary>
    Task<bool> ExportToExcelAsync(string filePath);

    /// <summary>
    ///     清空所有部门
    /// </summary>
    Task<bool> ClearAllAsync();
}