# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 协作规则

- **始终使用中文思考和回复**，无论用户用何种语言提问。
- **代码修改和添加都要添加中文注释**，对每个函数、字段、属性。

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

- **`Core/`** — `FireDetectionModule`（静态单例）：加载并封装 YoloSharp 预测器。三个入口点：`DetectAsync(imagePath)` 用于图片，`Detect(Image)` 用于 ImageSharp 对象，`DetectFrame(Mat)` 用于 OpenCV 帧。支持 `Reinitialize(modelPath)` 热更换模型。使用双重检查锁定保证线程安全。

- **`Services/`** — 通过 Prism/DryIoc 以单例模式注入到 ViewModel 的业务逻辑层：
  - `IConfigurationService` / `ConfigurationService` — 读取并实时写入 `appsettings.json`；`SaveAllSettings()` 使用临时文件 + `File.Replace` 原子写防止文件损坏
  - `ILoggerService` / `LoggerService` — 封装 Serilog；写入 `logs/app-{date}.log`（滚动日志，保留 30 天）
  - `IDatabaseService` / `DatabaseService` — EF Core 操作；首次运行时初始化数据库和默认管理员账户；包含旧库自动补列迁移逻辑
  - `IUserService` / `UserService` — 使用 BCrypt 密码哈希的登录验证和用户管理；支持按角色筛选
  - `IAlarmService` / `AlarmService` — 当检测到火灾且超过阈值时触发声音（WAV/系统声音）、桌面通知（MessageBox）和/或 MailKit 邮件报警；将报警结果写入 `AlarmLogs` 表

- **`Data/`** — `FireDetectionDbContext`（EF Core）：管理 SQLite 数据库（`firedetection.db`）；注册为**瞬态**（非单例）以避免 DbContext 生命周期问题

- **`Models/`** — EF Core 实体：`User`、`DetectionRecord`、`AlarmLog`、`SystemConfig`

- **`ViewModels/`** — 每个 View 都有配对的 ViewModel，另有两个对话框 ViewModel：`UserEditDialogViewModel`、`PasswordResetDialogViewModel`。导航由 Prism 的 `RegionManager` 处理；名为 `"ContentRegion"` 的区域（在 `MainView.xaml` 中）承载活动页面。

- **`Views/`** — 使用 Material Design 主题的 XAML 视图。`LoginView` 在主窗口显示前作为 Prism 对话框展示。

- **`Converters/`** — 用于 `UserManagementView` 的 WPF 值转换器：`RoleToColorConverter`、`StatusToColorConverter`、`StatusToTextConverter`、`StatusToIconConverter`、`StatusToToggleTooltipConverter`。**注意：这些类虽位于 `Converters/` 文件夹，但命名空间为 `FireDetectionSystem.Views`。**

### 启动序列（`App.xaml.cs`）

1. `CreateShell()` 解析 `MainView`
2. `RegisterTypes()` 将所有服务注册为单例并注册导航视图
3. `OnInitialized()`：
   - 初始化 SQLite 数据库（`EnsureCreated()` + 默认 `admin`/`admin123` 账户 + 旧库补列）
   - 从 `appsettings.json:ModelSettings:ModelPath` 加载 YOLO 模型 — 文件不存在时仅记录警告并继续启动（检测功能不可用）
   - 显示 `LoginView` 对话框 — 取消则 `Environment.Exit(0)` 退出；成功则调用 `base.OnInitialized()` 显示主窗口

### 检测模式

| 模式 | ViewModel | 关键行为 |
|------|-----------|---------|
| 图片 | `ImageDetectionViewModel` | 异步检测；保存 `DetectionRecord` 到数据库；超过阈值则触发报警 |
| 视频 | `VideoDetectionViewModel` | 三阶段异步管道（解码→推理→渲染）；`Channel<T>` 帧队列；连续帧状态机；支持暂停/继续 |
| 摄像头 | `CameraDetectionViewModel` | 多源并发检测；每路摄像头独立状态机；支持待处理报警管理 |

### 视频 / 摄像头检测管道

