using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FireDetectionSystem.Models
{
    /// <summary>
    /// 报警日志实体类
    /// 记录所有触发的报警信息
    /// </summary>
    public class AlarmLog
    {
        /// <summary>
        /// 报警日志唯一标识符（主键）
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 报警触发时间
        /// </summary>
        public DateTime AlarmTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 报警类型
        /// Sound: 声音报警
        /// Desktop: 桌面通知
        /// Email: 邮件通知
        /// SMS: 短信通知
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string AlarmType { get; set; } = string.Empty;

        /// <summary>
        /// 报警级别
        /// Low: 低级别（置信度0.5-0.7）
        /// Medium: 中级别（置信度0.7-0.85）
        /// High: 高级别（置信度0.85-1.0）
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string AlarmLevel { get; set; } = "Medium";

        /// <summary>
        /// 报警消息内容
        /// </summary>
        [Required]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 关联的检测记录ID
        /// 外键，指向触发该报警的检测记录
        /// </summary>
        public int DetectionRecordId { get; set; }

        /// <summary>
        /// 关联的检测记录对象
        /// 导航属性
        /// </summary>
        [ForeignKey("DetectionRecordId")]
        public DetectionRecord? DetectionRecord { get; set; }

        /// <summary>
        /// 报警是否成功发送
        /// true: 发送成功，false: 发送失败
        /// </summary>
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// 失败原因
        /// 如果发送失败，记录失败的原因
        /// </summary>
        [MaxLength(500)]
        public string? FailureReason { get; set; }

        /// <summary>
        /// 接收者信息
        /// 邮件地址、电话号码等
        /// </summary>
        [MaxLength(200)]
        public string? Recipient { get; set; }
    }
}
