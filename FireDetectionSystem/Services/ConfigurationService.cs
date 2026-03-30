using Microsoft.Extensions.Configuration;
using System.IO;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 配置服务实现类
    /// 负责加载和管理应用程序配置
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// 构造函数
        /// 加载 appsettings.json 配置文件
        /// </summary>
        public ConfigurationService()
        {
            // 构建配置对象，从 appsettings.json 读取配置
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();
        }

        /// <summary>
        /// 获取配置对象
        /// </summary>
        public IConfiguration Configuration => _configuration;

        /// <summary>
        /// 获取模型文件路径
        /// </summary>
        public string ModelPath => _configuration["ModelSettings:ModelPath"] ?? "models/best.onnx";

        /// <summary>
        /// 获取置信度阈值
        /// </summary>
        public float ConfidenceThreshold =>
            float.TryParse(_configuration["ModelSettings:ConfidenceThreshold"], out var value) ? value : 0.5f;

        /// <summary>
        /// 获取报警阈值
        /// </summary>
        public float AlertThreshold =>
            float.TryParse(_configuration["AlarmSettings:AlertThreshold"], out var value) ? value : 0.7f;

        /// <summary>
        /// 是否启用声音报警
        /// </summary>
        public bool EnableSoundAlarm =>
            bool.TryParse(_configuration["AlarmSettings:EnableSound"], out var value) && value;

        /// <summary>
        /// 是否启用桌面通知
        /// </summary>
        public bool EnableDesktopNotification =>
            bool.TryParse(_configuration["AlarmSettings:EnableDesktopNotification"], out var value) && value;

        /// <summary>
        /// 是否启用邮件通知
        /// </summary>
        public bool EnableEmailAlarm =>
            bool.TryParse(_configuration["AlarmSettings:EnableEmail"], out var value) && value;

        /// <summary>
        /// 获取数据库连接字符串
        /// </summary>
        public string DatabaseConnectionString =>
            _configuration["DatabaseSettings:ConnectionString"] ?? "Data Source=firedetection.db";

        /// <summary>
        /// 获取指定键的配置值
        /// </summary>
        /// <param name="key">配置键，支持冒号分隔的层级结构，如 "ModelSettings:ModelPath"</param>
        /// <returns>配置值，如果不存在返回 null</returns>
        public string GetValue(string key)
        {
            return _configuration[key] ?? string.Empty;
        }

        /// <summary>
        /// 获取指定键的配置值，如果不存在则返回默认值
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">配置键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>配置值</returns>
        public T GetValue<T>(string key, T defaultValue)
        {
            var value = _configuration[key];
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            try
            {
                // 尝试转换为目标类型
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
