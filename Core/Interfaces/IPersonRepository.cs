using MyLanServer.Core.Models;

namespace MyLanServer.Core.Interfaces;

/// <summary>
///     人员仓储接口：负责人员的持久化操作
/// </summary>
public interface IPersonRepository
{
    /// <summary>
    ///     获取所有人员列表
    /// </summary>
    Task<IEnumerable<Person>> GetAllAsync();

    /// <summary>
    ///     根据 ID 获取单个人员
    /// </summary>
    Task<Person?> GetByIdAsync(int id);

    /// <summary>
    ///     根据身份证号获取人员
    /// </summary>
    Task<Person?> GetByIdCardAsync(string idCard);

    /// <summary>
    ///     创建新人员
    /// </summary>
    Task<bool> CreateAsync(Person person);

    /// <summary>
    ///     更新人员信息
    /// </summary>
    Task<bool> UpdateAsync(Person person);

    /// <summary>
    ///     删除人员
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    ///     模糊搜索人员
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="limit">返回结果数量限制</param>
    /// <param name="fields">指定搜索字段（逗号分隔，如：name,contact,employeeNumber），为null时默认搜索姓名、身份证号、联系方式、工号</param>
    Task<IEnumerable<Person>> SearchAsync(string keyword, int limit = 10, string? fields = null);

    /// <summary>
    ///     从 Excel 文件导入人员列表
    /// </summary>
    Task<int> ImportFromExcelAsync(string filePath);

    /// <summary>
    ///     导出人员列表到 Excel 文件
    /// </summary>
    Task<bool> ExportToExcelAsync(string filePath);

    /// <summary>
    ///     清空所有人员
    /// </summary>
    Task<bool> ClearAllAsync();
}