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

                // 对包含密码/密钥的配置键名做脱敏处理，防止敏感值写入日志
                var safeValue = key.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                key.IndexOf("secret",   StringComparison.OrdinalIgnoreCase) >= 0
                                ? "***" : value;
                _logger.Info($"设置系统配置成功: {key} = {safeValue}");
            }
            catch (Exception ex)
            {
                _logger.Error($"设置系统配置失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 初始化数据库
        /// 创建数据库表结构并初始化默认数据，同时对旧库补充新增列
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.Info("开始初始化数据库...");

                // 步骤 1：EnsureCreated（新库建表，旧库不动）
                using (var dbContext = CreateDbContext())
                {
                    await dbContext.Database.EnsureCreatedAsync();
                }

                // 步骤 2：使用独立 DbContext 补列，避免 EnsureCreatedAsync 连接状态冲突
                using (var dbContext2 = CreateDbContext())
                {
                    await EnsureVideoEventColumnsAsync(dbContext2);
                }

                // 步骤 3：命令式播种默认管理员账户（仅在不存在时插入，不覆盖已修改的密码）
                await SeedDefaultAdminAsync();

                _logger.Info("数据库初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"数据库初始化失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 命令式播种默认管理员账户。
        /// 仅在 admin 账户不存在时插入，不覆盖用户已修改过的密码。
        /// </summary>
        private async Task SeedDefaultAdminAsync()
        {
            try
            {
                using var dbContext = CreateDbContext();
                var adminExists = await dbContext.Users.AnyAsync(u => u.Username == "admin");
                if (!adminExists)
                {
                    dbContext.Users.Add(new User
                    {
                        Username = "admin",
                        // 初始密码 admin123，使用 BCrypt 哈希；密码在此处即时计算，
                        // 仅在首次初始化时执行一次，不受 HasData 幂等问题影响
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                        FullName = "系统管理员",
                        Email = "admin@firedetection.com",
                        Role = "Admin",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });
                    await dbContext.SaveChangesAsync();
                    _logger.Info("已创建默认管理员账户 (admin/admin123)");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"播种默认管理员失败: {ex.Message}", ex);
                // 不向上抛出：admin 账户缺失不应阻止数据库整体初始化
            }
        }

        /// <summary>
        /// 更新摄像头检测记录的处置状态。
        /// 由 UI 处置操作调用，将 Pending 状态更新为 Confirmed / FalseAlarm / Resolved。
        /// </summary>
        /// <param name="recordId">检测记录 ID</param>
        /// <param name="status">新的处置状态</param>
        /// <param name="handledByUserId">处置人用户 ID</param>
        /// <param name="notes">处置备注</param>
        public async Task UpdateHandleStatusAsync(int recordId, string status, int? handledByUserId, string? notes)
        {
            try
            {
                using var dbContext = CreateDbContext();
                // 按主键加载记录
                var record = await dbContext.DetectionRecords.FindAsync(recordId);
                if (record == null)
                {
                    _logger.Warning($"UpdateHandleStatus: 未找到记录 ID={recordId}");
                    return;
                }

                // 更新处置信息
                record.HandleStatus    = status;
                record.HandledByUserId = handledByUserId;
                record.HandledAt       = DateTime.Now;
                record.HandleNotes     = notes;

                await dbContext.SaveChangesAsync();
                _logger.Info($"处置记录成功: ID={recordId}, Status={status}, OperatorId={handledByUserId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"更新处置状态失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 获取所有待处置的摄像头报警记录（HandleStatus = "Pending"）。
        /// 按检测时间倒序排列，包含执行检测用户的导航属性。
        /// </summary>
        /// <returns>待处置记录列表</returns>
        public async Task<List<DetectionRecord>> GetPendingCameraAlarmsAsync()
        {
            try
            {
                using var dbContext = CreateDbContext();
                return await dbContext.DetectionRecords
                    .Include(r => r.User)
                    .Where(r => r.SourceType == "Camera" && r.HandleStatus == "Pending")
                    .OrderByDescending(r => r.DetectionTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"获取待处置报警失败: {ex.Message}", ex);
                return new List<DetectionRecord>();
            }
        }

        /// <summary>
        /// 检查 DetectionRecords 表是否缺少所需字段，缺少则自动补列。
        /// 使用一次性 PRAGMA table_info 将所有列名读入 HashSet，再逐列判断，
        /// 避免在 EF Core 共享连接上多次打开 DataReader 导致冲突。
        /// </summary>
        /// <param name="dbContext">数据库上下文（须为独立实例，不与 EnsureCreatedAsync 共用）</param>
        private async Task EnsureVideoEventColumnsAsync(FireDetectionDbContext dbContext)
        {
            // 需要补充的列：(列名, SQLite 类型)
            // 仅包含 EnsureCreated 建表后可能缺失的字段（旧库升级路径）
            var newColumns = new[]
            {
                ("EventId",         "TEXT"),
                ("HandleStatus",    "TEXT"),
                ("HandledByUserId", "INTEGER"),
                ("HandledAt",       "TEXT"),
                ("HandleNotes",     "TEXT"),
            };

            try
            {
                // 一次性读取所有已有列名到 HashSet，关闭 Reader 后再执行 DDL，避免连接冲突
                var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var conn = dbContext.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open)
                    await conn.OpenAsync();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA table_info(DetectionRecords)";
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        // PRAGMA table_info 返回第二列（index=1）是列名
                        existingColumns.Add(reader.GetString(1));
                    }
                } // reader 和 cmd 在此关闭，连接回到空闲状态

                // 逐列判断是否需要补充
                foreach (var (columnName, columnType) in newColumns)
                {
                    if (existingColumns.Contains(columnName))
                        continue; // 列已存在，跳过

                    try
                    {
                        // columnName 来自上方硬编码数组，无外部输入，无注入风险
                        await dbContext.Database.ExecuteSqlRawAsync(
                            $"ALTER TABLE DetectionRecords ADD COLUMN {columnName} {columnType}");
                        _logger.Info($"数据库升级：DetectionRecords 表已添加列 {columnName}");
                    }
                    catch (Exception ex)
                    {
                        // 单列补充失败不中断整体初始化，记录警告继续
                        _logger.Warning($"补充列 {columnName} 失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // PRAGMA 查询失败不阻断整体流程
                _logger.Warning($"检查数据库列结构失败: {ex.Message}");
            }
        }
    }
}