**帧数据流：** OpenCV `Mat`（BGR）→ JPEG 字节流 → ImageSharp `Image`（RGB）→ YOLO 推理

**连续帧事件状态机**（视频和摄像头共用逻辑，摄像头状态字段挂在 `CameraItem` 上）：

```
Idle ──( 连续命中 N=5 帧 )──> Active ──( 连续 miss M=3 帧 或 停止 )──> 落库 ──> Idle
```

- 事件落库（`FlushVideoEventAsync` / `FlushCameraEventAsync`）：一次事件只写一条 `DetectionRecord`，避免高帧率写放大
- 冷却期 5 秒：事件结束后 5 秒内不开启新事件，防止同一火灾反复触发报警
- 强制结算：调用停止或视频播放结束时，若有未结束的 Active 事件，必须先 Flush 再取消 CancellationToken

### 数据模型关键字段

**`DetectionRecord`（检测记录）：**
- `SourceType`：Image / Video / Camera
- `EventId`（64 字符）：视频/摄像头事件唯一标识，图片检测留 `null`
- `HandleStatus`（仅摄像头）：Pending / Confirmed / FalseAlarm / Resolved
- `HandledByUserId`、`HandledAt`、`HandleNotes`：报警处理信息
- 事件统计字段：`StartFrameIndex`、`EndFrameIndex`、`PeakConfidence`、`AverageConfidence`、`EventDurationMs` 等

**`AlarmLog`（报警日志）：** 记录每次报警的类型（Sound/Desktop/Email）、级别（Low/Medium/High）、是否成功及失败原因。

### 摄像头模块特殊功能

- **多源类型：** `SimulatedFile`（视频文件循环）、`LocalDevice`（USB 摄像头）、`RtspStream`（网络流）
- **待处理报警管理：** `GetPendingCameraAlarmsAsync()` 加载未处理报警；`ConfirmHandleAsync()` 更新处理状态、操作人和备注

### 设置模块

- `SettingsViewModel` 支持：模型路径浏览和热重载（`FireDetectionModule.Reinitialize()`）、置信度/报警阈值滑块、三种报警方式开关、从用户列表中选择邮件接收人、发送测试邮件
- 所有设置通过 `ConfigurationService.SaveAllSettings()` 原子写入 `appsettings.json`

### 用户管理模块

- `UserManagementViewModel`：用户列表（含角色/状态筛选和搜索）、编辑对话框、启用/禁用账户、重置密码
- `operatorUsername` 参数应使用 `LoginViewModel.CurrentUser?.Username`（部分地方仍硬编码为 `"admin"`，待修复）

### 关键实现要点

- 导航区域名称为 `"ContentRegion"`（不是 `"MainRegion"`）
- `LoginViewModel.CurrentUser` 是持有当前登录用户的**静态属性**；退出登录时清除为 `null`
- 退出登录通过 `Process.Start(exePath)` 重启进程再 `Shutdown()` 来返回登录界面
- 所有密码使用 BCrypt 哈希；默认账户 `admin`/`admin123` 在数据库初始化时创建（不使用 EF HasData，因 BCrypt 每次生成新 salt）
- `.csproj` 中包含 `YoloDotNet` 包但**未被使用**，实际代码使用的是 `YoloSharp`（Compunet.YoloSharp）
- `VideoDetectionViewModel` 使用反射（`DetectionAccessor`）适配不同 YoloSharp 版本的输出格式

### 配置文件（`appsettings.json`）

`ModelSettings:ModelPath` **必须指向有效的 ONNX 文件**，应用才能运行检测。当前值是开发者特定的绝对路径 — 请更新为相对路径如 `models/best.onnx`。

主要配置节：
- `ModelSettings`：模型路径、置信度阈值（0.5）、NMS 阈值（0.45）、输入尺寸（640）
- `AlarmSettings`：启用/禁用声音/桌面/邮件；`AlertThreshold`（0.7）；`SoundFilePath`
- `DatabaseSettings`：SQLite 连接字符串（默认 `Data Source=firedetection.db`）
- `EmailSettings`：SMTP 配置（MailKit，支持 SSL/TLS）
- `Logging`：日志级别和滚动文件路径
