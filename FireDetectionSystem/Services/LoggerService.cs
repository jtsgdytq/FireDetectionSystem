using Serilog;
using System;
using System.IO;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 日志服务实现类
    /// 使用 Serilog 实现日志记录功能
    /// </summary>
    public class LoggerService : ILoggerService
    {
        private readonly ILogger _logger;

        /// <summary>
        /// 构造函数
        /// 初始化 Serilog 日志记录器
        /// </summary>
        public LoggerService()
        {
            // 确保日志目录存在
            var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // 配置 Serilog
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug() // 最低日志级别
                .WriteTo.Console() // 输出到控制台
                .WriteTo.File(
                    path: Path.Combine(logDirectory, "app-.log"), // 日志文件路径
                    rollingInterval: RollingInterval.Day, // 按天滚动
                    retainedFileCountLimit: 30, // 保留30天的日志
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}" // 日志格式
                )
                .CreateLogger();

            _logger.Information("日志系统初始化完成");
        }

        /// <summary>
        /// 记录调试信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Debug(string message)
        {
            _logger.Debug(message);
        }

        /// <summary>
        /// 记录一般信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Info(string message)
        {
            _logger.Information(message);
        }

        /// <summary>
        /// 记录警告信息
        /// </summary>
        /// <param name="message">日志消息</param>
        public void Warning(string message)
        {
            _logger.Warning(message);
        }

        /// <summary>
        /// 记录错误信息
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常对象</param>
        public void Error(string message, Exception? exception = null)
        {
            if (exception != null)
            {
                _logger.Error(exception, message);
            }
            else
            {
                _logger.Error(message);
            }
        }

        /// <summary>
        /// 记录致命错误
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常对象</param>
        public void Fatal(string message, Exception? exception = null)
        {
            if (exception != null)
            {
                _logger.Fatal(exception, message);
            }
            else
            {
                _logger.Fatal(message);
            }
        }
    }
}
