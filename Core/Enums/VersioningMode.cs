namespace MyLanServer.Core.Enums;

/// <summary>
///     定义文件重名时的处理策略
/// </summary>
public enum VersioningMode
{
    /// <summary>
    ///     覆盖模式：如果有同名文件，直接覆盖旧文件
    /// </summary>
    Overwrite = 0,

    /// <summary>
    ///     自动版本号：如果有同名文件，自动添加后缀 (e.g. filename_v1.xlsx)
    /// </summary>
    AutoVersion = 1
}