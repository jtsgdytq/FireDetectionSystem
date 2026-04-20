using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 配置服务实现类
    /// 负责加载和管理应用程序配置
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _configuration;
        private readonly object _fileLock = new object();

        // 使用 JsonWriterOptions 替代 JsonSerializerOptions，避免 .NET 8 要求 TypeInfoResolver 的问题
        private static readonly JsonWriterOptions _jsonWriterOptions =
            new JsonWriterOptions { Indented = true };

        private static readonly Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public ConfigurationService()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();
        }

        public IConfiguration Configuration => _configuration;

        public string ModelPath => _configuration["ModelSettings:ModelPath"] ?? "models/best.onnx";

        public float ConfidenceThreshold =>
            float.TryParse(_configuration["ModelSettings:ConfidenceThreshold"], out var v) ? v : 0.5f;

        public float AlertThreshold =>
            float.TryParse(_configuration["AlarmSettings:AlertThreshold"], out var v) ? v : 0.7f;

        public bool EnableSoundAlarm =>
            bool.TryParse(_configuration["AlarmSettings:EnableSound"], out var v) && v;

        public bool EnableDesktopNotification =>
            bool.TryParse(_configuration["AlarmSettings:EnableDesktopNotification"], out var v) && v;

        public bool EnableEmailAlarm =>
            bool.TryParse(_configuration["AlarmSettings:EnableEmail"], out var v) && v;

        public string DatabaseConnectionString =>
            _configuration["DatabaseSettings:ConnectionString"] ?? "Data Source=firedetection.db";

        public string GetValue(string key) => _configuration[key] ?? string.Empty;

        public T GetValue<T>(string key, T defaultValue)
        {
            var value = _configuration[key];
            if (string.IsNullOrEmpty(value)) return defaultValue;
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        // ──────────────────────────────────────────────
        // 写入方法：所有设置合并为一次原子写入
        // ──────────────────────────────────────────────

        public void UpdateModelSettings(string modelPath, float confidenceThreshold) { }
        public void UpdateAlarmToggles(bool enableSound, bool enableDesktop, bool enableEmail, float alertThreshold) { }
        public void UpdateEmailRecipients(string[] recipients) { }
        public void UpdateEmailSettings(string smtpServer, int smtpPort, bool useSsl,
            string username, string password, string fromAddress, string fromName) { }

        /// <summary>
        /// 将所有设置一次性写入 appsettings.json（原子写入，防止中途中断导致配置损坏）
        /// </summary>
        public void SaveAllSettings(
            string modelPath, float confidenceThreshold,
            bool enableSound, bool enableDesktop, bool enableEmail, float alertThreshold,
            string[] emailRecipients,
            string smtpServer, int smtpPort, bool useSsl,
            string smtpUsername, string smtpPassword,
            string fromAddress, string fromName)
        {
            lock (_fileLock)
            {
                var configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                var text = File.ReadAllText(configPath, _utf8NoBom);
                var json = JsonNode.Parse(text)
                    ?? throw new InvalidOperationException("appsettings.json 解析失败");

                // 确保所有必需节点存在
                EnsureSection(json, "ModelSettings");
                EnsureSection(json, "AlarmSettings");
                EnsureSection(json, "EmailSettings");

                // 模型设置
                json["ModelSettings"]!["ModelPath"] = modelPath;
                json["ModelSettings"]!["ConfidenceThreshold"] = Math.Round(confidenceThreshold, 2);

                // 报警开关
                json["AlarmSettings"]!["EnableSound"] = enableSound;
                json["AlarmSettings"]!["EnableDesktopNotification"] = enableDesktop;
                json["AlarmSettings"]!["EnableEmail"] = enableEmail;
                json["AlarmSettings"]!["AlertThreshold"] = Math.Round(alertThreshold, 2);

                // 收件人列表
                var recipientsArray = new JsonArray();
                foreach (var r in emailRecipients)
                    recipientsArray.Add(r);
                json["AlarmSettings"]!["EmailRecipients"] = recipientsArray;

                // SMTP 配置
                json["EmailSettings"]!["SmtpServer"] = smtpServer;
                json["EmailSettings"]!["SmtpPort"] = smtpPort;
                json["EmailSettings"]!["UseSsl"] = useSsl;
                json["EmailSettings"]!["Username"] = smtpUsername;
                json["EmailSettings"]!["Password"] = smtpPassword;
                json["EmailSettings"]!["FromAddress"] = fromAddress;
                json["EmailSettings"]!["FromName"] = fromName;

                // 原子写入：写临时文件 → 替换原文件（备份旧文件）
                // 使用 Utf8JsonWriter + FileStream 直接写入，避免 JsonSerializerOptions 的 TypeInfoResolver 限制
                var tmpPath = configPath + ".tmp";
                var bakPath = configPath + ".bak";
                using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new Utf8JsonWriter(fs, _jsonWriterOptions))
                {
                    json.WriteTo(writer);
                }
                File.Replace(tmpPath, configPath, bakPath);

                (_configuration as IConfigurationRoot)?.Reload();
            }
        }

        private static void EnsureSection(JsonNode root, string sectionName)
        {
            if (root[sectionName] == null)
                root[sectionName] = new JsonObject();
        }
    }
}
