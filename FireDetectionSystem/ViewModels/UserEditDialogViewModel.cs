using System.Collections.Generic;
using System.Windows;
using FireDetectionSystem.Models;
using FireDetectionSystem.Services;

namespace FireDetectionSystem.ViewModels
{
    /// <summary>
    /// 用户编辑对话框 ViewModel
    /// 支持添加和编辑两种模式
    /// 添加模式：需要输入用户名和密码
    /// 编辑模式：不显示密码字段，只能修改其他信息
    /// </summary>
    public class UserEditDialogViewModel : BindableBase, IDialogAware
    {
        // 依赖注入的服务
        private readonly IUserService _userService;  // 用户服务，处理用户数据操作
        private readonly ILoggerService _logger;     // 日志服务，记录操作日志

        /// <summary>
        /// 对话框标题
        /// 添加模式显示"添加用户"，编辑模式显示"编辑用户"
        /// </summary>
        private string _title = "添加用户";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// 对话框模式
        /// "Add" = 添加模式，"Edit" = 编辑模式
        /// </summary>
        private string _mode = "Add";

        /// <summary>
        /// 用户ID（仅编辑模式使用）
        /// </summary>
        private int _userId;

        /// <summary>
        /// 用户名
        /// 必填字段
        /// </summary>
        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        /// <summary>
        /// 密码
        /// 仅在添加模式下显示和必填
        /// </summary>
        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        /// <summary>
        /// 真实姓名
        /// 可选字段
        /// </summary>
        private string _fullName = string.Empty;
        public string FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        /// <summary>
        /// 邮箱地址
        /// 可选字段
        /// </summary>
        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        /// <summary>
        /// 用户角色
        /// 可选值：Admin、Operator、Viewer
        /// 默认为 Viewer
        /// </summary>
        private string _role = "Viewer";
        public string Role
        {
            get => _role;
            set => SetProperty(ref _role, value);
        }

        /// <summary>
        /// 账户状态
        /// true = 激活，false = 禁用
        /// 默认为激活状态
        /// </summary>
        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        /// 密码字段是否可见
        /// 添加模式为 true，编辑模式为 false
        /// </summary>
        private bool _isPasswordVisible = true;
        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set => SetProperty(ref _isPasswordVisible, value);
        }

        /// <summary>
        /// 可选角色列表
        /// 用于 ComboBox 绑定
        /// </summary>
        public List<string> AvailableRoles { get; } = new List<string> { "Admin", "Operator", "Viewer" };

        /// <summary>
        /// 命令：保存用户信息
        /// </summary>
        public DelegateCommand SaveCommand { get; }

        /// <summary>
        /// 命令：取消操作
        /// </summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>
        /// 对话框关闭请求委托
        /// Prism 对话框服务使用此委托关闭对话框
        /// </summary>
        public DialogCloseListener RequestClose { get; set; }

        /// <summary>
        /// 构造函数
        /// 初始化服务和命令
        /// </summary>
        /// <param name="userService">用户服务</param>
        /// <param name="logger">日志服务</param>
        public UserEditDialogViewModel(IUserService userService, ILoggerService logger)
        {
            _userService = userService;
            _logger = logger;

            // 初始化命令
            SaveCommand = new DelegateCommand(async () => await SaveAsync());
            CancelCommand = new DelegateCommand(Cancel);
        }

        /// <summary>
        /// 判断对话框是否可以关闭
        /// 始终返回 true
        /// </summary>
        public bool CanCloseDialog() => true;

        /// <summary>
        /// 对话框关闭时触发
        /// 当前无需执行任何操作
        /// </summary>
        public void OnDialogClosed() { }

        /// <summary>
        /// 对话框打开时触发
        /// 根据传入的参数初始化对话框状态
        /// </summary>
        /// <param name="parameters">对话框参数</param>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            _mode = parameters.GetValue<string>("Mode");

            if (_mode == "Edit")
            {
                // 编辑模式：加载用户信息，隐藏密码字段
                Title = "编辑用户";
                IsPasswordVisible = false;

                var user = parameters.GetValue<User>("User");
                if (user != null)
                {
                    _userId = user.Id;
                    Username = user.Username;
                    FullName = user.FullName ?? string.Empty;
                    Email = user.Email ?? string.Empty;
                    Role = user.Role;
                    IsActive = user.IsActive;
                }
            }
            else
            {
                // 添加模式：显示密码字段
                Title = "添加用户";
                IsPasswordVisible = true;
            }
        }

        /// <summary>
        /// 保存用户信息
        /// 添加模式：调用注册接口创建新用户
        /// 编辑模式：调用更新接口修改用户信息
        /// </summary>
        private async System.Threading.Tasks.Task SaveAsync()
        {
            try
            {
                // 验证用户名
                if (string.IsNullOrWhiteSpace(Username))
                {
                    MessageBox.Show("请输入用户名", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 添加模式下验证密码
                if (_mode == "Add" && string.IsNullOrWhiteSpace(Password))
                {
                    MessageBox.Show("请输入密码", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_mode == "Add")
                {
                    // 添加用户
                    var user = await _userService.RegisterAsync(Username, Password, FullName, Email, Role);
                    if (user != null)
                    {
                        // 如果设置为禁用状态，需要额外调用禁用接口
                        if (!IsActive)
                        {
                            await _userService.DisableUserAsync(user.Id, "admin");
                        }

                        _logger.Info($"用户管理 - 添加用户成功: {Username}");
                        RequestClose.Invoke(new DialogResult(ButtonResult.OK));
                    }
                    else
                    {
                        MessageBox.Show("添加用户失败，用户名可能已存在", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // 编辑用户
                    var user = await _userService.GetUserByIdAsync(_userId);
                    if (user != null)
                    {
                        // 更新用户信息
                        user.Username = Username;
                        user.FullName = string.IsNullOrWhiteSpace(FullName) ? null : FullName;
                        user.Email = string.IsNullOrWhiteSpace(Email) ? null : Email;
                        user.Role = Role;
                        user.IsActive = IsActive;

                        var success = await _userService.UpdateUserAsync(user, "admin");
                        if (success)
                        {
                            _logger.Info($"用户管理 - 编辑用户成功: {Username}");
                            RequestClose.Invoke(new DialogResult(ButtonResult.OK));
                        }
                        else
                        {
                            MessageBox.Show("更新用户失败，用户名可能已存在", "错误",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 保存用户失败: {ex.Message}", ex);
                MessageBox.Show($"保存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消操作
        /// 关闭对话框，不保存任何更改
        /// </summary>
        private void Cancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }
    }
}
