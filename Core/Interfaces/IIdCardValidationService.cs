namespace MyLanServer.Core.Interfaces;

/// <summary>
///     身份证号验证服务接口
/// </summary>
public interface IIdCardValidationService
{
    /// <summary>
    ///     验证身份证号是否合法
    /// </summary>
    /// <param name="idCard">身份证号</param>
    /// <returns>是否合法</returns>
    bool IsValidIdCard(string idCard);

    /// <summary>
    ///     从身份证号提取出生日期
    /// </summary>
    /// <param name="idCard">身份证号</param>
    /// <returns>出生日期，如果身份证号无效则返回 null</returns>
    DateTime? GetBirthDate(string idCard);

    /// <summary>
    ///     从身份证号计算年龄
    /// </summary>
    /// <param name="idCard">身份证号</param>
    /// <returns>年龄，如果身份证号无效则返回 null</returns>
    int? GetAge(string idCard);

    /// <summary>
    ///     从身份证号获取性别
    /// </summary>
    /// <param name="idCard">身份证号</param>
    /// <returns>性别（"男"或"女"），如果身份证号无效则返回 null</returns>
    string? GetGender(string idCard);

    /// <summary>
    ///     获取验证错误信息
    /// </summary>
    /// <param name="idCard">身份证号</param>
    /// <returns>错误信息，如果身份证号有效则返回 null</returns>
    string? GetValidationError(string idCard);
}