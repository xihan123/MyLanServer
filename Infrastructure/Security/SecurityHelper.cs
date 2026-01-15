using System.IO;
using System.Text.RegularExpressions;

namespace MyLanServer.Infrastructure.Security;

/// <summary>
///     安全辅助工具类
/// </summary>
public static class SecurityHelper
{
    /// <summary>
    ///     危险的路径遍历字符
    /// </summary>
    private static readonly string[] PathTraversalChars = { "..", "./", ".\\", "~", "%", "|" };

    /// <summary>
    ///     危险的HTML/脚本字符
    /// </summary>
    private static readonly char[] DangerousChars =
        { '<', '>', '"', '\'', '&', '%', '{', '}', '[', ']', '~', '`', '\\', '/' };

    /// <summary>
    ///     清理路径段，防止路径遍历攻击
    /// </summary>
    /// <param name="segment">路径段</param>
    /// <returns>清理后的安全路径段</returns>
    public static string SanitizePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return "Unknown";

        // 移除路径遍历字符
        var sanitized = segment;
        foreach (var dangerousChar in PathTraversalChars) sanitized = sanitized.Replace(dangerousChar, "");

        // 只允许安全字符：中文、字母、数字、下划线、横杠、点号
        sanitized = Regex.Replace(sanitized, @"[^\u4e00-\u9fa5a-zA-Z0-9\-_.]", "_");

        // 限制长度
        if (sanitized.Length > 100)
            sanitized = sanitized.Substring(0, 100);

        return sanitized;
    }

    /// <summary>
    ///     验证输入是否安全
    /// </summary>
    /// <param name="input">输入字符串</param>
    /// <param name="minLength">最小长度</param>
    /// <param name="maxLength">最大长度</param>
    /// <returns>是否安全</returns>
    public static bool IsValidInput(string input, int minLength = 1, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (input.Length < minLength || input.Length > maxLength)
            return false;

        // 检查是否包含潜在危险字符
        return !input.Any(c => DangerousChars.Contains(c));
    }

    /// <summary>
    ///     验证文件名是否安全
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <returns>是否安全</returns>
    public static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        // 检查路径遍历
        if (fileName.Contains("..") || fileName.Contains("./") || fileName.Contains(".\\"))
            return false;

        // 检查危险字符
        if (fileName.Any(c => DangerousChars.Contains(c)))
            return false;

        // 检查长度
        if (fileName.Length > 255)
            return false;

        return true;
    }

    /// <summary>
    ///     验证联系方式（手机号）
    /// </summary>
    /// <param name="contact">联系方式</param>
    /// <returns>是否有效</returns>
    public static bool IsValidContact(string contact)
    {
        if (string.IsNullOrWhiteSpace(contact))
            return false;

        // 移除所有非数字字符
        var digitsOnly = Regex.Replace(contact, @"[^\d]", "");

        // 验证长度（4-11位）
        if (digitsOnly.Length < 4 || digitsOnly.Length > 11)
            return false;

        // 验证是否只包含数字和可能的分隔符
        return contact.Length == digitsOnly.Length ||
               Regex.IsMatch(contact, @"^[\d\s\-]+$");
    }

    /// <summary>
    ///     验证部门名称
    /// </summary>
    /// <param name="department">部门名称</param>
    /// <returns>是否有效</returns>
    public static bool IsValidDepartment(string department)
    {
        if (string.IsNullOrWhiteSpace(department))
            return false;

        // 检查长度
        if (department.Length < 1 || department.Length > 50)
            return false;

        // 检查危险字符
        return !department.Any(c => DangerousChars.Contains(c));
    }

    /// <summary>
    ///     验证姓名
    /// </summary>
    /// <param name="name">姓名</param>
    /// <returns>是否有效</returns>
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // 检查长度
        if (name.Length < 1 || name.Length > 50)
            return false;

        // 检查危险字符
        return !name.Any(c => DangerousChars.Contains(c));
    }

    /// <summary>
    ///     获取安全的文件名
    /// </summary>
    /// <param name="originalFileName">原始文件名</param>
    /// <returns>安全的文件名</returns>
    public static string GetSafeFileName(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
            return "unknown";

        // 使用Path.GetFileName移除路径信息
        var fileName = Path.GetFileName(originalFileName);

        // 清理文件名
        var sanitized = SanitizePathSegment(fileName);

        // 确保文件名不为空
        if (string.IsNullOrWhiteSpace(sanitized))
            return "unknown";

        return sanitized;
    }
}