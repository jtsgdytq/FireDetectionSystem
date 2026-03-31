using System.Windows;
using FireDetectionSystem.Services;

namespace FireDetectionSystem.ViewModels
{
    /// <summary>
    /// 密码重置对话框 ViewModel
    /// 管理员可以为任何用户重置密码，无需验证旧密码
    /// 要求输入新密码并确认，密码长度至少6位
    /// </summary>
    public class PasswordResetDialogViewModel : BindableBase, IDialogAware
    {
        // 依赖注入的服务
        private readonly IUserService _userService;  // 用户服务，处理密码重置操作
        private readonly ILoggerService _logger;     // 日志服务，记录操作日志

        /// <summary>
        /// 目标用户ID
        /// </summary>
        private int _userId;

        /// <summary>
        /// 目标用户名
        /// 用于显示在对话框标题中
        /// </summary>
        private string _targetUsername = string.Empty;

        /// <summary>
        /// 操作人用户名
        /// 用于审计日志记录
        /// </summary>
        private string _operatorUsername = string.Empty;

        /// <summary>
        /// 对话框标题
        /// 格式："重置密码 - {用户名}"
        /// </summary>
        private string _title = "重置密码";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// 新密码
        /// 必填，长度至少6位
        /// </summary>
        private string _newPassword = string.Empty;
        public string NewPassword
        {
            get => _newPassword;
            set => SetProperty(ref _newPassword, value);
        }

        /// <summary>
        /// 确认密码
        /// 必须与新密码一致
        /// </summary>
        private string _confirmPassword = string.Empty;
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        /// <summary>
        /// 命令：重置密码
        /// </summary>
        public DelegateCommand ResetCommand { get; }

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
        public PasswordResetDialogViewModel(IUserService userService, ILoggerService logger)
        {
            _userService = userService;
            _logger = logger;

            // 初始化命令
            ResetCommand = new DelegateCommand(async () => await ResetAsync());
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
        /// 从参数中获取目标用户信息和操作人信息
        /// </summary>
        /// <param name="parameters">对话框参数</param>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            _userId = parameters.GetValue<int>("UserId");
            _targetUsername = parameters.GetValue<string>("Username");
            _operatorUsername = parameters.GetValue<string>("OperatorUsername");
            Title = $"重置密码 - {_targetUsername}";
        }

        /// <summary>
        /// 重置密码
        /// 验证输入后调用用户服务重置密码
        /// </summary>
        private async System.Threading.Tasks.Task ResetAsync()
        {
            // 验证新密码不为空
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                MessageBox.Show("请输入新密码", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 验证密码长度
            if (NewPassword.Length < 6)
            {
                MessageBox.Show("密码长度不能少于6位", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 验证两次密码输入一致
            if (NewPassword != ConfirmPassword)
            {
                MessageBox.Show("两次输入的密码不一致", "验证失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 调用用户服务重置密码
                var success = await _userService.ResetPasswordAsync(_userId, NewPassword, _operatorUsername);
                if (success)
                {
                    MessageBox.Show($"用户 '{_targetUsername}' 的密码已重置", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    RequestClose.Invoke(new DialogResult(ButtonResult.OK));
                }
                else
                {
                    MessageBox.Show("重置密码失败，请查看日志", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 重置密码异常: {ex.Message}", ex);
                MessageBox.Show($"重置密码失败：{ex.Message}", "错误",
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
