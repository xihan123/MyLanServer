namespace MyLanServer.Core.Interfaces;

/// <summary>
///     Web 服务器生命周期管理接口
/// </summary>
public interface IWebServerService
{
    /// <summary>
    ///     Web 服务器是否正在运行
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    ///     启动 Kestrel 服务器
    /// </summary>
    /// <param name="port">监听端口 (例如 8080)</param>
    Task StartServerAsync(int port);

    /// <summary>
    ///     停止 Kestrel 服务器并释放资源
    /// </summary>
    Task StopServerAsync();
}