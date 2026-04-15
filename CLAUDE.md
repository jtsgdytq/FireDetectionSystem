# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在此代码库中工作时提供指导。

## 协作规则

- **始终使用中文思考和回复**，无论用户用何种语言提问。
- **代码修改和添加都要添加中文注释**，对每个函数，字段，属性。 

## 构建和运行命令

```bash
# 还原 NuGet 包
dotnet restore FireDetectionSystem/FireDetectionSystem.csproj

# 构建项目
dotnet build FireDetectionSystem/FireDetectionSystem.csproj

# 以 Release 模式构建
dotnet build FireDetectionSystem/FireDetectionSystem.csproj -c Release

# 运行应用程序
dotnet run --project FireDetectionSystem/FireDetectionSystem.csproj
```

仅限 Windows 的 WPF 应用程序（`net8.0-windows`），无法在 Linux/macOS 上构建或运行。

## 架构概览

**基于 YOLO 的火灾检测系统** — 使用 MVVM 模式、Prism 9 框架和 DryIoc 依赖注入的 WPF 桌面应用程序。

### 层次结构

- **`Core/`** — `FireDetectionModule`（静态单例）：加载并封装 YoloSharp 预测器。三个入口点：`DetectAsync(imagePath)` 用于图片，`Detect(Image)` 用于 ImageSharp 对象，`DetectFrame(Mat)` 用于 OpenCV 帧。使用双重检查锁定保证线程安全。

- **`Services/`** — 通过 Prism/DryIoc 以单例模式注入到 ViewModel 的业务逻辑层：
  - `IConfigurationService` / `ConfigurationService` — 读取 `appsettings.json`；提供对模型路径、阈值、数据库连接、邮件设置的类型化访问
  - `ILoggerService` / `LoggerService` — 封装 Serilog；写入 `logs/app-{date}.log`（滚动日志，保留 30 天）
  - `IDatabaseService` / `DatabaseService` — EF Core 操作；首次运行时初始化数据库和默认管理员账户
  - `IUserService` / `UserService` — 使用 BCrypt 密码哈希的登录验证和用户管理
  - `IAlarmService` / `AlarmService` — 当检测到火灾且超过阈值时触发声音、桌面通知和/或邮件报警

- **`Data/`** — `FireDetectionDbContext`（EF Core）：管理 SQLite 数据库（`firedetection.db`）

- **`Models/`** — EF Core 实体：`User`、`DetectionRecord`、`AlarmLog`、`SystemConfig`

- **`ViewModels/`** — 每个 View 都有配对的 ViewModel。导航由 Prism 的 `RegionManager` 处理；名为 `"ContentRegion"` 的区域（在 `MainView.xaml` 中）承载活动页面。

- **`Views/`** — 使用 Material Design 主题的 XAML 视图。`LoginView` 在主窗口显示前作为 Prism 对话框展示。

- **`Converters/`** — 用于 `UserManagementView` 的 WPF 值转换器：`RoleToColorConverter`、`StatusToColorConverter`、`StatusToTextConverter`、`StatusToIconConverter`、`StatusToToggleTooltipConverter`。注意：这些类虽位于 `Converters/` 文件夹，但命名空间为 `FireDetectionSystem.Views`。

### 启动序列（`App.xaml.cs`）

1. `CreateShell()` 解析 `MainView`
2. `RegisterTypes()` 将所有服务注册为单例并注册导航视图
3. `OnInitialized()`：
   - 初始化 SQLite 数据库（`EnsureCreated()` + 默认 `admin`/`admin123` 账户）
   - 从 `appsettings.json` 的 `ModelSettings:ModelPath` 加载 YOLO 模型 — 文件不存在时仅记录警告并继续启动（检测功能不可用）
   - 显示 `LoginView` 对话框 — 取消则退出应用；成功则调用 `base.OnInitialized()` 显示主窗口

### 检测模式

| 模式 | ViewModel | 关键行为 |
|------|-----------|---------|
| 图片 | `ImageDetectionViewModel` | 异步检测；保存 `DetectionRecord` 到数据库；超过阈值则触发报警 |
| 视频 | `VideoDetectionViewModel` | 逐帧同步检测；120ms UI 节流；支持暂停/继续 |
| 摄像头 | `CameraDetectionViewModel` | 多源并发检测；使用视频文件模拟摄像头流；循环播放 |

### 关键实现要点

- 导航区域名称为 `"ContentRegion"`（不是 `"MainRegion"`）
- `LoginViewModel.CurrentUser` 是持有当前登录用户的静态属性；退出登录时清除为 `null`
- 退出登录通过 `Process.Start(exePath)` 重启进程再 `Shutdown()` 来返回登录界面
- 视频/摄像头检测管道：OpenCV `Mat`（BGR）→ JPEG 字节流 → ImageSharp `Image`（RGB）→ YOLO
- `FireDetectionDbContext` 注册为瞬态（非单例），以避免 DbContext 生命周期问题
- 所有密码使用 BCrypt 哈希；默认账户为 `admin`/`admin123`
- 用户管理操作中 `operatorUsername` 参数目前硬编码为 `"admin"`，应改为使用 `LoginViewModel.CurrentUser?.Username`
- `.csproj` 中包含 `YoloDotNet` 包但未被使用，实际代码使用的是 `YoloSharp`（Compunet.YoloSharp）

### 配置文件（`appsettings.json`）

`ModelSettings:ModelPath` **必须指向有效的 ONNX 文件**，应用才能运行检测。当前值是开发者特定的绝对路径 — 请更新它或使用相对路径如 `models/best.onnx`。

主要配置部分：
- `ModelSettings`：模型路径、置信度阈值（默认 0.5）、NMS 阈值（0.45）、输入尺寸（640）
- `AlarmSettings`：启用/禁用声音、桌面通知、邮件；`AlertThreshold`（默认 0.7）；`SoundFilePath`
- `DatabaseSettings`：SQLite 连接字符串（默认：`Data Source=firedetection.db`）
- `EmailSettings`：邮件报警的 SMTP 配置（使用 MailKit）
- `Logging`：日志级别和滚动文件路径
