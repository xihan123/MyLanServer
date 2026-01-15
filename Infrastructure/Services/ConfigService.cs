using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyLanServer.Core.Interfaces;
using MyLanServer.Core.Models;

namespace MyLanServer.Infrastructure.Services;

/// <summary>
///     配置服务实现
/// </summary>
public class ConfigService : IConfigService
{
    private readonly string _configFilePath;
    private readonly ILogger<ConfigService> _logger;

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
        _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "appconfig.json");
    }

    /// <summary>
    ///     加载配置
    /// </summary>
    public async Task<AppConfig> LoadConfigAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (config != null)
                {
                    _logger.LogInformation("Configuration loaded from {Path}", _configFilePath);
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load configuration from {Path}, using defaults", _configFilePath);
        }

        // 返回默认配置
        _logger.LogInformation("Using default configuration");
        return new AppConfig();
    }

    /// <summary>
    ///     保存配置
    /// </summary>
    public async Task SaveConfigAsync(AppConfig config)
    {
        try
        {
            // 确保config目录存在
            var configDir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir)) Directory.CreateDirectory(configDir);

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_configFilePath, json);
            _logger.LogInformation("Configuration saved to {Path}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration to {Path}", _configFilePath);
            throw;
        }
    }
}