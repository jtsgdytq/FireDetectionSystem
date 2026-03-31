using System.Collections.Generic;
using System.Threading.Tasks;
using FireDetectionSystem.Models;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 用户服务接口
    /// 提供用户认证和管理功能
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// 用户登录
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码（明文）</param>
        /// <returns>登录成功返回用户对象，失败返回 null</returns>
        Task<User?> LoginAsync(string username, string password);

        /// <summary>
        /// 用户注册
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码（明文）</param>
        /// <param name="fullName">真实姓名</param>
        /// <param name="email">邮箱</param>
        /// <param name="role">角色</param>
        /// <returns>注册成功返回用户对象，失败返回 null</returns>
        Task<User?> RegisterAsync(string username, string password, string? fullName = null,
            string? email = null, string role = "Viewer");

        /// <summary>
        /// 根据用户名获取用户
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>用户对象，不存在返回 null</returns>
        Task<User?> GetUserByUsernameAsync(string username);

        /// <summary>
        /// 根据ID获取用户
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>用户对象，不存在返回 null</returns>
        Task<User?> GetUserByIdAsync(int userId);

        /// <summary>
        /// 更新用户最后登录时间
        /// </summary>
        /// <param name="userId">用户ID</param>
        Task UpdateLastLoginTimeAsync(int userId);

        /// <summary>
        /// 修改密码
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="oldPassword">旧密码</param>
        /// <param name="newPassword">新密码</param>
        /// <returns>修改成功返回 true，失败返回 false</returns>
        Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);

        /// <summary>
        /// 验证密码
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="passwordHash">密码哈希值</param>
        /// <returns>密码正确返回 true，否则返回 false</returns>
        bool VerifyPassword(string password, string passwordHash);

        /// <summary>
        /// 获取所有用户列表
        /// </summary>
        /// <returns>用户列表</returns>
        Task<List<User>> GetAllUsersAsync();

        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="user">用户对象</param>
        /// <param name="operatorUsername">操作人用户名</param>
        /// <returns>更新成功返回 true，失败返回 false</returns>
        Task<bool> UpdateUserAsync(User user, string operatorUsername);

        /// <summary>
        /// 删除用户（物理删除）
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="operatorUsername">操作人用户名</param>
        /// <returns>删除成功返回 true，失败返回 false</returns>
        Task<bool> DeleteUserAsync(int userId, string operatorUsername);

        /// <summary>
        /// 禁用用户账户
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="operatorUsername">操作人用户名</param>
        /// <returns>禁用成功返回 true，失败返回 false</returns>
        Task<bool> DisableUserAsync(int userId, string operatorUsername);

        /// <summary>
        /// 启用用户账户
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="operatorUsername">操作人用户名</param>
        /// <returns>启用成功返回 true，失败返回 false</returns>
        Task<bool> EnableUserAsync(int userId, string operatorUsername);

        /// <summary>
        /// 管理员重置用户密码
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="newPassword">新密码</param>
        /// <param name="operatorUsername">操作人用户名</param>
        /// <returns>重置成功返回 true，失败返回 false</returns>
        Task<bool> ResetPasswordAsync(int userId, string newPassword, string operatorUsername);

        /// <summary>
        /// 搜索用户
        /// </summary>
        /// <param name="keyword">搜索关键字（用户名、姓名、邮箱）</param>
        /// <returns>匹配的用户列表</returns>
        Task<List<User>> SearchUsersAsync(string keyword);

        /// <summary>
        /// 按角色获取用户列表
        /// </summary>
        /// <param name="role">角色名称</param>
        /// <returns>指定角色的用户列表</returns>
        Task<List<User>> GetUsersByRoleAsync(string role);

        /// <summary>
        /// 检查用户名是否已存在
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="excludeUserId">排除的用户ID（编辑时使用）</param>
        /// <returns>存在返回 true，不存在返回 false</returns>
        Task<bool> IsUsernameExistsAsync(string username, int? excludeUserId = null);
    }
}
