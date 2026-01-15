using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     全局 IO 锁服务实现
///     使用单一静态锁协调所有文件 IO 操作
/// </summary>
public class IoLockService : IIoLockService
{
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<IoLockService> _logger;

    public IoLockService(ILogger<IoLockService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     获取 IO 锁
    /// </summary>
    /// <returns>可释放的锁对象</returns>
    public async Task<IDisposable> AcquireLockAsync()
    {
        _logger.LogDebug("Waiting for IO lock...");
        await _lock.WaitAsync();
        _logger.LogDebug("IO lock acquired");
        return new LockReleaser(_lock, _logger);
    }

    /// <summary>
    ///     锁释放器
    /// </summary>
    private class LockReleaser : IDisposable
    {
        private readonly SemaphoreSlim _lock;
        private readonly ILogger _logger;
        private bool _disposed;

        public LockReleaser(SemaphoreSlim semaphore, ILogger logger)
        {
            _lock = semaphore;
            _logger = logger;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _lock.Release();
                _logger.LogDebug("IO lock released");
                _disposed = true;
            }
        }
    }
}