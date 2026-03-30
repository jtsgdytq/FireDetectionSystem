using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FireDetectionSystem.Models
{
    /// <summary>
    /// 用户实体类
    /// 用于存储系统用户的基本信息和权限
    /// </summary>
    public class User
    {
        /// <summary>
        /// 用户唯一标识符（主键）
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 用户名（用于登录）
        /// 必填，最大长度50字符，唯一
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 密码哈希值（使用BCrypt加密）
        /// 不存储明文密码，只存储加密后的哈希值
        /// </summary>
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// 用户真实姓名
        /// 可选，最大长度100字符
        /// </summary>
        [MaxLength(100)]
        public string? FullName { get; set; }

        /// <summary>
        /// 用户邮箱地址
        /// 可选，用于接收报警通知
        /// </summary>
        [MaxLength(100)]
        public string? Email { get; set; }

        /// <summary>
        /// 用户角色
        /// Admin: 管理员，拥有所有权限
        /// Operator: 操作员，可以进行检测和查看记录
        /// Viewer: 查看者，只能查看检测结果
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "Viewer";

        /// <summary>
        /// 账户是否激活
        /// true: 可以登录，false: 禁用登录
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 账户创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后登录时间
        /// 用于追踪用户活跃度
        /// </summary>
        public DateTime? LastLoginAt { get; set; }
    }
}
