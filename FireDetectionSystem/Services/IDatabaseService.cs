using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FireDetectionSystem.Models;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 数据库服务接口
    /// 提供检测记录和系统配置的数据访问功能
    /// </summary>
    public interface IDatabaseService
    {
        /// <summary>
        /// 保存检测记录到数据库
        /// </summary>
        /// <param name="record">检测记录对象</param>
        /// <returns>保存后的记录ID</returns>
        Task<int> SaveDetectionRecordAsync(DetectionRecord record);

        /// <summary>
        /// 根据时间范围查询检测记录
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>检测记录列表</returns>
        Task<List<DetectionRecord>> GetDetectionRecordsByDateRangeAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// 根据来源类型查询检测记录
        /// </summary>
        /// <param name="sourceType">来源类型（Image/Video/Camera）</param>
        /// <param name="limit">返回记录数量限制</param>
        /// <returns>检测记录列表</returns>
        Task<List<DetectionRecord>> GetDetectionRecordsBySourceTypeAsync(string sourceType, int limit = 100);

        /// <summary>
        /// 获取最近的检测记录
        /// </summary>
        /// <param name="count">返回记录数量</param>
        /// <returns>检测记录列表</returns>
        Task<List<DetectionRecord>> GetRecentDetectionRecordsAsync(int count = 50);

        /// <summary>
        /// 获取检测统计信息
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>统计信息字典</returns>
        Task<Dictionary<string, object>> GetDetectionStatisticsAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// 获取报警日志
        /// </summary>
        /// <param name="startTime">开始时间</param>
        /// <param name="endTime">结束时间</param>
        /// <returns>报警日志列表</returns>
        Task<List<AlarmLog>> GetAlarmLogsAsync(DateTime startTime, DateTime endTime);

        /// <summary>
        /// 获取系统配置
        /// </summary>
        /// <param name="key">配置键</param>
        /// <returns>配置值，不存在返回 null</returns>
        Task<string?> GetSystemConfigAsync(string key);

        /// <summary>
        /// 设置系统配置
        /// </summary>
        /// <param name="key">配置键</param>
        /// <param name="value">配置值</param>
        /// <param name="description">配置描述</param>
        /// <param name="valueType">值类型</param>
        /// <param name="userId">操作用户ID</param>
        Task SetSystemConfigAsync(string key, string value, string? description = null,
            string valueType = "String", int? userId = null);

        /// <summary>
        /// 初始化数据库
        /// </summary>
        Task InitializeDatabaseAsync();

        /// <summary>
        /// 更新摄像头检测记录的处置状态
        /// </summary>
        /// <param name="recordId">检测记录 ID</param>
        /// <param name="status">处置状态（Confirmed / FalseAlarm / Resolved）</param>
        /// <param name="handledByUserId">处置人用户 ID</param>
        /// <param name="notes">处置备注</param>
        Task UpdateHandleStatusAsync(int recordId, string status, int? handledByUserId, string? notes);

        /// <summary>
        /// 获取所有待处置的摄像头报警记录（HandleStatus = Pending）
        /// </summary>
        /// <returns>待处置记录列表，按检测时间倒序排列</returns>
        Task<List<DetectionRecord>> GetPendingCameraAlarmsAsync();
    }
}
