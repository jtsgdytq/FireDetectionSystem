using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using FireDetectionSystem.Data;
using FireDetectionSystem.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 报警服务实现类
    /// 实现声音报警、桌面通知和邮件通知功能
    /// </summary>
    public class AlarmService : IAlarmService
    {
        private readonly IConfigurationService _configService;
        private readonly ILoggerService _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="configService">配置服务</param>
        /// <param name="logger">日志服务</param>
        public AlarmService(
            IConfigurationService configService,
            ILoggerService logger)
        {
            _configService = configService;
            _logger = logger;
        }

        /// <summary>
        /// 创建数据库上下文实例
        /// 每次操作创建新的 DbContext，避免生命周期冲突
        /// </summary>
        /// <returns>数据库上下文实例</returns>
        private FireDetectionDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<FireDetectionDbContext>();
            optionsBuilder.UseSqlite(_configService.DatabaseConnectionString);
            return new FireDetectionDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// 触发报警
        /// 根据配置自动选择报警方式
        /// </summary>
        /// <param name="detectionRecord">检测记录</param>
        public async Task TriggerAlarmAsync(DetectionRecord detectionRecord)
        {
            try
            {
                // 判断是否需要报警（置信度超过阈值）
                if (detectionRecord.MaxConfidence < _configService.AlertThreshold)
                {
                    return;
                }

                _logger.Info($"触发报警：检测到火灾，置信度 {detectionRecord.MaxConfidence:P1}");

                // 确定报警级别
                var alarmLevel = GetAlarmLevel(detectionRecord.MaxConfidence);
                var message = $"检测到火灾！置信度：{detectionRecord.MaxConfidence:P1}，来源：{detectionRecord.SourcePath}";

                // 声音报警
                if (_configService.EnableSoundAlarm)
                {
                    try
                    {
                        var soundPath = _configService.GetValue("AlarmSettings:SoundFilePath", "");
                        if (!string.IsNullOrEmpty(soundPath) && File.Exists(soundPath))
                        {
                            PlaySoundAlarm(soundPath);
                            await SaveAlarmLogAsync(detectionRecord.Id, "Sound", alarmLevel, message, true);
                        }
                        else
                        {
                            // 使用系统默认声音
                            SystemSounds.Exclamation.Play();
                            await SaveAlarmLogAsync(detectionRecord.Id, "Sound", alarmLevel, message, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"声音报警失败: {ex.Message}", ex);
                        await SaveAlarmLogAsync(detectionRecord.Id, "Sound", alarmLevel, message, false, ex.Message);
                    }
                }

                // 桌面通知
                if (_configService.EnableDesktopNotification)
                {
                    try
                    {
                        ShowDesktopNotification("火灾检测报警", message);
                        await SaveAlarmLogAsync(detectionRecord.Id, "Desktop", alarmLevel, message, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"桌面通知失败: {ex.Message}", ex);
                        await SaveAlarmLogAsync(detectionRecord.Id, "Desktop", alarmLevel, message, false, ex.Message);
                    }
                }

                // 邮件通知
                if (_configService.EnableEmailAlarm)
                {
                    try
                    {
                        var recipients = _configService.Configuration
                            .GetSection("AlarmSettings:EmailRecipients")
                            .Get<string[]>() ?? Array.Empty<string>();

                        
                        
                        if (recipients.Length > 0)
                        {
                            await SendEmailAsync(recipients, "火灾检测报警", message);
                            await SaveAlarmLogAsync(detectionRecord.Id, "Email", alarmLevel, message, true,
                                string.Join(", ", recipients));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"邮件通知失败: {ex.Message}", ex);
                        await SaveAlarmLogAsync(detectionRecord.Id, "Email", alarmLevel, message, false, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"触发报警失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 播放声音报警
        /// </summary>
        /// <param name="soundFilePath">声音文件路径</param>
        public void PlaySoundAlarm(string soundFilePath)
        {
            try
            {
                if (File.Exists(soundFilePath))
                {
                    using var player = new SoundPlayer(soundFilePath);
                    player.Play();
                    _logger.Info($"播放报警声音: {soundFilePath}");
                }
                else
                {
                    _logger.Warning($"报警声音文件不存在: {soundFilePath}");
                    SystemSounds.Exclamation.Play();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"播放声音失败: {ex.Message}", ex);
                // 降级使用系统声音
                SystemSounds.Exclamation.Play();
            }
        }

        /// <summary>
        /// 显示桌面通知
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="message">通知内容</param>
        public void ShowDesktopNotification(string title, string message)
        {
            try
            {
                // 在 UI 线程上显示消息框
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                });

                _logger.Info($"显示桌面通知: {title} - {message}");
            }
            catch (Exception ex)
            {
                _logger.Error($"显示桌面通知失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 发送邮件通知
        /// </summary>
        /// <param name="recipients">收件人列表</param>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">邮件正文</param>
        public async Task SendEmailAsync(string[] recipients, string subject, string body)
        {
            try
            {
                var smtpServerRaw = _configService.GetValue("EmailSettings:SmtpServer", "").Trim();
                var smtpPort = _configService.GetValue("EmailSettings:SmtpPort", 587);
                var useSsl = _configService.GetValue("EmailSettings:UseSsl", true);
                var username = _configService.GetValue("EmailSettings:Username", "");
                var password = _configService.GetValue("EmailSettings:Password", "");
                var fromAddress = _configService.GetValue("EmailSettings:FromAddress", "");
                var fromName = _configService.GetValue("EmailSettings:FromName", "火灾检测系统");

                // 验证配置
                if (string.IsNullOrEmpty(smtpServerRaw) || string.IsNullOrEmpty(username) ||
                    string.IsNullOrEmpty(password) || string.IsNullOrEmpty(fromAddress))
                {
                    _logger.Warning("邮件配置不完整，无法发送邮件");
                    return;
                }

                var (smtpServer, normalizedPort) = NormalizeSmtpEndpoint(smtpServerRaw, smtpPort);
                if (Uri.CheckHostName(smtpServer) == UriHostNameType.Unknown)
                {
                    _logger.Warning($"SMTP 服务器地址无效：{smtpServerRaw}");
                    return;
                }

                // 创建邮件消息
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromAddress));

                foreach (var recipient in recipients)
                {
                    message.To.Add(MailboxAddress.Parse(recipient));
                }

                message.Subject = subject;
                message.Body = new TextPart("plain") { Text = body };

                // 发送邮件
                using var client = new SmtpClient();
                var socketOptions = ResolveSocketOptions(useSsl, normalizedPort);

                try
                {
                    await client.ConnectAsync(smtpServer, normalizedPort, socketOptions);
                }
                catch (SslHandshakeException) when (socketOptions == SecureSocketOptions.SslOnConnect && normalizedPort == 587)
                {
                    // 某些配置会把 587 + UseSsl=true 误当成 SSL 直连，回退到 STARTTLS 重试一次
                    await client.ConnectAsync(smtpServer, normalizedPort, SecureSocketOptions.StartTls);
                }

                await client.AuthenticateAsync(username, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.Info($"邮件发送成功: {subject} -> {string.Join(", ", recipients)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"发送邮件失败: {ex.Message}", ex);
                throw;
            }
        }

        private static (string Host, int Port) NormalizeSmtpEndpoint(string smtpServerRaw, int fallbackPort)
        {
            var value = smtpServerRaw.Trim();

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                var port = uri.IsDefaultPort ? fallbackPort : uri.Port;
                return (uri.Host, port);
            }

            // 支持 host:port 的简写输入
            var firstColon = value.IndexOf(':');
            var lastColon = value.LastIndexOf(':');
            if (firstColon > 0 && firstColon == lastColon && lastColon < value.Length - 1)
            {
                var host = value[..lastColon].Trim();
                if (int.TryParse(value[(lastColon + 1)..], out var parsedPort))
                    return (host, parsedPort);
            }

            return (value, fallbackPort);
        }

        private static SecureSocketOptions ResolveSocketOptions(bool useSsl, int port)
        {
            if (port == 465)
                return SecureSocketOptions.SslOnConnect;

            if (port == 587)
                return useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.StartTlsWhenAvailable;

            return useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        }

        /// <summary>
        /// 根据置信度确定报警级别
        /// </summary>
        /// <param name="confidence">置信度</param>
        /// <returns>报警级别</returns>
        private string GetAlarmLevel(float confidence)
        {
            if (confidence >= 0.85f)
                return "High";
            else if (confidence >= 0.7f)
                return "Medium";
            else
                return "Low";
        }

        /// <summary>
        /// 保存报警日志到数据库
        /// </summary>
        private async Task SaveAlarmLogAsync(int detectionRecordId, string alarmType,
            string alarmLevel, string message, bool isSuccess, string? recipient = null, string? failureReason = null)
        {
            try
            {
                using var dbContext = CreateDbContext();

                var alarmLog = new AlarmLog
                {
                    AlarmTime = DateTime.Now,
                    AlarmType = alarmType,
                    AlarmLevel = alarmLevel,
                    Message = message,
                    DetectionRecordId = detectionRecordId,
                    IsSuccess = isSuccess,
                    Recipient = recipient,
                    FailureReason = failureReason
                };

                dbContext.AlarmLogs.Add(alarmLog);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"保存报警日志失败: {ex.Message}", ex);
            }
        }
    }
}
