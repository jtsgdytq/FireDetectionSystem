using FireDetectionSystem.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FireDetectionSystem.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel
    /// 负责主窗口的导航、窗口控制和退出登录功能
    /// </summary>
    public class MainViewModel : BindableBase
    {
        private readonly IRegionManager _regionManager;
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _logger;
        private readonly IContainerProvider _containerProvider;

        /// <summary>
        /// 导航命令
        /// 用于切换不同的功能页面
        /// </summary>
        public DelegateCommand<string> NavigateCommand { get; private set; }

        /// <summary>
        /// 最小化窗口命令
        /// </summary>
        public DelegateCommand MinimizeCommand { get; private set; }

        /// <summary>
        /// 最大化/还原窗口命令
        /// </summary>
        public DelegateCommand MaximizeCommand { get; private set; }

        /// <summary>
        /// 退出应用程序命令
        /// </summary>
        public DelegateCommand ExitCommand { get; private set; }

        /// <summary>
        /// 退出登录命令
        /// 返回到登录界面
        /// </summary>
        public DelegateCommand LogoutCommand { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="regionManager">区域管理器，用于页面导航</param>
        /// <param name="dialogService">对话框服务，用于显示登录对话框</param>
        /// <param name="logger">日志服务</param>
        /// <param name="containerProvider">容器提供者，用于解析依赖注入的服务和视图</param>
        public MainViewModel(IRegionManager regionManager, IDialogService dialogService,
            ILoggerService logger, IContainerProvider containerProvider)
        {
            _regionManager = regionManager;
            _dialogService = dialogService;
            _logger = logger;
            _containerProvider = containerProvider;

            NavigateCommand = new DelegateCommand<string>(Navigate);
            MinimizeCommand = new DelegateCommand(OnMinimize);
            MaximizeCommand = new DelegateCommand(OnMaximize);
            ExitCommand = new DelegateCommand(OnExit);
            LogoutCommand = new DelegateCommand(OnLogout);
        }
        /// <summary>
        /// 导航到指定页面根据路径
        /// </summary>
        /// <param name="navigatePath">导航路径（如 "ImageDetection"、"VideoDetection" 等）</param>
        private void Navigate(string navigatePath)
        {
            if (!string.IsNullOrEmpty(navigatePath))
            {
                _regionManager.RequestNavigate("ContentRegion", navigatePath);
            }
        }

        /// <summary>
        /// 最小化窗口
        /// </summary>
        private void OnMinimize()
        {
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化或还原窗口
        /// 如果当前是最大化状态则还原，否则最大化
        /// </summary>
        private void OnMaximize()
        {
            if (Application.Current.MainWindow.WindowState == WindowState.Maximized)
            {
                Application.Current.MainWindow.WindowState = WindowState.Normal;
            }
            else
            {
                Application.Current.MainWindow.WindowState = WindowState.Maximized;
            }
        }

        /// <summary>
        /// 退出应用程序
        /// 直接关闭整个应用
        /// </summary>
        private void OnExit()
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 退出登录
        /// 清除当前用户会话，重启应用程序返回登录界面
        /// </summary>
        private void OnLogout()
        {
            var result = MessageBox.Show(
                "确定要退出登录吗？",
                "退出登录",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _logger.Info($"用户退出登录: {LoginViewModel.CurrentUser?.Username}");

                // 清除当前用户会话
                LoginViewModel.CurrentUser = null;

                // 重启应用程序
                _logger.Info("重启应用程序以返回登录界面");

                // 获取当前应用程序的可执行文件路径
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

                // 启动新的应用程序实例
                System.Diagnostics.Process.Start(exePath);

                // 关闭当前应用程序
                Application.Current.Shutdown();
            }
        }
    }
}
