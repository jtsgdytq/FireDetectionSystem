using System;
using System.Threading.Tasks;
using FireDetectionSystem.Models;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 报警服务接口
    /// 提供多种报警通知方式
    /// </summary>
    public interface IAlarmService
    {
        /// <summary>
        /// 触发报警
        /// 根据配置自动选择报警方式（声音、桌面通知、邮件等）
        /// </summary>
        /// <param name="detectionRecord">检测记录</param>
        /// <returns>异步任务</returns>
        Task TriggerAlarmAsync(DetectionRecord detectionRecord);

        /// <summary>
        /// 播放声音报警
        /// </summary>
        /// <param name="soundFilePath">声音文件路径</param>
        void PlaySoundAlarm(string soundFilePath);

        /// <summary>
        /// 显示桌面通知
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="message">通知内容</param>
        void ShowDesktopNotification(string title, string message);

        /// <summary>
        /// 发送邮件通知
        /// </summary>
        /// <param name="recipients">收件人列表</param>
        /// <param name="subject">邮件主题</param>
        /// <param name="body">邮件正文</param>
        /// <returns>异步任务</returns>
        Task SendEmailAsync(string[] recipients, string subject, string body);
    }
}
