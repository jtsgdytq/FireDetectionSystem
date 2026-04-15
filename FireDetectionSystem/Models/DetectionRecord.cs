using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FireDetectionSystem.Models
{
    /// <summary>
    /// 检测记录实体类
    /// 存储每次火灾检测的核心信息以及摄像头检测的处置结果
    /// </summary>
    public class DetectionRecord
    {
        /// <summary>
        /// 记录唯一标识符（主键）
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 检测时间
        /// </summary>
        public DateTime DetectionTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 检测来源类型：Image / Video / Camera
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string SourceType { get; set; } = string.Empty;

        /// <summary>
        /// 来源路径或摄像头名称
        /// </summary>
        [MaxLength(500)]
        public string? SourcePath { get; set; }

        /// <summary>
        /// 是否检测到火灾
        /// </summary>
        public bool IsFireDetected { get; set; }

        /// <summary>
        /// 最高置信度（0~1）
        /// </summary>
        public float MaxConfidence { get; set; }

        /// <summary>
        /// 是否已触发报警
        /// </summary>
        public bool IsAlarmTriggered { get; set; }

        /// <summary>
        /// 执行检测的用户 ID（外键 → Users）
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// 执行检测的用户导航属性
        /// </summary>
        [ForeignKey("UserId")]
        public User? User { get; set; }

        /// <summary>
        /// 连续事件唯一标识（视频/摄像头检测，同一连续触发区间共享同一 EventId）
        /// 图片检测该字段为 null
        /// </summary>
        [MaxLength(64)]
        public string? EventId { get; set; }

        // ── 处置信息（仅摄像头检测填写）─────────────────────────────────────

        /// <summary>
        /// 处置状态：
        /// Pending   — 待处置（系统自动写入）
        /// Confirmed — 已确认火灾
        /// FalseAlarm — 误报/演习
        /// Resolved  — 已处置完毕
        /// </summary>
        [MaxLength(20)]
        public string? HandleStatus { get; set; }

        /// <summary>
        /// 处置人用户 ID（外键 → Users）
        /// </summary>
        public int? HandledByUserId { get; set; }

        /// <summary>
        /// 处置人导航属性
        /// </summary>
        [ForeignKey("HandledByUserId")]
        public User? HandledByUser { get; set; }

        /// <summary>
        /// 处置时间
        /// </summary>
        public DateTime? HandledAt { get; set; }

        /// <summary>
        /// 处置备注（如"已通知消防"、"误报已核实"）
        /// </summary>
        [MaxLength(500)]
        public string? HandleNotes { get; set; }
    }
}
