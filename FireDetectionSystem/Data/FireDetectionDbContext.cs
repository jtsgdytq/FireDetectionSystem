using Microsoft.EntityFrameworkCore;
using FireDetectionSystem.Models;

namespace FireDetectionSystem.Data
{
    /// <summary>
    /// 数据库上下文类
    /// 负责管理数据库连接和实体映射
    /// </summary>
    public class FireDetectionDbContext : DbContext
    {
        /// <summary>
        /// 用户表
        /// </summary>
        public DbSet<User> Users { get; set; }

        /// <summary>
        /// 检测记录表
        /// </summary>
        public DbSet<DetectionRecord> DetectionRecords { get; set; }

        /// <summary>
        /// 报警日志表
        /// </summary>
        public DbSet<AlarmLog> AlarmLogs { get; set; }

        /// <summary>
        /// 系统配置表
        /// </summary>
        public DbSet<SystemConfig> SystemConfigs { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">数据库上下文选项</param>
        public FireDetectionDbContext(DbContextOptions<FireDetectionDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// 配置数据库模型
        /// 在这里可以设置表关系、索引、默认值等
        /// </summary>
        /// <param name="modelBuilder">模型构建器</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置用户表
            modelBuilder.Entity<User>(entity =>
            {
                // 用户名唯一索引
                entity.HasIndex(e => e.Username).IsUnique();

                // 邮箱索引（用于快速查找）
                entity.HasIndex(e => e.Email);
            });

            // 配置检测记录表
            modelBuilder.Entity<DetectionRecord>(entity =>
            {
                // 检测时间索引（按时间范围查询）
                entity.HasIndex(e => e.DetectionTime);

                // 来源类型索引（区分 Image / Video / Camera）
                entity.HasIndex(e => e.SourceType);

                // 操作用户索引
                entity.HasIndex(e => e.UserId);

                // 来源类型 + 时间复合索引（历史列表分类查询）
                entity.HasIndex(e => new { e.SourceType, e.DetectionTime })
                    .HasDatabaseName("IX_DetectionRecords_SourceType_Time");

                // EventId 索引（聚合同一连续事件的记录）
                entity.HasIndex(e => e.EventId)
                    .HasDatabaseName("IX_DetectionRecords_EventId");

                // IsAlarmTriggered 索引（快速筛选已报警记录）
                entity.HasIndex(e => e.IsAlarmTriggered)
                    .HasDatabaseName("IX_DetectionRecords_IsAlarmTriggered");

                // HandleStatus 索引（快速查询待处置的摄像头报警）
                entity.HasIndex(e => e.HandleStatus)
                    .HasDatabaseName("IX_DetectionRecords_HandleStatus");

                // 处置人索引（按处置人查询历史处置记录）
                entity.HasIndex(e => e.HandledByUserId)
                    .HasDatabaseName("IX_DetectionRecords_HandledByUserId");
            });

            // 配置报警日志表
            modelBuilder.Entity<AlarmLog>(entity =>
            {
                // 报警时间索引
                entity.HasIndex(e => e.AlarmTime);

                // 报警类型索引
                entity.HasIndex(e => e.AlarmType);
            });

            // 配置系统配置表
            modelBuilder.Entity<SystemConfig>(entity =>
            {
                // 配置键唯一索引
                entity.HasIndex(e => e.Key).IsUnique();
            });

            // 注意：默认管理员账户通过 DatabaseService.InitializeDatabaseAsync() 命令式播种，
            // 不在此处使用 HasData，原因：BCrypt.HashPassword 每次调用生成不同 salt，
            // 导致 HasData 幂等性被破坏，重启后可能覆盖用户已修改的密码。
        }
    }
}
