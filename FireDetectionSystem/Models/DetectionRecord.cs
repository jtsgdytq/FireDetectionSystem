using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FireDetectionSystem.Models
{
    /// <summary>
    /// 检测记录实体类
    /// 用于存储每次火灾检测的详细信息
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
        /// 记录检测发生的准确时间
        /// </summary>
        public DateTime DetectionTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 检测来源类型
        /// Image: 图片检测
        /// Video: 视频检测
        /// Camera: 摄像头检测
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string SourceType { get; set; } = string.Empty;

        /// <summary>
        /// 来源路径或名称
        /// 图片/视频的文件路径，或摄像头名称
        /// </summary>
        [MaxLength(500)]
        public string? SourcePath { get; set; }

        /// <summary>
        /// 是否检测到火灾
        /// true: 检测到火灾，false: 未检测到
        /// </summary>
        public bool IsFireDetected { get; set; }

        /// <summary>
        /// 检测到的目标数量
        /// 0表示未检测到任何目标
        /// </summary>
        public int DetectionCount { get; set; }

        /// <summary>
        /// 最高置信度
        /// 范围0.0-1.0，表示检测结果的可信程度
        /// </summary>
        public float MaxConfidence { get; set; }

        /// <summary>
        /// 检测结果详情（JSON格式）
        /// 存储所有检测到的目标的详细信息
        /// 格式：[{"Label":"fire","Confidence":0.85,"Box":"100,200 300x400"}]
        /// </summary>
        public string? DetectionDetails { get; set; }

        /// <summary>
        /// 检测结果截图路径
        /// 保存标注后的图片路径，用于后续查看
        /// </summary>
        [MaxLength(500)]
        public string? ScreenshotPath { get; set; }

        /// <summary>
        /// 是否已触发报警
        /// true: 已发送报警通知，false: 未报警
        /// </summary>
        public bool IsAlarmTriggered { get; set; }

        /// <summary>
        /// 执行检测的用户ID
        /// 外键，关联到User表
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// 执行检测的用户对象
        /// 导航属性，用于EF Core关联查询
        /// </summary>
        [ForeignKey("UserId")]
        public User? User { get; set; }

        /// <summary>
        /// 备注信息
        /// 用户可以添加的额外说明
        /// </summary>
        [MaxLength(500)]
        public string? Remarks { get; set; }
    }
}
