using FireDetectionSystem.Data;
using FireDetectionSystem.Services;
using FireDetectionSystem.ViewModels;
using FireDetectionSystem.Views;
using Microsoft.EntityFrameworkCore;
using Prism.Ioc;
using System;
using System.IO;
using System.Windows;
using FireDetectionSystem.Core;

namespace FireDetectionSystem
{
    /// <summary>
    /// 应用程序主类
    /// 负责应用程序的启动、服务注册和初始化
    /// </summary>
    public partial class App : PrismApplication
    {
        private ILoggerService? _logger;
        private IConfigurationService? _configService;

        /// <summary>
        /// 创建主窗口
        /// 在应用程序启动时被调用
        /// </summary>
        /// <returns>主窗口实例</returns>
        protected override Window CreateShell()
        {
            return Container.Resolve<Views.MainView>();
        }

        /// <summary>
        /// 注册服务和视图
        /// 配置依赖注入容器
        /// </summary>
        /// <param name="containerRegistry">容器注册器</param>
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册服务（单例模式）
            containerRegistry.RegisterSingleton<IConfigurationService, ConfigurationService>();
            containerRegistry.RegisterSingleton<ILoggerService, LoggerService>();
            containerRegistry.RegisterSingleton<IUserService, UserService>();
            containerRegistry.RegisterSingleton<IDatabaseService, DatabaseService>();
            containerRegistry.RegisterSingleton<IAlarmService, AlarmService>();

            // 注册数据库上下文（作用域模式，每次请求创建新实例）
            // 使用 Register 而不是 RegisterSingleton，避免 DbContext 生命周期问题
            containerRegistry.Register<FireDetectionDbContext>(provider =>
            {
                var configService = provider.Resolve<IConfigurationService>();
                var optionsBuilder = new DbContextOptionsBuilder<FireDetectionDbContext>();
                optionsBuilder.UseSqlite(configService.DatabaseConnectionString);
                return new FireDetectionDbContext(optionsBuilder.Options);
            });

            // 注册对话框视图
            containerRegistry.RegisterDialog<LoginView, LoginViewModel>();

            // 注册导航视图
            containerRegistry.RegisterForNavigation<Views.ImageDetectionView, ViewModels.ImageDetectionViewModel>("ImageDetection");
            containerRegistry.RegisterForNavigation<Views.VideoDetectionView, ViewModels.VideoDetectionViewModel>("VideoDetection");
            containerRegistry.RegisterForNavigation<Views.SettingsView, ViewModels.SettingsViewModel>("Settings");
            containerRegistry.RegisterForNavigation<Views.UserManagementView, ViewModels.UserManagementViewModel>("UserManagement");
            containerRegistry.RegisterForNavigation<Views.CameraDetectionView, ViewModels.CameraDetectionViewModel>("CameraDetection");
        }

        /// <summary>
        /// 应用程序初始化
        /// 在主窗口显示之前执行初始化操作
        /// </summary>
        protected override void OnInitialized()
        {
            try
            {
                // 获取服务实例
                _logger = Container.Resolve<ILoggerService>();
                _configService = Container.Resolve<IConfigurationService>();

                _logger.Info("应用程序启动");

                // 初始化数据库
                InitializeDatabase();

                // 初始化火灾检测模块
                InitializeFireDetectionModule();

                // 显示登录对话框
                ShowLoginDialog();
            }
            catch (Exception ex)
            {
                _logger?.Error($"应用程序初始化失败: {ex.Message}", ex);
                MessageBox.Show($"应用程序初始化失败：{ex.Message}\n\n请检查配置文件和日志。",
                    "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        /// <summary>
        /// 初始化数据库
        /// 创建数据库表结构和默认数据
        /// </summary>
        private void InitializeDatabase()
        {
            try
            {
                _logger?.Info("开始初始化数据库...");

                var dbService = Container.Resolve<IDatabaseService>();
                dbService.InitializeDatabaseAsync().Wait();

                _logger?.Info("数据库初始化完成");
            }
            catch (Exception ex)
            {
                _logger?.Error($"数据库初始化失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 初始化火灾检测模块
        /// 加载 YOLO 模型
        /// </summary>
        private void InitializeFireDetectionModule()
        {
            try
            {
                _logger?.Info("开始加载 YOLO 模型...");

                var modelPath = _configService?.ModelPath ?? "models/best.onnx";

                // 检查模型文件是否存在
                if (!File.Exists(modelPath))
                {
                    _logger?.Warning($"模型文件不存在: {modelPath}");
                    _logger?.Warning("请在配置文件中设置正确的模型路径，或将模型文件放置在指定位置");
                    return;
                }

                // 加载模型
                FireDetectionModule.Initialize(modelPath);

                _logger?.Info("YOLO 模型加载完成");
            }
            catch (Exception ex)
            {
                _logger?.Error($"模型加载失败: {ex.Message}", ex);
                _logger?.Warning("模型加载失败，检测功能将不可用");
            }
        }

        /// <summary>
        /// 显示登录对话框
        /// 用户必须登录才能使用系统
        /// </summary>
        private void ShowLoginDialog()
        {
            var dialog = Container.Resolve<IDialogService>();

            dialog.ShowDialog("LoginView", r =>
            {
                if (r.Result == ButtonResult.OK)
                {
                    // 登录成功，显示主窗口
                    _logger?.Info("用户登录成功，显示主窗口");
                    base.OnInitialized();
                }
                else
                {
                    // 用户取消登录，退出应用程序
                    _logger?.Info("用户取消登录，退出应用程序");
                    Environment.Exit(0);
                    Current.Shutdown();
                }
            });
        }

        /// <summary>
        /// 应用程序退出时的清理操作
        /// </summary>
        /// <param name="e">退出事件参数</param>
        protected override void OnExit(ExitEventArgs e)
        {
            _logger?.Info("应用程序退出");
            base.OnExit(e);
        }
    }
}
