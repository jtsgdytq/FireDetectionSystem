using Compunet.YoloSharp;
using Compunet.YoloSharp.Plotting;
using SixLabors.ImageSharp.Formats.Png;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using FireDetectionSystem.Core;


namespace FireDetectionSystem.Views
{
    /// <summary>
    /// MainView.xaml 的交互逻辑
    /// </summary>
    public partial class MainView : Window
    {

        public readonly IRegionManager _regionManager;
        public MainView(IRegionManager regionManager)
        {
            InitializeComponent();
            _regionManager = regionManager;
            this.Loaded += MainView_Loaded;
        }

        /// <summary>
        /// 初始化主页面模块，初始化导航到摄像头检测模块
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            _regionManager.RequestNavigate("ContentRegion", "CameraDetection");
        }

       
       

        private bool _isMenuOpen = false;
        private void ButtonOpenMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_isMenuOpen)
            {
                // 关闭菜单
                Storyboard sb = FindResource("MenuClose") as Storyboard;
                sb?.Begin();
            }
            else
            {
                // 打开菜单
                Storyboard sb = FindResource("MenuOpen") as Storyboard;
                sb?.Begin();
            }
            _isMenuOpen = !_isMenuOpen;
        }

    }
}
