using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FireDetectionSystem.Models;
using FireDetectionSystem.Services;

namespace FireDetectionSystem.ViewModels
{
    /// <summary>
    /// 用户管理 ViewModel
    /// 实现用户的增删改查功能
    /// </summary>
    public class UserManagementViewModel : BindableBase, INavigationAware
    {
        // 依赖注入的服务
        private readonly IUserService _userService;      // 用户服务，处理用户数据操作
        private readonly ILoggerService _logger;         // 日志服务，记录操作日志
        private readonly IDialogService _dialogService;  // 对话框服务，显示编辑和重置密码对话框

        /// <summary>
        /// 所有用户列表（未过滤）
        /// 从数据库加载的完整用户列表
        /// </summary>
        private ObservableCollection<User> _users = new ObservableCollection<User>();
        public ObservableCollection<User> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        /// <summary>
        /// 过滤后的用户列表
        /// 根据搜索关键字、角色和状态筛选后的用户列表，绑定到 DataGrid
        /// </summary>
        private ObservableCollection<User> _filteredUsers = new ObservableCollection<User>();
        public ObservableCollection<User> FilteredUsers
        {
            get => _filteredUsers;
            set => SetProperty(ref _filteredUsers, value);
        }

        /// <summary>
        /// 搜索关键字
        /// 用于按用户名、姓名、邮箱模糊搜索
        /// </summary>
        private string _searchKeyword = string.Empty;
        public string SearchKeyword
        {
            get => _searchKeyword;
            set
            {
                SetProperty(ref _searchKeyword, value);
                ApplyFilters(); // 搜索关键字变化时自动应用筛选
            }
        }

        /// <summary>
        /// 选中的角色筛选项
        /// 可选值："全部"、"Admin"、"Operator"、"Viewer"
        /// </summary>
        private string _selectedRoleFilter = "全部";
        public string SelectedRoleFilter
        {
            get => _selectedRoleFilter;
            set
            {
                SetProperty(ref _selectedRoleFilter, value);
                ApplyFilters(); // 角色筛选变化时自动应用筛选
            }
        }

        /// <summary>
        /// 选中的状态筛选项
        /// 可选值："全部"、"激活"、"禁用"
        /// </summary>
        private string _selectedStatusFilter = "全部";
        public string SelectedStatusFilter
        {
            get => _selectedStatusFilter;
            set
            {
                SetProperty(ref _selectedStatusFilter, value);
                ApplyFilters(); // 状态筛选变化时自动应用筛选
            }
        }

        /// <summary>
        /// 当前选中的用户
        /// 用于编辑、删除、状态切换等操作
        /// </summary>
        private User? _selectedUser;
        public User? SelectedUser
        {
            get => _selectedUser;
            set => SetProperty(ref _selectedUser, value);
        }

        /// <summary>
        /// 是否正在加载数据
        /// 用于显示加载指示器
        /// </summary>
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// 角色筛选选项列表
        /// </summary>
        public List<string> RoleFilters { get; } = new List<string> { "全部", "Admin", "Operator", "Viewer" };

        /// <summary>
        /// 状态筛选选项列表
        /// </summary>
        public List<string> StatusFilters { get; } = new List<string> { "全部", "激活", "禁用" };

        /// <summary>
        /// 命令：加载用户列表
        /// </summary>
        public DelegateCommand LoadUsersCommand { get; }

        /// <summary>
        /// 命令：添加新用户
        /// </summary>
        public DelegateCommand AddUserCommand { get; }

        /// <summary>
        /// 命令：编辑用户信息
        /// </summary>
        public DelegateCommand<User> EditUserCommand { get; }

        /// <summary>
        /// 命令：删除用户
        /// </summary>
        public DelegateCommand<User> DeleteUserCommand { get; }

        /// <summary>
        /// 命令：切换用户状态（启用/禁用）
        /// </summary>
        public DelegateCommand<User> ToggleUserStatusCommand { get; }

        /// <summary>
        /// 命令：重置用户密码
        /// </summary>
        public DelegateCommand<User> ResetPasswordCommand { get; }

        /// <summary>
        /// 命令：刷新用户列表
        /// </summary>
        public DelegateCommand RefreshCommand { get; }

        /// <summary>
        /// 构造函数
        /// 初始化服务和命令
        /// </summary>
        /// <param name="userService">用户服务</param>
        /// <param name="logger">日志服务</param>
        /// <param name="dialogService">对话框服务</param>
        public UserManagementViewModel(
            IUserService userService,
            ILoggerService logger,
            IDialogService dialogService)
        {
            _userService = userService;
            _logger = logger;
            _dialogService = dialogService;

            // 初始化所有命令
            LoadUsersCommand = new DelegateCommand(async () => await LoadUsersAsync());
            AddUserCommand = new DelegateCommand(ShowAddUserDialog);
            EditUserCommand = new DelegateCommand<User>(ShowEditUserDialog);
            DeleteUserCommand = new DelegateCommand<User>(async (user) => await DeleteUserAsync(user));
            ToggleUserStatusCommand = new DelegateCommand<User>(async (user) => await ToggleUserStatusAsync(user));
            ResetPasswordCommand = new DelegateCommand<User>(ShowResetPasswordDialog);
            RefreshCommand = new DelegateCommand(async () => await LoadUsersAsync());
        }

