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
                // 检测时间索引（用于按时间查询）
                entity.HasIndex(e => e.DetectionTime);

                // 来源类型索引
                entity.HasIndex(e => e.SourceType);

                // 用户ID索引
                entity.HasIndex(e => e.UserId);
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

            // 初始化默认数据
            SeedData(modelBuilder);
        }

        /// <summary>
        /// 初始化默认数据
        /// 创建默认管理员账户和基本配置
        /// </summary>
        /// <param name="modelBuilder">模型构建器</param>
        private void SeedData(ModelBuilder modelBuilder)
        {
            // 创建默认管理员账户
            // 用户名: admin, 密码: admin123
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "admin",
                    // BCrypt 加密后的 "admin123"
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    FullName = "系统管理员",
                    Email = "admin@firedetection.com",
                    Role = "Admin",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                }
            );
        }
    }
}
