using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     身份证号验证服务实现
///     支持 15 位和 18 位身份证号的验证和信息提取
/// </summary>
public class IdCardValidationService : IIdCardValidationService
{
    // 18位身份证校验码权重
    private static readonly int[] IdCardWeights = { 7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2 };

    // 18位身份证校验码对应表
    private static readonly char[] IdCardCheckCodes = { '1', '0', 'X', '9', '8', '7', '6', '5', '4', '3', '2' };
    private readonly ILogger<IdCardValidationService> _logger;

    public IdCardValidationService(ILogger<IdCardValidationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     验证身份证号是否合法
    /// </summary>
    public bool IsValidIdCard(string idCard)
    {
        return GetValidationError(idCard) == null;
    }

    /// <summary>
    ///     获取验证错误信息
    /// </summary>
    public string? GetValidationError(string idCard)
    {
        if (string.IsNullOrWhiteSpace(idCard))
            return "身份证号不能为空";

        idCard = idCard.ToUpper().Trim();

        // 验证长度
        if (idCard.Length != 15 && idCard.Length != 18)
            return "身份证号长度不正确，应为15位或18位";

        // 验证数字格式（18位最后一位可以是 X）
        if (!Regex.IsMatch(idCard, idCard.Length == 15 ? @"^\d{15}$" : @"^\d{17}[\dX]$"))
            return "身份证号格式不正确";

        // 验证校验码（18位）
        if (idCard.Length == 18)
        {
            var checkCode = CalculateCheckCode(idCard.Substring(0, 17));
            if (checkCode != idCard[17])
                return "身份证号校验码不正确";
        }

        // 验证出生日期
        var birthDate = ParseBirthDate(idCard);
        if (birthDate == null)
            return "身份证号出生日期不正确";

        if (birthDate.Value > DateTime.Today)
            return "身份证号出生日期不能大于当前日期";

        // 验证出生日期是否合理（不能早于1900年）
        if (birthDate.Value.Year < 1900)
            return "身份证号出生日期不能早于1900年";

        return null;
    }

    /// <summary>
    ///     从身份证号提取出生日期
    /// </summary>
    public DateTime? GetBirthDate(string idCard)
    {
        if (!IsValidIdCard(idCard))
            return null;

        return ParseBirthDate(idCard);
    }

    /// <summary>
    ///     从身份证号计算年龄
    /// </summary>
    public int? GetAge(string idCard)
    {
        var birthDate = GetBirthDate(idCard);
        if (!birthDate.HasValue)
            return null;

        return CalculateAge(birthDate.Value);
    }

    /// <summary>
    ///     从身份证号获取性别
    /// </summary>
    public string? GetGender(string idCard)
    {
        if (!IsValidIdCard(idCard))
            return null;

        return ParseGender(idCard);
    }

    /// <summary>
    ///     计算校验码
    /// </summary>
    private static char CalculateCheckCode(string idCard17)
    {
        var sum = 0;
        for (var i = 0; i < 17; i++) sum += (idCard17[i] - '0') * IdCardWeights[i];

        return IdCardCheckCodes[sum % 11];
    }

    /// <summary>
    ///     解析出生日期
    /// </summary>
    private static DateTime? ParseBirthDate(string idCard)
    {
        try
        {
            if (idCard.Length == 15)
            {
                var year = int.Parse("19" + idCard.Substring(6, 2));
                var month = int.Parse(idCard.Substring(8, 2));
                var day = int.Parse(idCard.Substring(10, 2));
                return new DateTime(year, month, day);
            }
            else
            {
                var year = int.Parse(idCard.Substring(6, 4));
                var month = int.Parse(idCard.Substring(10, 2));
                var day = int.Parse(idCard.Substring(12, 2));
                return new DateTime(year, month, day);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     计算年龄
    /// </summary>
    private static int CalculateAge(DateTime birthDate)
    {
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;

        // 如果今年还没过生日，年龄减1
        if (today < birthDate.AddYears(age))
            age--;

        return age;
    }

    /// <summary>
    ///     解析性别
    /// </summary>
    private static string ParseGender(string idCard)
    {
        var genderCode = idCard.Length == 15
            ? idCard.Substring(14, 1)
            : idCard.Substring(16, 1);

        return int.Parse(genderCode) % 2 == 1 ? "男" : "女";
    }
}