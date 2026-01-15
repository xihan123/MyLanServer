namespace MyLanServer.Core.Enums;

/// <summary>
///     列合并模式（用于在线表单统计）
/// </summary>
public enum MergeMode
{
    /// <summary>
    ///     累计模式：简单统计所有值的数量或总和
    ///     适用于：数字、双选框、文本
    /// </summary>
    Accumulate = 0,

    /// <summary>
    ///     分组统计模式：按指定字段分组统计
    ///     例如：按部门分组统计，显示"技术部：是【5】，否【10】"
    /// </summary>
    GroupBy = 1
}