using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FireDetectionSystem.Data;
using FireDetectionSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 数据库服务实现类
    /// 提供检测记录和系统配置的数据访问功能
    /// </summary>
    public class DatabaseService : IDatabaseService
    {
        private readonly ILoggerService _logger;
        private readonly IConfigurationService _configService;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志服务</param>
        /// <param name="configService">配置服务</param>
        public DatabaseService(ILoggerService logger, IConfigurationService configService)
        {
            _logger = logger;
            _configService = configService;
        }

        /// <summary>
        /// 创建数据库上下文实例
        /// 每次调用都创建新的实例，避免生命周期问题
        /// </summary>
        /// <returns>数据库上下文实例</returns>
        private FireDetectionDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<FireDetectionDbContext>();
            optionsBuilder.UseSqlite(_configService.DatabaseConnectionString);
            return new FireDetectionDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// 保存检测记录到数据库
        /// </summary>
        /// <param name="record">检测记录对象</param>
        /// <returns>保存后的记录ID</returns>
        public async Task<int> SaveDetectionRecordAsync(DetectionRecord record)
        {
            try
            {
                using var dbContext = CreateDbContext();
                dbContext.DetectionRecords.Add(record);
                await dbContext.SaveChangesAsync();

                _logger.Info($"保存检测记录成功: ID={record.Id}, 来源={record.SourceType}");
                return record.Id;
            }
            catch (Exception ex)
            {
                _logger.Error($"保存检测记录失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 根据时间范围查询检测记录
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>检测记录列表</returns>
        public async Task<List<DetectionRecord>> GetDetectionRecordsByDateRangeAsync(
            DateTime startTime, DateTime endTime)
        {
            try
            {
                using var dbContext = CreateDbContext();
                return await dbContext.DetectionRecords
                    .Include(r => r.User) // 包含用户信息
                    .Where(r => r.DetectionTime >= startTime && r.DetectionTime <= endTime)
                    .OrderByDescending(r => r.DetectionTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"查询检测记录失败: {ex.Message}", ex);
                return new List<DetectionRecord>();
            }
        }

        /// <summary>
        /// 根据来源类型查询检测记录
        /// </summary>
        /// <param name="sourceType">来源类型（Image/Video/Camera）</param>
        /// <param name="limit">返回记录数量限制</param>
        /// <returns>检测记录列表</returns>
        public async Task<List<DetectionRecord>> GetDetectionRecordsBySourceTypeAsync(
            string sourceType, int limit = 100)
        {
            try
            {
                using var dbContext = CreateDbContext();
                return await dbContext.DetectionRecords
                    .Include(r => r.User)
                    .Where(r => r.SourceType == sourceType)
                    .OrderByDescending(r => r.DetectionTime)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"查询检测记录失败: {ex.Message}", ex);
                return new List<DetectionRecord>();
            }
        }

        /// <summary>
        /// 获取最近的检测记录
        /// </summary>
        /// <param name="count">返回记录数量</param>
        /// <returns>检测记录列表</returns>
        public async Task<List<DetectionRecord>> GetRecentDetectionRecordsAsync(int count = 50)
        {
            try
            {
                using var dbContext = CreateDbContext();
                return await dbContext.DetectionRecords
                    .Include(r => r.User)
                    .OrderByDescending(r => r.DetectionTime)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"查询最近检测记录失败: {ex.Message}", ex);
                return new List<DetectionRecord>();
            }
        }

        /// <summary>
        /// 获取检测统计信息
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>统计信息字典</returns>
        public async Task<Dictionary<string, object>> GetDetectionStatisticsAsync(
            DateTime startTime, DateTime endTime)
        {
            try
            {
                using var dbContext = CreateDbContext();
                var records = await dbContext.DetectionRecords
                    .Where(r => r.DetectionTime >= startTime && r.DetectionTime <= endTime)
                    .ToListAsync();

                var statistics = new Dictionary<string, object>
                {
                    ["TotalDetections"] = records.Count,
                    ["FireDetected"] = records.Count(r => r.IsFireDetected),
                    ["AlarmTriggered"] = records.Count(r => r.IsAlarmTriggered),
                    ["AverageConfidence"] = records.Any() ? records.Average(r => r.MaxConfidence) : 0,
                    ["MaxConfidence"] = records.Any() ? records.Max(r => r.MaxConfidence) : 0,
                    ["BySourceType"] = records.GroupBy(r => r.SourceType)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.Error($"获取统计信息失败: {ex.Message}", ex);
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// 获取报警日志
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>报警日志列表</returns>
        public async Task<List<AlarmLog>> GetAlarmLogsAsync(DateTime startTime, DateTime endTime)
        {
            try
            {
                using var dbContext = CreateDbContext();
                return await dbContext.AlarmLogs
                    .Include(a => a.DetectionRecord)
                    .Where(a => a.AlarmTime >= startTime && a.AlarmTime <= endTime)
                    .OrderByDescending(a => a.AlarmTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"查询报警日志失败: {ex.Message}", ex);
                return new List<AlarmLog>();
            }
        }

        /// <summary>
        /// 获取系统配置
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>配置值，不存在返回 null</returns>
        public async Task<string?> GetSystemConfigAsync(string key)
        {
            try
            {
                using var dbContext = CreateDbContext();
                var config = await dbContext.SystemConfigs
                    .FirstOrDefaultAsync(c => c.Key == key);

                return config?.Value;
            }
            catch (Exception ex)
            {
                _logger.Error($"获取系统配置失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 设置系统配置
        /// </summary>
        /// <param name="key">配置键</param>
        /// <param name="value">配置值</param>
        /// <param name="description">配置描述</param>
        /// <param name="valueType">值类型</param>
        /// <param name="userId">操作用户ID</param>
        public async Task SetSystemConfigAsync(string key, string value,
            string? description = null, string valueType = "String", int? userId = null)
        {
            try
            {
                using var dbContext = CreateDbContext();
                var config = await dbContext.SystemConfigs
                    .FirstOrDefaultAsync(c => c.Key == key);

                if (config != null)
                {
                    // 更新现有配置
                    config.Value = value;
                    config.Description = description ?? config.Description;
                    config.ValueType = valueType;
                    config.UpdatedAt = DateTime.Now;
                    config.UpdatedByUserId = userId;
                }
                else
                {
                    // 创建新配置
                    config = new SystemConfig
                    {
                        Key = key,
                        Value = value,
                        Description = description,
                        ValueType = valueType,
                        UpdatedAt = DateTime.Now,
                        UpdatedByUserId = userId
                    };
                    dbContext.SystemConfigs.Add(config);
                }

                await dbContext.SaveChangesAsync();
                _logger.Info($"设置系统配置成功: {key} = {value}");
            }
            catch (Exception ex)
            {
                _logger.Error($"设置系统配置失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 初始化数据库
        /// 创建数据库表结构并初始化默认数据
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.Info("开始初始化数据库...");

                using var dbContext = CreateDbContext();
                // 确保数据库已创建
                await dbContext.Database.EnsureCreatedAsync();

                _logger.Info("数据库初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"数据库初始化失败: {ex.Message}", ex);
                throw;
            }
        }
    }
}
