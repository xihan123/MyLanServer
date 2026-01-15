using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     快捷过期时间选项服务实现
/// </summary>
public class ExpiryQuickOptionsService : IExpiryQuickOptionsService, IDisposable
{
    private readonly string _configFilePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<ExpiryQuickOptionsService> _logger;
    private ExpiryQuickOptionsConfig? _currentConfig;
    private FileSystemWatcher? _watcher;

    public ExpiryQuickOptionsService(ILogger<ExpiryQuickOptionsService> logger)
    {
        _logger = logger;
        _configFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config",
            "expiry_quick_options.json");
    }

    /// <summary>
    ///     释放资源
    /// </summary>
    public void Dispose()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnConfigFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        _lock?.Dispose();
    }

    public event EventHandler? OptionsChanged;

    /// <summary>
    ///     加载配置
    /// </summary>
    public async Task<ExpiryQuickOptionsConfig> LoadConfigAsync()
    {
        await _lock.WaitAsync();
        try
        {
            _logger.LogInformation("ExpiryQuickOptionsService - 准备加载快捷时间选项配置");
            _logger.LogInformation("  文件路径: {Path}", _configFilePath);

            // 检查文件是否存在
            if (!File.Exists(_configFilePath))
            {
                _logger.LogInformation("  配置文件不存在，生成默认配置");
                var defaultConfig = CreateDefaultConfig();
                await SaveConfigAsync(defaultConfig);
                _currentConfig = defaultConfig;
            }
            else
            {
                _logger.LogInformation("  配置文件存在，读取内容");
                var json = await File.ReadAllTextAsync(_configFilePath);
                _logger.LogInformation("  JSON 内容长度: {Length}", json.Length);

                var config = JsonSerializer.Deserialize<ExpiryQuickOptionsConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null)
                {
                    _logger.LogInformation("  反序列化成功");
                    _logger.LogInformation("  加载到的时间选项数量: {Count}", config.Options.Count);
                    _currentConfig = config;
                }
                else
                {
                    _logger.LogWarning("  反序列化失败，使用默认配置");
                    _currentConfig = CreateDefaultConfig();
                }
            }

            // 启动文件监视器（仅第一次）
            if (_watcher == null) StartFileWatcher();

            _logger.LogInformation("ExpiryQuickOptionsService - 快捷时间选项配置加载成功");
            return _currentConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExpiryQuickOptionsService - 加载快捷时间选项配置失败");
            _logger.LogError("  文件路径: {Path}", _configFilePath);
            _logger.LogError("  错误信息: {Message}", ex.Message);

            // 发生异常时返回默认配置
            _currentConfig = CreateDefaultConfig();
            return _currentConfig;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    ///     获取当前时间选项列表
    /// </summary>
    public List<ExpiryQuickOption> GetOptions()
    {
        if (_currentConfig == null)
        {
            _logger.LogWarning("ExpiryQuickOptionsService - 配置未加载，返回默认选项");
            return CreateDefaultConfig().Options;
        }

        return _currentConfig.Options;
    }

    /// <summary>
    ///     配置文件变更事件处理器
    /// </summary>
    private async void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        // 防止重复触发
        await Task.Delay(500);

        _logger.LogInformation("ExpiryQuickOptionsService - 检测到配置文件变化，重新加载");
        await LoadConfigAsync();
        OptionsChanged?.Invoke(this, EventArgs.Empty);
        _logger.LogInformation("ExpiryQuickOptionsService - 配置已更新，已触发 OptionsChanged 事件");
    }

    /// <summary>
    ///     保存配置
    /// </summary>
    private async Task SaveConfigAsync(ExpiryQuickOptionsConfig config)
    {
        try
        {
            var configDir = Path.GetDirectoryName(_configFilePath);
            _logger.LogInformation("  配置目录: {Directory}", configDir);

            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                _logger.LogInformation("  创建配置目录");
                Directory.CreateDirectory(configDir);
            }

            config.LastUpdated = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            _logger.LogInformation("  JSON 内容长度: {Length}", json.Length);
            await File.WriteAllTextAsync(_configFilePath, json);

            _logger.LogInformation("ExpiryQuickOptionsService - 快捷时间选项配置保存成功: {Path}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExpiryQuickOptionsService - 保存快捷时间选项配置失败");
            _logger.LogError("  文件路径: {Path}", _configFilePath);
            _logger.LogError("  错误信息: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    ///     创建默认配置
    /// </summary>
    private ExpiryQuickOptionsConfig CreateDefaultConfig()
    {
        _logger.LogInformation("ExpiryQuickOptionsService - 创建默认配置");

        var config = new ExpiryQuickOptionsConfig
        {
            Options = new List<ExpiryQuickOption>
            {
                new() { DisplayName = "1小时", Hours = 1 },
                new() { DisplayName = "12小时", Hours = 12 },
                new() { DisplayName = "1天", Days = 1 },
                new() { DisplayName = "3天", Days = 3 },
                new() { DisplayName = "7天", Days = 7 },
                new() { DisplayName = "15天", Days = 15 },
                new() { DisplayName = "30天", Days = 30 },
                new() { DisplayName = "60天", Days = 60 },
                new() { DisplayName = "90天", Days = 90 }
            },
            LastUpdated = DateTime.UtcNow
        };

        _logger.LogInformation("  创建了 {Count} 个默认时间选项", config.Options.Count);
        return config;
    }

    /// <summary>
    ///     启动文件监视器（实现热更新）
    /// </summary>
    private void StartFileWatcher()
    {
        try
        {
            var configDir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir)) Directory.CreateDirectory(configDir);

            if (string.IsNullOrEmpty(configDir))
            {
                _logger.LogWarning("ExpiryQuickOptionsService - 配置目录路径为空，无法启动文件监视器");
                return;
            }

            _watcher = new FileSystemWatcher(configDir)
            {
                Filter = Path.GetFileName(_configFilePath),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };

            _watcher.Changed += OnConfigFileChanged;

            _watcher.EnableRaisingEvents = true;
            _logger.LogInformation("ExpiryQuickOptionsService - 文件监视器已启动: {Path}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExpiryQuickOptionsService - 启动文件监视器失败");
        }
    }
}