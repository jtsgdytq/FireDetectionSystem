using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FireDetectionSystem.Core;
using FireDetectionSystem.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace FireDetectionSystem.ViewModels
{
    /// <summary>
    /// 邮件收件人条目（仅 ViewModel 层使用，非数据库实体）
    /// </summary>
    public class UserEmailItem : BindableBase
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Email { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 是否可以勾选（有邮箱才可以选）
        /// </summary>
        public bool CanSelect => !string.IsNullOrWhiteSpace(Email);

        /// <summary>
        /// 无邮箱（用于控制"无邮箱"标签的可见性）
        /// </summary>
        public bool HasNoEmail => string.IsNullOrWhiteSpace(Email);

        /// <summary>
        /// 显示名称：用户名 + 姓名
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(FullName)
            ? Username
            : $"{Username}（{FullName}）";
    }

    /// <summary>
    /// 设置页面 ViewModel
    /// 负责 ONNX 模型切换、阈值调节、报警开关、邮件配置与收件人管理
    /// </summary>
    public class SettingsViewModel : BindableBase, INavigationAware
    {
        private readonly IConfigurationService _configService;
        private readonly IUserService _userService;
        private readonly ILoggerService _logger;

        #region 模型设置属性

        private string _modelPath = string.Empty;
        public string ModelPath
        {
            get => _modelPath;
            set => SetProperty(ref _modelPath, value);
        }

        private float _confidenceThreshold = 0.5f;
        public float ConfidenceThreshold
        {
            get => _confidenceThreshold;
            set
            {
                SetProperty(ref _confidenceThreshold, value);
                RaisePropertyChanged(nameof(ConfidenceDisplay));
            }
        }

        /// <summary>置信度阈值的百分比显示（实时预览）</summary>
        public string ConfidenceDisplay => $"{ConfidenceThreshold:P0}";

        private float _alertThreshold = 0.7f;
        public float AlertThreshold
        {
            get => _alertThreshold;
            set
            {
                SetProperty(ref _alertThreshold, value);
                RaisePropertyChanged(nameof(AlertThresholdDisplay));
            }
        }

        /// <summary>报警阈值的百分比显示（实时预览）</summary>
        public string AlertThresholdDisplay => $"{AlertThreshold:P0}";

        #endregion

        #region 报警开关属性

        private bool _enableSound;
        public bool EnableSound
        {
            get => _enableSound;
            set => SetProperty(ref _enableSound, value);
        }

        private bool _enableDesktopNotification;
        public bool EnableDesktopNotification
        {
            get => _enableDesktopNotification;
            set => SetProperty(ref _enableDesktopNotification, value);
        }

        private bool _enableEmail;
        public bool EnableEmail
        {
            get => _enableEmail;
            set
            {
                SetProperty(ref _enableEmail, value);
                RaisePropertyChanged(nameof(IsEmailSectionVisible));
            }
        }

        /// <summary>邮件配置区域可见性（绑定到 EnableEmail）</summary>
        public bool IsEmailSectionVisible => EnableEmail;

        #endregion

        #region SMTP 邮件配置属性

        private string _smtpServer = string.Empty;
        public string SmtpServer
        {
            get => _smtpServer;
            set => SetProperty(ref _smtpServer, value);
        }

        private int _smtpPort = 587;
        public int SmtpPort
        {
            get => _smtpPort;
            set => SetProperty(ref _smtpPort, value);
        }

        private bool _smtpUseSsl = true;
        public bool SmtpUseSsl
        {
            get => _smtpUseSsl;
            set => SetProperty(ref _smtpUseSsl, value);
        }

        private string _smtpUsername = string.Empty;
        public string SmtpUsername
        {
            get => _smtpUsername;
            set => SetProperty(ref _smtpUsername, value);
        }

        private string _smtpPassword = string.Empty;
        public string SmtpPassword
        {
            get => _smtpPassword;
            set => SetProperty(ref _smtpPassword, value);
        }

        private string _fromAddress = string.Empty;
        public string FromAddress
        {
            get => _fromAddress;
            set => SetProperty(ref _fromAddress, value);
        }

        private string _fromName = "火灾检测系统";
        public string FromName
        {
            get => _fromName;
            set => SetProperty(ref _fromName, value);
        }

        #endregion

        #region 收件人列表属性

        private ObservableCollection<UserEmailItem> _emailRecipients = new();
        public ObservableCollection<UserEmailItem> EmailRecipients
        {
            get => _emailRecipients;
            set => SetProperty(ref _emailRecipients, value);
        }

        #endregion

        #region 状态属性

        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            set
            {
                SetProperty(ref _isSaving, value);
                RaisePropertyChanged(nameof(CanSave));
            }
        }

        /// <summary>是否可以保存（未在保存中）</summary>
        public bool CanSave => !IsSaving;

        private string _saveStatusMessage = string.Empty;
        public string SaveStatusMessage
        {
            get => _saveStatusMessage;
            set => SetProperty(ref _saveStatusMessage, value);
        }

        private bool _showSaveStatus;
        public bool ShowSaveStatus
        {
            get => _showSaveStatus;
            set => SetProperty(ref _showSaveStatus, value);
        }

        private bool _saveSuccess;
        public bool SaveSuccess
        {
            get => _saveSuccess;
            set => SetProperty(ref _saveSuccess, value);
        }

        #endregion

        #region 命令

        public DelegateCommand BrowseModelFileCommand { get; }
        public DelegateCommand SelectAllRecipientsCommand { get; }
        public DelegateCommand UnselectAllRecipientsCommand { get; }
        public DelegateCommand SaveSettingsCommand { get; }

        #endregion

        public SettingsViewModel(
            IConfigurationService configService,
            IUserService userService,
            ILoggerService logger)
        {
            _configService = configService;
            _userService = userService;
            _logger = logger;

            BrowseModelFileCommand = new DelegateCommand(BrowseModelFile);
            SelectAllRecipientsCommand = new DelegateCommand(SelectAllRecipients);
            UnselectAllRecipientsCommand = new DelegateCommand(UnselectAllRecipients);
            SaveSettingsCommand = new DelegateCommand(async () => await SaveSettingsAsync());
        }

        /// <summary>
        /// 打开文件对话框选择 ONNX 模型文件
        /// </summary>
        private void BrowseModelFile()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择 YOLO 模型文件",
                Filter = "ONNX 模型文件 (*.onnx)|*.onnx|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                ModelPath = dialog.FileName;
            }
        }

        /// <summary>
        /// 全选有邮箱的收件人
        /// </summary>
        private void SelectAllRecipients()
        {
            foreach (var item in EmailRecipients.Where(u => u.CanSelect))
                item.IsSelected = true;
        }

        /// <summary>
        /// 取消全选收件人
        /// </summary>
        private void UnselectAllRecipients()
        {
            foreach (var item in EmailRecipients)
                item.IsSelected = false;
        }

        /// <summary>
        /// 保存所有设置到 appsettings.json（一次原子写入）
        /// </summary>
        private async Task SaveSettingsAsync()
        {
            try
            {
                IsSaving = true;

                var selectedEmails = EmailRecipients
                    .Where(u => u.IsSelected && !string.IsNullOrWhiteSpace(u.Email))
                    .Select(u => u.Email!)
                    .ToArray();

                // 记录保存前的模型路径，用于判断是否需要重载模型
                var previousModelPath = _configService.ModelPath;

                // 一次性原子写入所有配置
                _configService.SaveAllSettings(
                    ModelPath, ConfidenceThreshold,
                    EnableSound, EnableDesktopNotification, EnableEmail, AlertThreshold,
                    selectedEmails,
                    SmtpServer, SmtpPort, SmtpUseSsl,
                    SmtpUsername, SmtpPassword,
                    FromAddress, FromName);

                // 模型路径发生变化时重新加载模型
                if (!string.Equals(ModelPath, previousModelPath, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(ModelPath))
                {
                    FireDetectionModule.Reinitialize(ModelPath);
                    _logger.Info($"YOLO 模型已重新加载：{ModelPath}");
                }
                else if (!FireDetectionModule.IsLoaded && File.Exists(ModelPath))
                {
                    // 首次启动时模型未加载的情况
                    FireDetectionModule.Initialize(ModelPath);
                    _logger.Info($"YOLO 模型已加载：{ModelPath}");
                }

                _logger.Info("设置已保存");
                SaveSuccess = true;
                SaveStatusMessage = "设置已保存";
                ShowSaveStatus = true;

                await Task.Delay(3000);
                ShowSaveStatus = false;
            }
            catch (Exception ex)
            {
                _logger.Error($"保存设置失败: {ex.Message}");
                SaveSuccess = false;
                SaveStatusMessage = $"保存失败：{ex.Message}";
                ShowSaveStatus = true;

                await Task.Delay(4000);
                ShowSaveStatus = false;
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>
        /// 从配置服务读取当前配置填充 ViewModel 属性
        /// </summary>
        private void LoadSettingsFromConfig()
        {
            ModelPath = _configService.ModelPath;
            ConfidenceThreshold = _configService.ConfidenceThreshold;
            AlertThreshold = _configService.AlertThreshold;
            EnableSound = _configService.EnableSoundAlarm;
            EnableDesktopNotification = _configService.EnableDesktopNotification;
            EnableEmail = _configService.EnableEmailAlarm;

            SmtpServer = _configService.GetValue("EmailSettings:SmtpServer", string.Empty);
            SmtpPort = _configService.GetValue("EmailSettings:SmtpPort", 587);
            SmtpUseSsl = _configService.GetValue("EmailSettings:UseSsl", true);
            SmtpUsername = _configService.GetValue("EmailSettings:Username", string.Empty);
            SmtpPassword = _configService.GetValue("EmailSettings:Password", string.Empty);
            FromAddress = _configService.GetValue("EmailSettings:FromAddress", string.Empty);
            FromName = _configService.GetValue("EmailSettings:FromName", "火灾检测系统");
        }

        /// <summary>
        /// 从数据库加载用户列表，并根据现有收件人配置预勾选
        /// </summary>
        private async Task LoadEmailRecipientsAsync()
        {
            try
            {
                var users = await _userService.GetAllUsersAsync();

                // 读取当前已配置的收件人邮箱列表
                var currentRecipients = _configService.Configuration
                    .GetSection("AlarmSettings:EmailRecipients")
                    .Get<string[]>() ?? Array.Empty<string>();

                var items = users.Select(u => new UserEmailItem
                {
                    UserId = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    Email = u.Email,
                    IsSelected = !string.IsNullOrWhiteSpace(u.Email)
                                 && currentRecipients.Contains(u.Email, StringComparer.OrdinalIgnoreCase)
                }).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    EmailRecipients = new ObservableCollection<UserEmailItem>(items);
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"加载用户收件人列表失败: {ex.Message}", ex);
            }
        }

        #region INavigationAware

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            LoadSettingsFromConfig();
            _ = LoadEmailRecipientsAsync();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        #endregion
    }
}
