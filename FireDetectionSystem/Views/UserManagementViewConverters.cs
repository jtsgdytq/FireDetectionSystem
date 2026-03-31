using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FireDetectionSystem.Views
{
    /// <summary>
    /// 角色到颜色转换器
    /// 将用户角色转换为对应的颜色画刷，用于 DataGrid 中的角色显示
    /// Admin = 红色，Operator = 蓝色，Viewer = 绿色
    /// </summary>
    public class RoleToColorConverter : IValueConverter
    {
        /// <summary>
        /// 将角色字符串转换为颜色画刷
        /// </summary>
        /// <param name="value">角色字符串（Admin、Operator、Viewer）</param>
        /// <returns>对应的 SolidColorBrush</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string role)
            {
                return role switch
                {
                    "Admin" => new SolidColorBrush(Color.FromRgb(244, 67, 54)),    // 红色 - 管理员
                    "Operator" => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // 蓝色 - 操作员
                    "Viewer" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),    // 绿色 - 查看者
                    _ => new SolidColorBrush(Colors.Gray)                           // 灰色 - 未知角色
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 状态到颜色转换器
    /// 将用户激活状态转换为对应的颜色画刷
    /// 激活 = 绿色，禁用 = 灰色
    /// </summary>
    public class StatusToColorConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔状态转换为颜色画刷
        /// </summary>
        /// <param name="value">布尔值（true = 激活，false = 禁用）</param>
        /// <returns>对应的 SolidColorBrush</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))    // 绿色 - 激活
                    : new SolidColorBrush(Color.FromRgb(158, 158, 158)); // 灰色 - 禁用
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 状态到文本转换器
    /// 将布尔状态转换为中文文本
    /// true = "激活"，false = "禁用"
    /// </summary>
    public class StatusToTextConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔状态转换为中文文本
        /// </summary>
        /// <param name="value">布尔值（true = 激活，false = 禁用）</param>
        /// <returns>中文状态文本</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? "激活" : "禁用";
            }
            return "未知";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 状态到图标转换器
    /// 将用户激活状态转换为 Material Design 图标名称
    /// 激活 = AccountCheck，禁用 = AccountOff
    /// </summary>
    public class StatusToIconConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔状态转换为图标名称
        /// </summary>
        /// <param name="value">布尔值（true = 激活，false = 禁用）</param>
        /// <returns>Material Design 图标名称字符串</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                // 注意：这里的逻辑是反向的，因为按钮显示的是"要执行的操作"
                // 激活状态显示 AccountOff（表示可以禁用）
                // 禁用状态显示 AccountCheck（表示可以启用）
                return isActive ? "AccountOff" : "AccountCheck";
            }
            return "Account";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 状态到切换提示转换器
    /// 将用户激活状态转换为操作提示文本
    /// 激活 = "禁用账户"，禁用 = "启用账户"
    /// </summary>
    public class StatusToToggleTooltipConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔状态转换为操作提示文本
        /// </summary>
        /// <param name="value">布尔值（true = 激活，false = 禁用）</param>
        /// <returns>操作提示文本</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                // 显示的是"要执行的操作"
                return isActive ? "禁用账户" : "启用账户";
            }
            return "切换状态";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
