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
    }
}
