using System.Collections.Generic;
using System.Windows;

namespace FireDetectionSystem.Views
{
    /// <summary>
    /// 本地摄像头选择对话框。
    /// 展示 EnumerateLocalCameras() 枚举到的设备列表，用户确认后返回所选设备索引和描述。
    /// </summary>
    public partial class LocalCameraSelectDialog : Window
    {
        /// <summary>
        /// 用户选中的设备索引，DialogResult=true 时有效，传给 VideoCapture(int) 打开摄像头。
        /// </summary>
        public int SelectedDeviceIndex { get; private set; }

        /// <summary>
        /// 用户选中的设备描述文本，用作 CameraItem.Name 显示。
        /// </summary>
        public string SelectedDescription { get; private set; }

        /// <summary>
        /// 枚举到的设备列表（索引与描述的映射），与 DeviceListBox 项目一一对应。
        /// </summary>
        private readonly List<(int Index, string Description)> _devices;

        /// <summary>
        /// 构造函数，传入已枚举的设备列表并填充 ListBox。
        /// </summary>
        /// <param name="devices">EnumerateLocalCameras() 返回的设备列表</param>
        public LocalCameraSelectDialog(List<(int Index, string Description)> devices)
        {
            InitializeComponent();
            _devices = devices;

            // 将设备描述文本填入列表，默认选中第一项
            foreach (var d in devices)
                DeviceListBox.Items.Add(d.Description);

            if (devices.Count > 0)
                DeviceListBox.SelectedIndex = 0;
        }

        /// <summary>
        /// 确认连接：读取选中设备信息并关闭对话框（DialogResult=true）。
        /// </summary>
        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            if (DeviceListBox.SelectedIndex < 0)
            {
                MessageBox.Show(
                    "请先选择一个摄像头设备。",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var selected = _devices[DeviceListBox.SelectedIndex];
            SelectedDeviceIndex  = selected.Index;
            SelectedDescription  = selected.Description;
            DialogResult = true;
        }

        /// <summary>
        /// 取消：关闭对话框（DialogResult=false）。
        /// </summary>
        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
