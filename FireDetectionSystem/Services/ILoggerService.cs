using System;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 日志服务接口
    /// 提供统一的日志记录功能
    /// </summary>
    public interface ILoggerService
    {
        /// <summary>
        /// 记录调试信息
        /// 用于开发调试，生产环境通常不输出
        /// </summary>
        /// <param name="message">日志消息</param>
        void Debug(string message);

        /// <summary>
        /// 记录一般信息
        /// 用于记录正常的业务流程
        /// </summary>
        /// <param name="message">日志消息</param>
        void Info(string message);

        /// <summary>
        /// 记录警告信息
        /// 用于记录潜在的问题，但不影响系统运行
        /// </summary>
        /// <param name="message">日志消息</param>
        void Warning(string message);

        /// <summary>
        /// 记录错误信息
        /// 用于记录错误，但系统仍可继续运行
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常对象（可选）</param>
        void Error(string message, Exception? exception = null);

        /// <summary>
        /// 记录致命错误
        /// 用于记录导致系统无法继续运行的严重错误
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常对象（可选）</param>
        void Fatal(string message, Exception? exception = null);
    }
}
