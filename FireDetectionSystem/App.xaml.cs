using FireDetectionSystem.ViewModels;
using FireDetectionSystem.Views;
using Prism.Ioc;
using System.Configuration;
using System.Data;
using System.Windows;

namespace FireDetectionSystem
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {

            return Container.Resolve<Views.MainView>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterDialog<LoginView,LoginViewModel>();
            containerRegistry.RegisterForNavigation<Views.ImageDetectionView, ViewModels.ImageDetectionViewModel>("ImageDetection");
            containerRegistry.RegisterForNavigation<Views.VideoDetectionView, ViewModels.VideoDetectionViewModel>("VideoDetection");
            containerRegistry.RegisterForNavigation<Views.SettingsView, ViewModels.SettingsViewModel>("Settings");
            containerRegistry.RegisterForNavigation<Views.UserManagementView, ViewModels.UserManagementViewModel>("UserManagement");
            containerRegistry.RegisterForNavigation<Views.CameraDetectionView, ViewModels.CameraDetectionViewModel>("CameraDetection");
           
        }

        protected override void OnInitialized()
        {
           var dialog=Container.Resolve<IDialogService>();

            dialog.ShowDialog("LoginView", r =>
            {
                if (r.Result == ButtonResult.OK)
                {
                    base.OnInitialized();
                }
                else
                {
                    Environment.Exit(0);
                    Current.Shutdown();
                }
            });

            
        }
    }

}
