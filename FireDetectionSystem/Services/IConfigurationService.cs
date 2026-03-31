using Microsoft.Extensions.Configuration;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 配置服务接口
    /// 提供访问和持久化应用程序配置的统一接口
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>获取模型文件路径</summary>
        string ModelPath { get; }

        /// <summary>获取置信度阈值（默认 0.5）</summary>
        float ConfidenceThreshold { get; }

        /// <summary>获取报警阈值（默认 0.7）</summary>
        float AlertThreshold { get; }

        /// <summary>是否启用声音报警</summary>
        bool EnableSoundAlarm { get; }

        /// <summary>是否启用桌面通知</summary>
        bool EnableDesktopNotification { get; }

        /// <summary>是否启用邮件通知</summary>
        bool EnableEmailAlarm { get; }

        /// <summary>获取数据库连接字符串</summary>
        string DatabaseConnectionString { get; }

        /// <summary>获取底层配置对象（供 AlarmService 读取 EmailRecipients 数组用）</summary>
        IConfiguration Configuration { get; }

        /// <summary>获取指定键的配置值</summary>
        string GetValue(string key);

        /// <summary>获取指定键的配置值，不存在时返回默认值</summary>
        T GetValue<T>(string key, T defaultValue);

        /// <summary>
        /// 将所有设置一次性原子写入 appsettings.json 并立即 Reload。
        /// 使用临时文件 + File.Replace 防止写入中断导致配置损坏。
        /// </summary>
        void SaveAllSettings(
            string modelPath, float confidenceThreshold,
            bool enableSound, bool enableDesktop, bool enableEmail, float alertThreshold,
            string[] emailRecipients,
            string smtpServer, int smtpPort, bool useSsl,
            string smtpUsername, string smtpPassword,
            string fromAddress, string fromName);

        // 以下四个方法保留为空实现兼容旧调用，实际保存请使用 SaveAllSettings
        void UpdateModelSettings(string modelPath, float confidenceThreshold);
        void UpdateAlarmToggles(bool enableSound, bool enableDesktop, bool enableEmail, float alertThreshold);
        void UpdateEmailRecipients(string[] recipients);
        void UpdateEmailSettings(string smtpServer, int smtpPort, bool useSsl,
            string username, string password, string fromAddress, string fromName);
    }
}
