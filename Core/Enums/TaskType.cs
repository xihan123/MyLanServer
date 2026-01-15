namespace MyLanServer.Core.Enums;

/// <summary>
///     定义任务类型
/// </summary>
public enum TaskType
{
    /// <summary>
    ///     文件收集任务（原有）：用户上传 Excel 文件
    /// </summary>
    FileCollection = 0,

    /// <summary>
    ///     数据收集任务（新增）：用户在线填表，数据以 JSON 形式保存
    /// </summary>
    DataCollection = 1
}