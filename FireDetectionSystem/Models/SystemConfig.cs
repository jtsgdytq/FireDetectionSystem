using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FireDetectionSystem.Models
{
    /// <summary>
    /// 系统配置实体类
    /// 用于存储系统的各种配置参数
    /// </summary>
    public class SystemConfig
    {
        /// <summary>
        /// 配置唯一标识符（主键）
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 配置键名
        /// 唯一标识一个配置项，如 "ModelPath", "ConfidenceThreshold"
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 配置值
        /// 存储配置的实际值，以字符串形式存储
        /// </summary>
        [Required]
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 配置描述
        /// 说明该配置项的用途
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// 配置类型
        /// String, Int, Float, Boolean, Json 等
        /// </summary>
        [MaxLength(20)]
        public string ValueType { get; set; } = "String";

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新该配置的用户ID
        /// </summary>
        public int? UpdatedByUserId { get; set; }
    }
}