        /// <summary>
        /// 从数据库加载所有用户
        /// 加载完成后自动应用当前的筛选条件
        /// </summary>
        private async Task LoadUsersAsync()
        {
            try
            {
                IsLoading = true;
                var users = await _userService.GetAllUsersAsync();
                Users = new ObservableCollection<User>(users);
                ApplyFilters(); // 应用当前的筛选条件
                _logger.Info($"用户管理 - 加载用户列表，共 {users.Count} 个用户");
            }
            catch (Exception ex)
            {
                _logger.Error($"用户管理 - 加载用户列表失败: {ex.Message}", ex);
                MessageBox.Show("加载用户列表失败，请查看日志", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 应用筛选条件
        /// 根据搜索关键字、角色和状态筛选用户列表
        /// </summary>
        private void ApplyFilters()
        {
            var filtered = Users.AsEnumerable();

            // 搜索关键字筛选（用户名、姓名、邮箱）
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                var keyword = SearchKeyword.ToLower();
                filtered = filtered.Where(u =>
                    u.Username.ToLower().Contains(keyword) ||
                    (u.FullName != null && u.FullName.ToLower().Contains(keyword)) ||
                    (u.Email != null && u.Email.ToLower().Contains(keyword)));
            }

            // 角色筛选
            if (SelectedRoleFilter != "全部")
            {
                filtered = filtered.Where(u => u.Role == SelectedRoleFilter);
            }

            // 状态筛选
            if (SelectedStatusFilter == "激活")
            {
                filtered = filtered.Where(u => u.IsActive);
            }
            else if (SelectedStatusFilter == "禁用")
            {
                filtered = filtered.Where(u => !u.IsActive);
            }

            FilteredUsers = new ObservableCollection<User>(filtered);
        }

        /// <summary>
        /// 显示添加用户对话框
        /// </summary>
        private void ShowAddUserDialog()
        {
            var parameters = new DialogParameters
            {
                { "Mode", "Add" }  // 设置为添加模式
            };

            _dialogService.ShowDialog("UserEditDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    LoadUsersCommand.Execute();  // 添加成功后刷新列表
                }
            });
        }

        /// <summary>
        /// 显示编辑用户对话框
        /// </summary>
        /// <param name="user">要编辑的用户</param>
        private void ShowEditUserDialog(User user)
        {
            if (user == null) return;

            var parameters = new DialogParameters
            {
                { "Mode", "Edit" },  // 设置为编辑模式
                { "User", user }     // 传递用户对象
            };

            _dialogService.ShowDialog("UserEditDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    LoadUsersCommand.Execute();  // 编辑成功后刷新列表
                }
            });
        }

        /// <summary>
        /// 删除用户
        /// 显示确认对话框，确认后执行删除操作
        /// 注意：管理员账户不能删除
        /// </summary>
        /// <param name="user">要删除的用户</param>
        private async Task DeleteUserAsync(User user)
        {
            if (user == null) return;

            // 显示确认对话框
            var result = MessageBox.Show(
                $"确定要删除用户 '{user.Username}' 吗？\n\n此操作不可恢复！",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // 执行删除操作（操作人暂时硬编码为 "admin"）
                var success = await _userService.DeleteUserAsync(user.Id, "admin");
                if (success)
                {
                    Users.Remove(user);  // 从列表中移除
                    ApplyFilters();      // 重新应用筛选
                    MessageBox.Show("删除成功", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("删除失败，管理员账户不能删除", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 切换用户状态（启用/禁用）
        /// 根据当前状态自动选择启用或禁用操作
        /// </summary>
        /// <param name="user">要切换状态的用户</param>
        private async Task ToggleUserStatusAsync(User user)
        {
            if (user == null) return;

            bool success;
            if (user.IsActive)
            {
                // 当前是激活状态，执行禁用操作
                success = await _userService.DisableUserAsync(user.Id, "admin");
            }
            else
            {
                // 当前是禁用状态，执行启用操作
                success = await _userService.EnableUserAsync(user.Id, "admin");
            }

            if (success)
            {
                await LoadUsersAsync();  // 操作成功后刷新列表
            }
            else
            {
                MessageBox.Show("操作失败，请查看日志", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 显示重置密码对话框
        /// 管理员可以为任何用户重置密码
        /// </summary>
        /// <param name="user">要重置密码的用户</param>
        private void ShowResetPasswordDialog(User user)
        {
            if (user == null) return;

            var parameters = new DialogParameters
            {
                { "UserId", user.Id },
                { "Username", user.Username },
                { "OperatorUsername", "admin" }  // 操作人暂时硬编码为 "admin"
            };

            _dialogService.ShowDialog("PasswordResetDialog", parameters, result =>
            {
                // 密码重置成功后不需要刷新列表（密码不显示在列表中）
            });
        }

        /// <summary>
        /// 导航到此页面时触发
        /// 自动加载用户列表
        /// </summary>
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            LoadUsersCommand.Execute();
        }

        /// <summary>
        /// 判断是否可以导航到此页面
        /// 始终返回 true，表示可以重复导航
        /// </summary>
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        /// <summary>
        /// 从此页面导航离开时触发
        /// 当前无需执行任何操作
        /// </summary>
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}
