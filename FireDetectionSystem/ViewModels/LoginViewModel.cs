using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace FireDetectionSystem.ViewModels
{
    public class LoginViewModel : BindableBase,IDialogAware
    {
        public string Title => "火灾检测";

        public DialogCloseListener RequestClose { get; set; }

        public DelegateCommand LoginCommand { get; set; }

        public DelegateCommand CloseCommand { get; set; }



        public LoginViewModel()
        {
            LoginCommand = new DelegateCommand(Login);
            CloseCommand = new DelegateCommand(() => RequestClose.Invoke(new DialogResult(ButtonResult.No)));
        }
        /// <summary>
        /// 登录方法
        /// </summary>
        private void Login()
        {
              
             RequestClose.Invoke(new DialogResult(ButtonResult.OK));
           
        }

        

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
           
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            
        }
    }
}
