using Microsoft.Extensions.Configuration;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 配置服务接口
    /// 提供访问应用程序配置的统一接口
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// 获取模型文件路径
        /// </summary>
        string ModelPath { get; }

        /// <summary>
        /// 获取置信度阈值（默认0.5）
        /// </summary>
        float ConfidenceThreshold { get; }

        /// <summary>
        /// 获取报警阈值（默认0.7）
        /// </summary>
        float AlertThreshold { get; }

        /// <summary>
        /// 是否启用声音报警
        /// </summary>
        bool EnableSoundAlarm { get; }

        /// <summary>
        /// 是否启用桌面通知
        /// </summary>
        bool EnableDesktopNotification { get; }

        /// <summary>
        /// 是否启用邮件通知
        /// </summary>
        bool EnableEmailAlarm { get; }

        /// <summary>
        /// 获取数据库连接字符串
        /// </summary>
        string DatabaseConnectionString { get; }

        /// <summary>
        /// 获取配置对象
        /// </summary>
        IConfiguration Configuration { get; }

        /// <summary>
        /// 获取指定键的配置值
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>配置值</returns>
        string GetValue(string key);

        /// <summary>
        /// 获取指定键的配置值，如果不存在则返回默认值
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">配置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>配置值</returns>
        T GetValue<T>(string key, T defaultValue);
    }
}
