using FireDetectionSystem.Models;
using FireDetectionSystem.Services;
using System;
using System.Windows;
using System.Windows.Input;

namespace FireDetectionSystem.ViewModels
{
    /// <summary>
    /// 登录视图模型
    /// 负责处理用户登录逻辑，包括用户名密码验证和登录状态管理
    /// </summary>
    public class LoginViewModel : BindableBase, IDialogAware
    {
        private readonly IUserService _userService;
        private readonly ILoggerService _logger;

        /// <summary>
        /// 当前登录成功的用户
        /// 登录成功后保存用户信息，供其他模块使用
        /// </summary>
        public static User? CurrentUser { get; private set; }

        /// <summary>
        /// 对话框标题
        /// </summary>
        public string Title => "火灾检测系统 - 登录";

        /// <summary>
        /// 对话框关闭监听器
        /// 用于通知 Prism 关闭对话框
        /// </summary>
        public DialogCloseListener RequestClose { get; set; }

        #region 绑定属性

        private string _username = string.Empty;
        /// <summary>
        /// 用户名输入框绑定属性
        /// </summary>
        public string Username
        {
            get => _username;
            set { _username = value; RaisePropertyChanged(); LoginCommand.RaiseCanExecuteChanged(); }
        }

        private string _password = string.Empty;
        /// <summary>
        /// 密码输入框绑定属性
        /// </summary>
        public string Password
        {
            get => _password;
            set { _password = value; RaisePropertyChanged(); LoginCommand.RaiseCanExecuteChanged(); }
        }

        private bool _isLoading;
        /// <summary>
        /// 是否正在登录中
        /// 用于控制加载动画和禁用按钮
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; RaisePropertyChanged(); }
        }

        private string _errorMessage = string.Empty;
        /// <summary>
        /// 错误提示信息
        /// 登录失败时显示错误原因
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; RaisePropertyChanged(); }
        }

        private bool _hasError;
        /// <summary>
        /// 是否有错误信息
        /// 控制错误提示区域的显示/隐藏
        /// </summary>
        public bool HasError
        {
            get => _hasError;
            set { _hasError = value; RaisePropertyChanged(); }
        }

        #endregion

        #region 命令

        /// <summary>
        /// 登录命令
        /// 点击登录按钮时执行
        /// </summary>
        public DelegateCommand LoginCommand { get; set; }

        /// <summary>
        /// 关闭命令
        /// 点击取消/关闭按钮时执行
        /// </summary>
        public DelegateCommand CloseCommand { get; set; }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="userService">用户服务，用于验证用户名和密码</param>
        /// <param name="logger">日志服务</param>
        public LoginViewModel(IUserService userService, ILoggerService logger)
        {
            _userService = userService;
            _logger = logger;

            // 初始化命令
            // CanExecute 方法确保用户名和密码不为空时才能点击登录
            LoginCommand = new DelegateCommand(ExecuteLogin, CanLogin);
            CloseCommand = new DelegateCommand(() =>
                RequestClose.Invoke(new DialogResult(ButtonResult.No)));
        }

        /// <summary>
        /// 判断是否可以执行登录
        /// 用户名和密码都不为空时才允许登录
        /// </summary>
        /// <returns>是否可以登录</returns>
        private bool CanLogin()
        {
            return !string.IsNullOrWhiteSpace(Username) &&
                   !string.IsNullOrWhiteSpace(Password) &&
                   !IsLoading;
        }

        /// <summary>
        /// 执行登录操作
        /// 验证用户名和密码，成功后关闭对话框进入主界面
        /// </summary>
        private async void ExecuteLogin()
        {
            try
            {
                // 清除之前的错误信息
                ClearError();

                // 显示加载状态
                IsLoading = true;
                LoginCommand.RaiseCanExecuteChanged();

                _logger.Info($"用户尝试登录: {Username}");

                // 调用用户服务验证用户名和密码
                var user = await _userService.LoginAsync(Username, Password);

                if (user != null)
                {
                    // 登录成功，保存当前用户信息
                    CurrentUser = user;
                    _logger.Info($"登录成功: {Username}，角色: {user.Role}");

                    // 关闭对话框，返回 OK 结果
                    RequestClose.Invoke(new DialogResult(ButtonResult.OK));
                }
                else
                {
                    // 登录失败，显示错误信息
                    ShowError("用户名或密码错误，请重试");
                    _logger.Warning($"登录失败: {Username}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"登录异常: {ex.Message}", ex);
                ShowError($"登录时发生错误：{ex.Message}");
            }
            finally
            {
                // 无论成功失败，都要关闭加载状态
                IsLoading = false;
                LoginCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 显示错误信息
        /// </summary>
        /// <param name="message">错误消息</param>
        private void ShowError(string message)
        {
            ErrorMessage = message;
            HasError = true;
        }

        /// <summary>
        /// 清除错误信息
        /// </summary>
        private void ClearError()
        {
            ErrorMessage = string.Empty;
            HasError = false;
        }

        #region IDialogAware 接口实现

        /// <summary>
        /// 判断是否可以关闭对话框
        /// </summary>
        /// <returns>始终返回 true，允许关闭</returns>
        public bool CanCloseDialog() => true;

        /// <summary>
        /// 对话框关闭时的回调
        /// </summary>
        public void OnDialogClosed()
        {
            _logger.Debug("登录对话框已关闭");
        }

        /// <summary>
        /// 对话框打开时的回调
        /// 可以通过参数传递初始数据
        /// </summary>
        /// <param name="parameters">对话框参数</param>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            _logger.Debug("登录对话框已打开");
        }

        #endregion
    }
}
