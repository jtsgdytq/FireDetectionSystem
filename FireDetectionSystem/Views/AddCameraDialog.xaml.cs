using FireDetectionSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace FireDetectionSystem.Views
{
    /// <summary>
    /// 统一添加摄像头对话框。
    /// 支持三种来源类型：模拟文件、本地设备、远程 RTSP，用户选择类型后填写对应参数。
    /// </summary>
    public partial class AddCameraDialog : Window
    {
        // ── 对话框返回值（DialogResult=true 时有效） ──────────────────────────

        /// <summary>
        /// 用户选择的摄像头来源类型。
        /// </summary>
        public CameraDetectionViewModel.CameraSourceType SourceType { get; private set; }

        /// <summary>
        /// 最终使用的摄像头名称（空时由对话框自动命名）。
        /// </summary>
        public string CameraName { get; private set; }

        /// <summary>
        /// 模拟摄像头的视频文件路径（SourceType=SimulatedFile 时有效）。
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// 本地摄像头的设备索引（SourceType=LocalDevice 时有效）。
        /// </summary>
        public int DeviceIndex { get; private set; }

        /// <summary>
        /// 远程摄像头的连接地址（SourceType=RtspStream 时有效）。
        /// </summary>
        public string RtspUrl { get; private set; }

        // ── 内部状态 ────────────────────────────────────────────────────────

        /// <summary>
        /// 枚举到的本地设备列表，与 CmbDevices 下标一一对应。
        /// </summary>
        private List<(int Index, string Description)> _devices = new();

        /// <summary>
        /// 自动命名使用的计数后缀（由外部传入，与 cameraIndex 同步）。
        /// </summary>
        private readonly int _counter;

        // ── 构造函数 ────────────────────────────────────────────────────────

        /// <summary>
        /// 构造函数，传入当前摄像头计数以便自动生成名称。
        /// 同时异步枚举本地设备，便于用户切换到"本地摄像头"类型时已有设备列表。
        /// </summary>
        /// <param name="counter">摄像头计数（ViewModel 的 cameraIndex）</param>
        public AddCameraDialog(int counter = 1)
        {
            InitializeComponent();
            _counter = counter;
            // 预先后台枚举，切换到"本地摄像头"时已有结果
            _ = RefreshDevicesAsync();
        }

        // ── 类型切换 ────────────────────────────────────────────────────────

        /// <summary>
        /// RadioButton 选中回调：切换各类型对应的面板可见性。
        /// </summary>
        private void OnTypeChanged(object sender, RoutedEventArgs e)
        {
            // InitializeComponent 完成前控件为 null，提前返回
            if (PanelSimulated == null) return;

            PanelSimulated.Visibility = RbSimulated.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelLocal.Visibility     = RbLocal.IsChecked     == true ? Visibility.Visible : Visibility.Collapsed;
            PanelRtsp.Visibility      = RbRtsp.IsChecked      == true ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── 模拟摄像头：文件浏览 ────────────────────────────────────────────

        /// <summary>
        /// 打开文件选择对话框，将路径填入 TxtFilePath。
        /// </summary>
        private void OnBrowseFile(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.avi;*.mov;*.mkv|所有文件|*.*",
                Title  = "选择视频文件"
            };
            if (dlg.ShowDialog() == true)
                TxtFilePath.Text = dlg.FileName;
        }

        // ── 本地摄像头：设备枚举 ────────────────────────────────────────────

        /// <summary>
        /// "刷新设备列表"按钮回调。
        /// </summary>
        private async void OnRefreshDevices(object sender, RoutedEventArgs e)
        {
            await RefreshDevicesAsync();
        }

        /// <summary>
        /// 后台枚举本地摄像头设备，每个索引最多等待 2 秒（防止驱动挂起卡住 UI）。
        /// </summary>
        private async Task RefreshDevicesAsync()
        {
            if (TxtEnumerating == null) return;

            TxtEnumerating.Visibility = Visibility.Visible;
            TxtNoDevice.Visibility    = Visibility.Collapsed;
            CmbDevices.IsEnabled      = false;

            _devices = await Task.Run(() => EnumerateLocalCameras());

            CmbDevices.Items.Clear();
            foreach (var d in _devices)
                CmbDevices.Items.Add(d.Description);

            if (_devices.Count > 0)
                CmbDevices.SelectedIndex = 0;

            TxtEnumerating.Visibility = Visibility.Collapsed;
            TxtNoDevice.Visibility    = _devices.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            CmbDevices.IsEnabled      = _devices.Count > 0;
        }

        /// <summary>
        /// 枚举系统本地摄像头，逐索引 0~9 尝试，每个索引超时 2 秒。
        /// 在后台线程调用，不会阻塞 UI。
        /// </summary>
        private static List<(int Index, string Description)> EnumerateLocalCameras()
        {
            var result = new List<(int, string)>();
            for (int i = 0; i < 10; i++)
            {
                string desc = null;
                var task = Task.Run(() =>
                {
                    try
                    {
                        // DirectShow 后端：Windows 下兼容性最佳
                        using var cap = new OpenCvSharp.VideoCapture(i, OpenCvSharp.VideoCaptureAPIs.DSHOW);
                        if (!cap.IsOpened()) return;
                        var w = (int)cap.Get(OpenCvSharp.VideoCaptureProperties.FrameWidth);
                        var h = (int)cap.Get(OpenCvSharp.VideoCaptureProperties.FrameHeight);
                        desc = (w > 0 && h > 0) ? $"摄像头 {i}  ({w}×{h})" : $"摄像头 {i}";
                    }
                    catch { /* 忽略单个设备打开失败 */ }
                });
                // 超时 2 秒：防止驱动挂起导致枚举无限阻塞
                if (task.Wait(TimeSpan.FromSeconds(2)) && desc != null)
                    result.Add((i, desc));
            }
            return result;
        }

        // ── 确认 / 取消 ─────────────────────────────────────────────────────

        /// <summary>
        /// 确认添加：根据当前选中类型验证输入，通过后写入返回属性并关闭对话框。
        /// </summary>
        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            if (RbSimulated.IsChecked == true)
            {
                // 验证文件路径
                if (string.IsNullOrWhiteSpace(TxtFilePath.Text) || !File.Exists(TxtFilePath.Text))
                {
                    MessageBox.Show("请选择一个有效的视频文件。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                SourceType = CameraDetectionViewModel.CameraSourceType.SimulatedFile;
                FilePath   = TxtFilePath.Text;
                CameraName = string.IsNullOrWhiteSpace(TxtName.Text)
                    ? $"模拟摄像头 {_counter}"
                    : TxtName.Text.Trim();
            }
            else if (RbLocal.IsChecked == true)
            {
                // 验证设备选择
                if (CmbDevices.SelectedIndex < 0 || _devices.Count == 0)
                {
                    MessageBox.Show("请先选择一个摄像头设备，或点击\"刷新设备列表\"重新检测。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                SourceType  = CameraDetectionViewModel.CameraSourceType.LocalDevice;
                DeviceIndex = _devices[CmbDevices.SelectedIndex].Index;
                CameraName  = string.IsNullOrWhiteSpace(TxtName.Text)
                    ? _devices[CmbDevices.SelectedIndex].Description
                    : TxtName.Text.Trim();
            }
            else // 远程 RTSP
            {
                // 验证 URL 格式
                var url = TxtRtspUrl.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(url)
                    || (!url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("http://",  StringComparison.OrdinalIgnoreCase)
                        && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("请输入有效的连接地址（以 rtsp:// 或 http:// 开头）。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                SourceType = CameraDetectionViewModel.CameraSourceType.RtspStream;
                RtspUrl    = url;
                CameraName = string.IsNullOrWhiteSpace(TxtName.Text)
                    ? $"远程摄像头 {_counter}"
                    : TxtName.Text.Trim();
            }

            DialogResult = true;
        }

        /// <summary>
        /// 取消：直接关闭对话框（DialogResult=false）。
        /// </summary>
        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
