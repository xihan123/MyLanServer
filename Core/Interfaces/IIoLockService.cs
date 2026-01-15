namespace MyLanServer.Core.Interfaces;

/// <summary>
///     全局 IO 锁服务接口
///     用于协调所有文件 IO 操作，防止并发冲突
/// </summary>
public interface IIoLockService
{
    /// <summary>
    ///     获取 IO 锁
    /// </summary>
    /// <returns>可释放的锁对象</returns>
    Task<IDisposable> AcquireLockAsync();
}