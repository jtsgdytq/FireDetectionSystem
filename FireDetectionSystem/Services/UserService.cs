using System;
using System.Linq;
using System.Threading.Tasks;
using FireDetectionSystem.Data;
using FireDetectionSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace FireDetectionSystem.Services
{
    /// <summary>
    /// 用户服务实现类
    /// 提供用户认证和管理功能
    /// </summary>
    public class UserService : IUserService
    {
        private readonly ILoggerService _logger;
        private readonly IConfigurationService _configService;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志服务</param>
        /// <param name="configService">配置服务</param>
        public UserService(ILoggerService logger, IConfigurationService configService)
        {
            _logger = logger;
            _configService = configService;
        }

        /// <summary>
        /// 创建数据库上下文实例
        /// 每次操作创建新的 DbContext，避免生命周期冲突
        /// </summary>
        /// <returns>数据库上下文实例</returns>
        private FireDetectionDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<FireDetectionDbContext>();
            optionsBuilder.UseSqlite(_configService.DatabaseConnectionString);
            return new FireDetectionDbContext(optionsBuilder.Options);
        }

        /// <summary>
        /// 用户登录
        /// 验证用户名和密码，成功后更新最后登录时间
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码（明文）</param>
        /// <returns>登录成功返回用户对象，失败返回 null</returns>
        public async Task<User?> LoginAsync(string username, string password)
        {
            try
            {
                using var dbContext = CreateDbContext();

                // 查找用户
                var user = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (user == null)
                {
                    _logger.Warning($"登录失败：用户不存在 - {username}");
                    return null;
                }

                // 检查账户是否激活
                if (!user.IsActive)
                {
                    _logger.Warning($"登录失败：账户已禁用 - {username}");
                    return null;
                }

                // 验证密码
                if (!VerifyPassword(password, user.PasswordHash))
                {
                    _logger.Warning($"登录失败：密码错误 - {username}");
                    return null;
                }

                // 更新最后登录时间
                await UpdateLastLoginTimeAsync(user.Id);

                _logger.Info($"用户登录成功: {username}");
                return user;
            }
            catch (Exception ex)
            {
                _logger.Error($"登录异常: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 用户注册
        /// 创建新用户账户，密码使用 BCrypt 加密
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码（明文）</param>
        /// <param name="fullName">真实姓名</param>
        /// <param name="email">邮箱</param>
        /// <param name="role">角色</param>
        /// <returns>注册成功返回用户对象，失败返回 null</returns>
        public async Task<User?> RegisterAsync(string username, string password,
            string? fullName = null, string? email = null, string role = "Viewer")
        {
            try
            {
                using var dbContext = CreateDbContext();

                // 检查用户名是否已存在
                var existingUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (existingUser != null)
                {
                    _logger.Warning($"注册失败：用户名已存在 - {username}");
                    return null;
                }

                // 创建新用户
                var user = new User
                {
                    Username = username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    FullName = fullName,
                    Email = email,
                    Role = role,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();

                _logger.Info($"用户注册成功: {username}");
                return user;
            }
            catch (Exception ex)
            {
                _logger.Error($"注册异常: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 根据用户名获取用户
        /// </summary>
        /// <param name="username">用户名</param>
        /// <returns>用户对象，不存在返回 null</returns>
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            try
            {
                using var dbContext = CreateDbContext();
                return await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Username == username);
            }
            catch (Exception ex)
            {
                _logger.Error($"获取用户失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 根据ID获取用户
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>用户对象，不存在返回 null</returns>
        public async Task<User?> GetUserByIdAsync(int userId)
        {
            try
            {
                using var dbContext = CreateDbContext();
                return await dbContext.Users.FindAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.Error($"获取用户失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 更新用户最后登录时间
        /// </summary>
        /// <param name="userId">用户ID</param>
        public async Task UpdateLastLoginTimeAsync(int userId)
        {
            try
            {
                using var dbContext = CreateDbContext();
                var user = await dbContext.Users.FindAsync(userId);
                if (user != null)
                {
                    user.LastLoginAt = DateTime.Now;
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"更新登录时间失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 修改密码
        /// 验证旧密码后更新为新密码
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="oldPassword">旧密码</param>
        /// <param name="newPassword">新密码</param>
        /// <returns>修改成功返回 true，失败返回 false</returns>
        public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            try
            {
                using var dbContext = CreateDbContext();
                var user = await dbContext.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.Warning($"修改密码失败：用户不存在 - ID: {userId}");
                    return false;
                }

                // 验证旧密码
                if (!VerifyPassword(oldPassword, user.PasswordHash))
                {
                    _logger.Warning($"修改密码失败：旧密码错误 - {user.Username}");
                    return false;
                }

                // 更新密码
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await dbContext.SaveChangesAsync();

                _logger.Info($"密码修改成功: {user.Username}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"修改密码异常: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 验证密码
        /// 使用 BCrypt 验证明文密码和哈希值是否匹配
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="passwordHash">密码哈希值</param>
        /// <returns>密码正确返回 true，否则返回 false</returns>
        public bool VerifyPassword(string password, string passwordHash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, passwordHash);
            }
            catch (Exception ex)
            {
                _logger.Error($"密码验证异常: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 获取所有用户列表
        /// </summary>
        /// <returns>用户列表</returns>
        public async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                using var dbContext = CreateDbContext();
                var users = await dbContext.Users
                    .OrderBy(u => u.Username)
                    .ToListAsync();

                _logger.Info($"用户管理 - 加载用户列表，共 {users.Count} 个用户");
                return users;
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 加载用户列表失败: {ex.Message}", ex);
                return new List<User>();
            }
        }

        /// <summary>
        /// 更新用户信息
        /// </summary>
        /// <param name="user">用户对象</param>
        /// <param name="operatorUsername">操作人用户名</param>
        /// <returns>更新成功返回 true，失败返回 false</returns>
        public async Task<bool> UpdateUserAsync(User user, string operatorUsername)
        {
            try
            {
                using var dbContext = CreateDbContext();

                // 检查用户名是否与其他用户冲突
                var existingUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Username == user.Username && u.Id != user.Id);

                if (existingUser != null)
                {
                    _logger.Warning($"用户管理 - 更新用户失败: 用户名已存在 - {user.Username}, 操作人={operatorUsername}");
                    return false;
                }

                dbContext.Users.Update(user);
                await dbContext.SaveChangesAsync();

                _logger.Info($"用户管理 - 更新用户信息成功: username={user.Username}, role={user.Role}, 操作人={operatorUsername}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 更新用户失败: {ex.Message}, 操作人={operatorUsername}", ex);
                return false;
            }
        }

        /// <summary>
        /// 删除用户（物理删除）
        /// 不允许删除管理员账户
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="operatorUsername">操作人用户名</param>
        /// <returns>删除成功返回 true，失败返回 false</returns>
        public async Task<bool> DeleteUserAsync(int userId, string operatorUsername)
        {
            try
            {
                using var dbContext = CreateDbContext();
                var user = await dbContext.Users.FindAsync(userId);

                if (user == null)
                {
                    _logger.Warning($"用户管理 - 删除用户失败: 用户不存在 - ID={userId}, 操作人={operatorUsername}");
                    return false;
                }

                // 不允许删除管理员账户
                if (user.Role == "Admin")
                {
                    _logger.Warning($"用户管理 - 删除用户失败: 不能删除管理员账户 - {user.Username}, 操作人={operatorUsername}");
                    return false;
                }

                dbContext.Users.Remove(user);
                await dbContext.SaveChangesAsync();

                _logger.Warning($"用户管理 - 删除用户: username={user.Username}, 操作人={operatorUsername}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 删除用户失败: {ex.Message}, 操作人={operatorUsername}", ex);
                return false;
            }
        }

        /// <summary>
        /// 禁用用户账户
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="operatorUsername">操作人用户名</param>
        /// <returns>禁用成功返回 true，失败返回 false</returns>
        public async Task<bool> DisableUserAsync(int userId, string operatorUsername)
        {
            try
            {
                using var dbContext = CreateDbContext();
                var user = await dbContext.Users.FindAsync(userId);

                if (user == null)
                {
                    _logger.Warning($"用户管理 - 禁用用户失败: 用户不存在 - ID={userId}, 操作人={operatorUsername}");
                    return false;
                }

                user.IsActive = false;
                await dbContext.SaveChangesAsync();

                _logger.Warning($"用户管理 - 禁用用户账户: username={user.Username}, 操作人={operatorUsername}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 禁用用户失败: {ex.Message}, 操作人={operatorUsername}", ex);
                return false;
            }
        }

        /// <summary>
        /// 启用用户账户
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="operatorUsername">操作人用户名</param>
        /// <returns>启用成功返回 true，失败返回 false</returns>
        public async Task<bool> EnableUserAsync(int userId, string operatorUsername)
        {
            try
            {
                using var dbContext = CreateDbContext();
                var user = await dbContext.Users.FindAsync(userId);

                if (user == null)
                {
                    _logger.Warning($"用户管理 - 启用用户失败: 用户不存在 - ID={userId}, 操作人={operatorUsername}");
                    return false;
                }

                user.IsActive = true;
                await dbContext.SaveChangesAsync();

                _logger.Info($"用户管理 - 启用用户账户: username={user.Username}, 操作人={operatorUsername}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 启用用户失败: {ex.Message}, 操作人={operatorUsername}", ex);
                return false;
            }
        }

        /// <summary>
        /// 管理员重置用户密码
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="newPassword">新密码</param>
        /// <param name="operatorUsername">操作人用户名</param>
        /// <returns>重置成功返回 true，失败返回 false</returns>
        public async Task<bool> ResetPasswordAsync(int userId, string newPassword, string operatorUsername)
        {
            try
            {
                using var dbContext = CreateDbContext();
                var user = await dbContext.Users.FindAsync(userId);

                if (user == null)
                {
                    _logger.Warning($"用户管理 - 重置密码失败: 用户不存在 - ID={userId}, 操作人={operatorUsername}");
                    return false;
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                await dbContext.SaveChangesAsync();

                _logger.Warning($"用户管理 - 重置用户密码: username={user.Username}, 操作人={operatorUsername}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 重置密码失败: {ex.Message}, 操作人={operatorUsername}", ex);
                return false;
            }
        }

        /// <summary>
        /// 搜索用户（按用户名、姓名、邮箱模糊匹配）
        /// </summary>
        /// <param name="keyword">搜索关键字</param>
        /// <returns>匹配的用户列表</returns>
        public async Task<List<User>> SearchUsersAsync(string keyword)
        {
            try
            {
                using var dbContext = CreateDbContext();
                var lower = keyword.ToLower();
                return await dbContext.Users
                    .Where(u => u.Username.ToLower().Contains(lower)
                             || (u.FullName != null && u.FullName.ToLower().Contains(lower))
                             || (u.Email != null && u.Email.ToLower().Contains(lower)))
                    .OrderBy(u => u.Username)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 搜索用户失败: {ex.Message}", ex);
                return new List<User>();
            }
        }

        /// <summary>
        /// 按角色获取用户列表
        /// </summary>
        /// <param name="role">角色名称</param>
        /// <returns>指定角色的用户列表</returns>
        public async Task<List<User>> GetUsersByRoleAsync(string role)
        {
            try
            {
                using var dbContext = CreateDbContext();
                return await dbContext.Users
                    .Where(u => u.Role == role)
                    .OrderBy(u => u.Username)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 按角色查询用户失败: {ex.Message}", ex);
                return new List<User>();
            }
        }

        /// <summary>
        /// 检查用户名是否已存在
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="excludeUserId">排除的用户ID（编辑时使用）</param>
        /// <returns>存在返回 true，不存在返回 false</returns>
        public async Task<bool> IsUsernameExistsAsync(string username, int? excludeUserId = null)
        {
            try
            {
                using var dbContext = CreateDbContext();
                return await dbContext.Users
                    .AnyAsync(u => u.Username == username && (excludeUserId == null || u.Id != excludeUserId));
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 检查用户名失败: {ex.Message}", ex);
                return false;
            }
        }
    }
}
