using Compunet.YoloSharp.Plotting;
using FireDetectionSystem.Core;
using FireDetectionSystem.Models;
using FireDetectionSystem.Services;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FireDetectionSystem.ViewModels
{
    /// <summary>
    /// 摄像头检测页面 ViewModel。
    /// 支持多路视频源并行检测，内部采用"解码 -> 推理 -> 渲染"三段式异步流水线。
    /// </summary>
    class CameraDetectionViewModel : BindableBase
    {
        // ── 注入的服务 ───────────────────────────────────────────────────────
        private readonly ILoggerService _logger;
        private readonly IConfigurationService _configService;
        private readonly IDatabaseService _dbService;
        private readonly IAlarmService _alarmService;

        // ── 连续帧状态机常量（与视频检测保持一致） ─────────────────────────
        /// <summary>连续命中帧数达到此值才激活事件</summary>
        private const int HIT_THRESHOLD_N = 5;
        /// <summary>连续 miss 帧数达到此值才结束事件</summary>
        private const int MISS_THRESHOLD_M = 3;
        /// <summary>事件结束后冷却时间（毫秒），期间不开启新事件</summary>
        private const long COOLDOWN_MS = 5000;

        /// <summary>
        /// 摄像头集合（每一项对应一路检测上下文）。
        /// </summary>
        private readonly ObservableCollection<CameraItem> cameras = new();

        /// <summary>
        /// 摄像头集合绑定属性。
        /// </summary>
        public ObservableCollection<CameraItem> Cameras => cameras;

        /// <summary>
        /// 当前选中的摄像头项。
        /// </summary>
        private CameraItem selectedCamera;

        /// <summary>
        /// 当前选中摄像头绑定属性。
        /// </summary>
        public CameraItem SelectedCamera
        {
            get { return selectedCamera; }
            set { selectedCamera = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 置信度过滤阈值（用于检测结果展示过滤）。
        /// </summary>
        private float confidenceThreshold = 0.5f;

        /// <summary>
        /// 置信度阈值绑定属性。
        /// </summary>
        public float ConfidenceThreshold
        {
            get { return confidenceThreshold; }
            set { confidenceThreshold = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 预警阈值（最高置信度超过此值时触发预警状态）。
        /// </summary>
        private float alertThreshold = 0.7f;

        /// <summary>
        /// 预警阈值绑定属性。
        /// </summary>
        public float AlertThreshold
        {
            get { return alertThreshold; }
            set { alertThreshold = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 已连接摄像头数量（集合项总数）。
        /// </summary>
        public int ConnectedCount => Cameras.Count;

        /// <summary>
        /// 正在运行的摄像头数量（状态为 Running 的项）。
        /// </summary>
        public int RunningCount => Cameras.Count(c => c.Status == CameraStatus.Running);

        /// <summary>
        /// 添加视频源命令。
        /// </summary>
        public DelegateCommand AddVideoSourceCommand { get; }

        /// <summary>
        /// 移除选中摄像头命令。
        /// </summary>
        public DelegateCommand RemoveSelectedCommand { get; }

        /// <summary>
        /// 切换选中摄像头暂停/继续命令。
        /// </summary>
        public DelegateCommand ToggleSelectedPauseCommand { get; }

        /// <summary>
        /// 启动选中摄像头命令。
        /// </summary>
        public DelegateCommand StartSelectedCommand { get; }

        /// <summary>
        /// 模拟摄像头编号自增计数器。
        /// </summary>
        private int cameraIndex = 1;

        // ── 报警处置 UI 状态 ─────────────────────────────────────────────────

        /// <summary>
        /// 待处置报警记录列表（HandleStatus = Pending 的摄像头报警）。
        /// </summary>
        private ObservableCollection<DetectionRecord> _pendingAlarms = new();

        /// <summary>
        /// 待处置报警绑定集合。
        /// </summary>
        public ObservableCollection<DetectionRecord> PendingAlarms
        {
            get => _pendingAlarms;
            set { _pendingAlarms = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 当前选中的待处置报警记录。
        /// </summary>
        private DetectionRecord _selectedAlarm;

        /// <summary>
        /// 选中报警绑定属性。
        /// </summary>
        public DetectionRecord SelectedAlarm
        {
            get => _selectedAlarm;
            set
            {
                _selectedAlarm = value;
                RaisePropertyChanged();
                // 选中变化时刷新处置按钮可用状态
                ConfirmHandleCommand?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 处置状态 UI 显示文本 → DB 存储值的映射表。
        /// </summary>
        private static readonly Dictionary<string, string> HandleStatusMap = new()
        {
            { "已确认火灾", "Confirmed" },
            { "误报/演习",  "FalseAlarm" },
            { "已处置完毕", "Resolved" }
        };

        /// <summary>
        /// 处置结果选项列表（显示中文，落库前通过 HandleStatusMap 转换为英文枚举值）。
        /// </summary>
        public List<string> HandleStatusOptions { get; } = new List<string>
        {
            "已确认火灾",
            "误报/演习",
            "已处置完毕"
        };

        /// <summary>
        /// 当前选择的处置状态（ComboBox 绑定，中文显示值）。
        /// </summary>
        private string _selectedHandleStatus = "已确认火灾";

        /// <summary>
        /// 处置状态选择绑定属性。
        /// </summary>
        public string SelectedHandleStatus
        {
            get => _selectedHandleStatus;
            set { _selectedHandleStatus = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 处置备注文本（TextBox 绑定）。
        /// </summary>
        private string _handleNotes = string.Empty;

        /// <summary>
        /// 处置备注绑定属性。
        /// </summary>
        public string HandleNotes
        {
            get => _handleNotes;
            set { _handleNotes = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 确认处置命令：将选中报警更新为指定处置状态。
        /// </summary>
        public DelegateCommand ConfirmHandleCommand { get; private set; }

        /// <summary>
        /// 手动刷新待处置列表命令。
        /// </summary>
        public DelegateCommand RefreshPendingAlarmsCommand { get; private set; }

        /// <summary>
        /// 构造函数：通过 Prism DI 自动注入四个服务，初始化命令并订阅摄像头集合变化事件。
        /// </summary>
        public CameraDetectionViewModel(
            ILoggerService logger,
            IConfigurationService configService,
            IDatabaseService dbService,
            IAlarmService alarmService)
        {
            _logger = logger;
            _configService = configService;
            _dbService = dbService;
            _alarmService = alarmService;

            // 从配置读取默认阈值
            ConfidenceThreshold = _configService.ConfidenceThreshold;
            AlertThreshold = _configService.AlertThreshold;

            AddVideoSourceCommand = new DelegateCommand(AddVideoSource);
            RemoveSelectedCommand = new DelegateCommand(RemoveSelected, CanRemoveSelected)
                .ObservesProperty(() => SelectedCamera);
            ToggleSelectedPauseCommand = new DelegateCommand(ToggleSelectedPause, CanToggleSelected)
                .ObservesProperty(() => SelectedCamera);
            StartSelectedCommand = new DelegateCommand(StartSelected, CanStartSelected)
                .ObservesProperty(() => SelectedCamera);

            // 处置相关命令
            ConfirmHandleCommand = new DelegateCommand(
                async () => await ConfirmHandleAsync(),
                () => SelectedAlarm != null && !string.IsNullOrEmpty(SelectedHandleStatus));
            RefreshPendingAlarmsCommand = new DelegateCommand(
                async () => await RefreshPendingAlarmsAsync());

            cameras.CollectionChanged += OnCamerasChanged;

            // 启动后立即加载待处置报警列表
            _ = RefreshPendingAlarmsAsync();
        }

        /// <summary>
        /// 摄像头集合变化回调：
        /// 为新增项注册属性监听，为移除项取消监听，并刷新统计数据。
        /// </summary>
        private void OnCamerasChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (CameraItem item in e.NewItems)
                    item.PropertyChanged += OnCameraItemChanged;
            if (e.OldItems != null)
                foreach (CameraItem item in e.OldItems)
                    item.PropertyChanged -= OnCameraItemChanged;

            RaisePropertyChanged(nameof(ConnectedCount));
            RaisePropertyChanged(nameof(RunningCount));
        }

        /// <summary>
        /// 单个摄像头属性变化回调。
        /// 状态变化时同时刷新运行数量统计和"启动"命令可用状态。
        /// </summary>
        private void OnCameraItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CameraItem.Status))
            {
                RaisePropertyChanged(nameof(RunningCount));
                // 摄像头状态变更（如 Running→Stopped）需刷新启动按钮可用状态
                StartSelectedCommand?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 通过文件选择器添加一个视频源，并自动启动检测。
        /// </summary>
        private void AddVideoSource()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.avi;*.mov;*.mkv|所有文件|*.*",
                Title = "选择视频文件作为摄像头源"
            };
            if (dlg.ShowDialog() != true) return;

            var camera = new CameraItem
            {
                Name = $"模拟摄像头 {cameraIndex++}",
                SourcePath = dlg.FileName,
                Status = CameraStatus.Stopped,
                StatusText = "未启动",
                PauseButtonText = "暂停",
                LastUpdateTime = "-"
            };
            Cameras.Add(camera);
            SelectedCamera = camera;
            StartCamera(camera);
        }

        /// <summary>
        /// 移除当前选中的摄像头项（包含停止后台任务）。
        /// </summary>
        private void RemoveSelected()
        {
            if (SelectedCamera == null) return;
            StopCamera(SelectedCamera);
            Cameras.Remove(SelectedCamera);
            SelectedCamera = Cameras.FirstOrDefault();
        }

        /// <summary>
        /// 判断"移除摄像头"命令是否可执行。
        /// </summary>
        private bool CanRemoveSelected() => SelectedCamera != null;

        /// <summary>
        /// 切换选中摄像头的暂停状态，并更新按钮文案与状态文本。
        /// </summary>
        private void ToggleSelectedPause()
        {
            if (SelectedCamera == null) return;
            SelectedCamera.IsPaused = !SelectedCamera.IsPaused;
            SelectedCamera.PauseButtonText = SelectedCamera.IsPaused ? "继续" : "暂停";
            SelectedCamera.Status = SelectedCamera.IsPaused ? CameraStatus.Paused : CameraStatus.Running;
            SelectedCamera.StatusText = SelectedCamera.IsPaused ? "已暂停" : "运行中";
        }

        /// <summary>
        /// 判断"暂停/继续"命令是否可执行。
        /// </summary>
        private bool CanToggleSelected() => SelectedCamera != null;

        /// <summary>
        /// 启动当前选中的摄像头。
        /// </summary>
        private void StartSelected()
        {
            if (SelectedCamera != null) StartCamera(SelectedCamera);
        }

        /// <summary>
        /// 判断"启动摄像头"命令是否可执行。
        /// 排除已处于 Running 状态的摄像头，防止重复点击时新建通道被旧后台线程 TryComplete 关闭。
        /// </summary>
        private bool CanStartSelected() =>
            SelectedCamera != null && SelectedCamera.Status != CameraStatus.Running;

        /// <summary>
        /// 启动指定摄像头：
        /// 先停止旧任务，再初始化通道与三段异步循环任务。
        /// </summary>
        private void StartCamera(CameraItem camera)
        {
            StopCamera(camera);

            camera.Cts = new CancellationTokenSource();
            camera.IsPaused = false;
            camera.PauseButtonText = "暂停";
            camera.Status = CameraStatus.Running;
            camera.StatusText = "运行中";
            camera.AlertMessage = "状态正常";
            camera.IsAlert = false;

            var channelOpts = new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest };
            camera.DecodeChannel = Channel.CreateBounded<Mat>(channelOpts);
            camera.InferenceChannel = Channel.CreateBounded<CameraInferenceResult>(channelOpts);

            var token = camera.Cts.Token;

            // 保存任务引用，StopCamera 等任务结束后再 Dispose CTS，防止 FlushCameraEventAsync 被中断
            camera.DecodeTask    = Task.Run(() => CameraDecodeLoopAsync(camera, token), token);
            camera.InferenceTask = Task.Run(() => CameraInferenceLoopAsync(camera, token), token);
            camera.RenderTask    = Task.Run(() => CameraRenderLoopAsync(camera, token), token);

            // 附加异常日志回调，防止后台任务异常被静默吞掉
            _ = camera.DecodeTask.ContinueWith(
                t => _logger.Error($"摄像头 [{camera.Name}] 解码任务异常: {t.Exception?.GetBaseException().Message}"),
                TaskContinuationOptions.OnlyOnFaulted);
            _ = camera.InferenceTask.ContinueWith(
                t => _logger.Error($"摄像头 [{camera.Name}] 推理任务异常: {t.Exception?.GetBaseException().Message}"),
                TaskContinuationOptions.OnlyOnFaulted);
            _ = camera.RenderTask.ContinueWith(
                t => _logger.Error($"摄像头 [{camera.Name}] 渲染任务异常: {t.Exception?.GetBaseException().Message}"),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// 停止指定摄像头检测，取消令牌并关闭通道。
        /// 推理线程会在 finally 中自行强制结算 Active 事件后再退出。
        /// CTS 的 Dispose 延迟到所有后台任务完成后执行，防止 FlushCameraEventAsync 被中断丢数据。
        /// </summary>
        private void StopCamera(CameraItem camera)
        {
            if (camera.Cts != null)
            {
                var ctsToCancel = camera.Cts;
                camera.Cts = null;
                ctsToCancel.Cancel();

                // 等所有后台任务结束后再 Dispose CTS，防止 FlushCameraEventAsync 被 ObjectDisposedException 中断
                var tasks = new[] { camera.DecodeTask, camera.InferenceTask, camera.RenderTask }
                    .Where(t => t != null).ToArray();
                if (tasks.Length > 0)
                    Task.WhenAll(tasks!).ContinueWith(_ => ctsToCancel.Dispose());
                else
                    ctsToCancel.Dispose();
            }

            // 关闭 Channel，确保后台线程正常退出
            camera.DecodeChannel?.Writer.TryComplete();
            camera.InferenceChannel?.Writer.TryComplete();
            camera.Status = CameraStatus.Stopped;
            camera.StatusText = "已停止";
            camera.AlertMessage = "已停止";
            camera.IsAlert = false;
            _logger.Info($"摄像头 [{camera.Name}] 已停止");
        }

        /// <summary>
        /// 解码循环：按源帧率读取视频帧，更新原始画面并投递到推理通道。
        /// </summary>
        private async Task CameraDecodeLoopAsync(CameraItem camera, CancellationToken token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(camera.SourcePath) || !File.Exists(camera.SourcePath))
                {
                    UpdateCameraStatus(camera, CameraStatus.Error, "源文件不存在");
                    return;
                }

                using var capture = new VideoCapture(camera.SourcePath);
                if (!capture.IsOpened())
                {
                    UpdateCameraStatus(camera, CameraStatus.Error, "打开失败");
                    return;
                }

                var fps = capture.Fps;
                if (fps <= 0) fps = 25;
                var frameIntervalMs = (int)(1000.0 / fps);
                var sw = Stopwatch.StartNew();

                while (!token.IsCancellationRequested)
                {
                    if (camera.IsPaused) { await Task.Delay(50, token); continue; }

                    var t0 = sw.ElapsedMilliseconds;
                    var frame = new Mat();

                    if (!capture.Read(frame) || frame.Empty())
                    {
                        // 模拟摄像头：视频播完后循环重播
                        if (!capture.Set(VideoCaptureProperties.PosFrames, 0))
                            _logger.Warning($"摄像头 [{camera.Name}] 不支持循环播放，seek 失败");
                        frame.Dispose();
                        continue;
                    }

                    // 立即更新原始画面
                    var originalBmp = MatToWriteableBitmap(frame);
                    await App.Current.Dispatcher.InvokeAsync(() => camera.OriginalFrame = originalBmp);

                    // 发给推理线程
                    if (!camera.DecodeChannel.Writer.TryWrite(frame))
                        frame.Dispose();

                    var elapsed = (int)(sw.ElapsedMilliseconds - t0);
                    var sleep = frameIntervalMs - elapsed;
                    if (sleep > 0) await Task.Delay(sleep, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                UpdateCameraStatus(camera, CameraStatus.Error, $"解码错误: {ex.Message}");
            }
            finally
            {
                camera.DecodeChannel?.Writer.TryComplete();
            }
        }

        /// <summary>
        /// 推理循环：消费解码帧，执行 YOLO 检测，驱动该摄像头的连续帧状态机，事件结束时落库并报警。
        /// 状态机变量完全在本方法内维护，每路摄像头独立，无跨线程共享。
        /// </summary>
        private async Task CameraInferenceLoopAsync(CameraItem camera, CancellationToken token)
        {
            var fpsWindow = new Queue<long>(10);
            var sw = Stopwatch.StartNew();

            // ── 连续帧状态机变量（每摄像头独立，仅在本推理线程访问）──────────
            long  frameIndex        = 0;
            int   consecutiveHits   = 0;
            int   consecutiveMisses = 0;
            bool  eventActive       = false;
            float eventPeakConf     = 0f;  // 事件内峰值置信度，落库时作为 MaxConfidence
            long  cooldownUntilMs   = 0;
            // 捕获阈值快照（推理开始时固定，避免同一摄像头推理过程中阈值漂移）
            float confidenceSnapshot = ConfidenceThreshold;
            float alertSnapshot      = AlertThreshold;

            try
            {
                await foreach (var frame in camera.DecodeChannel.Reader.ReadAllAsync(token))
                {
                    try
                    {
                        var t0 = sw.ElapsedMilliseconds;
                        frameIndex++;

                        using var imageSharp = MatToImageSharp(frame);
                        var result = FireDetectionModule.Detect(imageSharp);
                        using var plotted = result.PlotImage(imageSharp) as SixLabors.ImageSharp.Image<Rgba32>;

                        if (plotted == null)
                            _logger.Warning($"摄像头 [{camera.Name}] PlotImage 返回 null，使用原始图像");

                        var elapsed = sw.ElapsedMilliseconds - t0;
                        fpsWindow.Enqueue(elapsed);
                        if (fpsWindow.Count > 10) fpsWindow.Dequeue();
                        var avgMs = fpsWindow.Average();
                        var fps = avgMs > 0 ? 1000.0 / avgMs : 0;

                        var detBmp = ImageSharpToWriteableBitmap(plotted ?? imageSharp);
                        var infos = BuildDetectionInfos(result, confidenceSnapshot);
                        var maxConf = infos.Count > 0 ? infos.Max(i => i.Confidence) : 0f;

                        camera.InferenceChannel.Writer.TryWrite(new CameraInferenceResult
                        {
                            DetectionImage        = detBmp,
                            Infos                 = infos,
                            InferenceTimeMs       = elapsed,
                            Fps                   = fps,
                            // 将推理时使用的阈值快照一并传给渲染线程，保持 UI 预警与 DB 记录一致
                            AlertThresholdSnapshot = alertSnapshot
                        });

                        // ── 连续帧状态机 ────────────────────────────────────
                        var nowMs = sw.ElapsedMilliseconds;
                        var isHit = infos.Count > 0 && maxConf >= alertSnapshot;

                        if (isHit && nowMs > cooldownUntilMs)
                        {
                            consecutiveHits++;
                            consecutiveMisses = 0;

                            // 连续命中达到阈值 N，激活事件
                            if (!eventActive && consecutiveHits >= HIT_THRESHOLD_N)
                            {
                                eventActive   = true;
                                eventPeakConf = 0f;
                                _logger.Info($"摄像头 [{camera.Name}] 事件激活：帧 {frameIndex}，阈值 {alertSnapshot:P1}");
                            }

                            // 事件进行中，持续追踪峰值置信度
                            if (eventActive)
                            {
                                if (maxConf > eventPeakConf) eventPeakConf = maxConf;
                            }
                        }
                        else if (eventActive)
                        {
                            consecutiveMisses++;
                            if (consecutiveMisses >= MISS_THRESHOLD_M)
                            {
                                await FlushCameraEventAsync(camera, eventPeakConf);

                                eventActive       = false;
                                consecutiveHits   = 0;
                                consecutiveMisses = 0;
                                cooldownUntilMs   = nowMs + COOLDOWN_MS;
                            }
                        }
                        else
                        {
                            // Idle 状态或冷却期内——无论命中与否都重置连续计数。
                            // 关键：冷却期内命中帧也须重置，防止冷却结束后仅需 1 帧即触发误报
                            consecutiveHits   = 0;
                            consecutiveMisses = 0;
                        }
                    }
                    finally
                    {
                        frame.Dispose();
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Error($"摄像头 [{camera.Name}] 推理异常: {ex.Message}", ex);
            }
            finally
            {
                // 摄像头停止时若有 Active 事件，强制结算落库
                if (eventActive)
                {
                    await FlushCameraEventAsync(camera, eventPeakConf);
                }
                camera.InferenceChannel?.Writer.TryComplete();
            }
        }

        /// <summary>
        /// 结算并落库一次摄像头检测事件。
        /// 写入 HandleStatus = "Pending" 表示待处置，并自动刷新待处置列表。
        /// </summary>
        /// <param name="camera">触发事件的摄像头上下文</param>
        /// <param name="peakConf">事件期间检测到的峰值置信度</param>
        private async Task FlushCameraEventAsync(CameraItem camera, float peakConf)
        {
            try
            {
                var eventId = Guid.NewGuid().ToString("N");

                var record = new DetectionRecord
                {
                    DetectionTime    = DateTime.Now,
                    SourceType       = "Camera",
                    SourcePath       = camera.Name,
                    IsFireDetected   = true,
                    MaxConfidence    = peakConf,
                    IsAlarmTriggered = true,
                    UserId           = LoginViewModel.CurrentUser?.Id,
                    EventId          = eventId,
                    // 摄像头报警写入 Pending 状态，等待值班员确认处置
                    HandleStatus     = "Pending"
                };

                var recordId = await _dbService.SaveDetectionRecordAsync(record);
                record.Id = recordId;
                _logger.Info($"摄像头 [{camera.Name}] 事件落库: ID={recordId}, EventId={eventId}, 峰值 {peakConf:P1}");

                await _alarmService.TriggerAlarmAsync(record);

                // 落库后在 UI 线程刷新待处置列表，让值班员第一时间看到新报警
                await App.Current.Dispatcher.InvokeAsync(async () =>
                    await RefreshPendingAlarmsAsync());
            }
            catch (Exception ex)
            {
                _logger.Error($"摄像头 [{camera.Name}] 事件落库失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 渲染循环：将推理结果回写到 UI 绑定属性，并更新预警状态。
        /// </summary>
        private async Task CameraRenderLoopAsync(CameraItem camera, CancellationToken token)
        {
            try
            {
                await foreach (var ir in camera.InferenceChannel.Reader.ReadAllAsync(token))
                {
                    // 应用关闭时 App.Current 可能变为 null，跳过已无效的 Dispatcher 调用
                    var dispatcher = App.Current?.Dispatcher;
                    if (dispatcher == null) break;

                    await dispatcher.InvokeAsync(() =>
                    {
                        camera.DetectionFrame = ir.DetectionImage;
                        camera.DetectionInfos = new ObservableCollection<DetectionInfo>(ir.Infos);
                        camera.DetectionCount = ir.Infos.Count;
                        camera.MaxConfidence = ir.Infos.Count > 0 ? ir.Infos.Max(i => i.Confidence) : 0f;
                        camera.InferenceTimeMs = ir.InferenceTimeMs;
                        camera.DetectionFps = ir.Fps;
                        camera.LastUpdateTime = DateTime.Now.ToString("HH:mm:ss.fff");

                        // 使用推理阶段的阈值快照，防止用户实时修改阈值导致 UI 预警与 DB 记录不一致
                        var isAlert = ir.Infos.Count > 0 && camera.MaxConfidence >= ir.AlertThresholdSnapshot;
                        camera.IsAlert = isAlert;
                        camera.AlertMessage = isAlert
                            ? $"预警：检测到目标，最高置信度 {camera.MaxConfidence:P1}"
                            : "状态正常";
                    });
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 从数据库加载所有 Pending 状态的摄像头报警，刷新 PendingAlarms 集合。
        /// 在 UI 线程调用（Dispatcher.InvokeAsync 内），或调用前已在 UI 线程。
        /// </summary>
        private async Task RefreshPendingAlarmsAsync()
        {
            try
            {
                var list = await _dbService.GetPendingCameraAlarmsAsync();
                PendingAlarms = new ObservableCollection<DetectionRecord>(list);
            }
            catch (Exception ex)
            {
                _logger.Error($"刷新待处置列表失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 将当前选中的报警更新为用户选择的处置状态，并刷新待处置列表。
        /// 由 ConfirmHandleCommand 驱动。
        /// </summary>
        private async Task ConfirmHandleAsync()
        {
            if (SelectedAlarm == null || string.IsNullOrEmpty(SelectedHandleStatus)) return;

            try
            {
                var operatorId = LoginViewModel.CurrentUser?.Id;
                // 将中文选项映射为 DB 存储的英文枚举值，匹配失败则直接使用原值作为兜底
                var dbStatus = HandleStatusMap.TryGetValue(SelectedHandleStatus, out var mapped)
                    ? mapped : SelectedHandleStatus;
                await _dbService.UpdateHandleStatusAsync(
                    SelectedAlarm.Id,
                    dbStatus,
                    operatorId,
                    string.IsNullOrWhiteSpace(HandleNotes) ? null : HandleNotes);

                _logger.Info($"处置报警 ID={SelectedAlarm.Id}，状态={SelectedHandleStatus}，操作人={operatorId}");

                // 处置完成后清空备注，刷新列表
                HandleNotes = string.Empty;
                SelectedAlarm = null;
                await RefreshPendingAlarmsAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"处置报警失败: {ex.Message}", ex);
                MessageBox.Show($"处置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 统一更新摄像头状态（在 UI 线程执行）。
        /// </summary>
        private void UpdateCameraStatus(CameraItem camera, CameraStatus status, string text)
        {
            App.Current.Dispatcher.InvokeAsync(() =>
            {
                camera.Status = status;
                camera.StatusText = text;
                camera.AlertMessage = status == CameraStatus.Error ? $"错误：{text}" : camera.AlertMessage;
                camera.IsAlert = status == CameraStatus.Error;
            });
        }

        /// <summary>
        /// 推理阶段传递给渲染阶段的中间数据对象。
        /// </summary>
        internal sealed class CameraInferenceResult
        {
            /// <summary>
            /// 已绘制检测框的图像。
            /// </summary>
            public ImageSource DetectionImage { get; set; }

            /// <summary>
            /// 当前帧检测详情集合。
            /// </summary>
            public IReadOnlyList<DetectionInfo> Infos { get; set; }

            /// <summary>
            /// 当前帧推理耗时（毫秒）。
            /// </summary>
            public long InferenceTimeMs { get; set; }

            /// <summary>
            /// 当前检测帧率（滑动窗口估算）。
            /// </summary>
            public double Fps { get; set; }

            /// <summary>
            /// 推理时使用的预警阈值快照。
            /// 传递给渲染线程，确保 UI 预警状态与数据库记录判断逻辑一致，
            /// 避免用户实时调整阈值时出现"UI 无警报但 DB 有告警"的矛盾。
            /// </summary>
            public float AlertThresholdSnapshot { get; set; }
        }

        /// <summary>
        /// OpenCV Mat（BGR24）转 WriteableBitmap。
        /// 采用直接内存拷贝，减少图像编解码开销。
        /// </summary>
        private static WriteableBitmap MatToWriteableBitmap(Mat mat)
        {
            var w = mat.Width;
            var h = mat.Height;
            var bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr24, null);
            bitmap.Lock();
            try
            {
                unsafe
                {
                    Buffer.MemoryCopy(
                        (void*)mat.DataPointer,
                        (void*)bitmap.BackBuffer,
                        (long)bitmap.BackBufferStride * h,
                        mat.Step() * h);
                }
                bitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
            }
            finally { bitmap.Unlock(); }
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// ImageSharp RGBA 图像转 WriteableBitmap（BGRA32）。
        /// 逐像素写入 BackBuffer，避免额外编码损耗。
        /// </summary>
        private static WriteableBitmap ImageSharpToWriteableBitmap(SixLabors.ImageSharp.Image<Rgba32> image)
        {
            var w = image.Width;
            var h = image.Height;
            var bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            bitmap.Lock();
            try
            {
                unsafe
                {
                    var ptr = (byte*)bitmap.BackBuffer;
                    var stride = bitmap.BackBufferStride;
                    image.ProcessPixelRows(accessor =>
                    {
                        for (int y = 0; y < h; y++)
                        {
                            var row = accessor.GetRowSpan(y);
                            var dst = ptr + y * stride;
                            for (int x = 0; x < w; x++)
                            {
                                var p = row[x];
                                dst[x * 4 + 0] = p.B;
                                dst[x * 4 + 1] = p.G;
                                dst[x * 4 + 2] = p.R;
                                dst[x * 4 + 3] = p.A;
                            }
                        }
                    });
                }
                bitmap.AddDirtyRect(new Int32Rect(0, 0, w, h));
            }
            finally { bitmap.Unlock(); }
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// OpenCV Mat（BGR）转 ImageSharp（RGBA）。
        /// 用于对接 YOLO 推理输入类型。
        /// </summary>
        private static SixLabors.ImageSharp.Image<Rgba32> MatToImageSharp(Mat mat)
        {
            using var rgba = new Mat();
            Cv2.CvtColor(mat, rgba, ColorConversionCodes.BGR2RGBA);
            var byteLen = rgba.Rows * rgba.Cols * rgba.ElemSize();
            var buf = new byte[byteLen];
            Marshal.Copy(rgba.Data, buf, 0, byteLen);
            return SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(buf, rgba.Cols, rgba.Rows);
        }

        /// <summary>
        /// 从检测结果提取检测条目，按阈值过滤并按置信度降序保留前 50 条。
        /// </summary>
        private static IReadOnlyList<DetectionInfo> BuildDetectionInfos(object result, float threshold)
        {
            var list = new List<DetectionInfo>();
            foreach (var det in EnumerateDetections(result))
            {
                if (det == null) continue;
                var acc = DetectionAccessor.Get(det.GetType());
                var conf = acc.GetConfidence(det);
                if (conf < threshold) continue;
                list.Add(new DetectionInfo { Label = acc.GetLabel(det), Confidence = conf, Box = acc.GetBox(det) });
            }
            return list.OrderByDescending(i => i.Confidence).Take(50).ToList();
        }

        /// <summary>
        /// 统一枚举检测项：
        /// 支持"结果自身可枚举"与"结果对象含集合属性"两种返回结构。
        /// </summary>
        private static IEnumerable<object> EnumerateDetections(object result)
        {
            if (result is System.Collections.IEnumerable en)
            {
                foreach (var item in en)
                    if (item != null) yield return item;
                yield break;
            }
            var prop = DetectionAccessor.GetDetectionsProperty(result.GetType());
            if (prop?.GetValue(result) is System.Collections.IEnumerable list)
                foreach (var item in list)
                    if (item != null) yield return item;
        }

        /// <summary>
        /// 检测对象反射访问器。
        /// 通过候选字段名兼容不同版本推理库的属性命名差异。
        /// </summary>
        private sealed class DetectionAccessor
        {
            /// <summary>
            /// 置信度候选字段名。
            /// </summary>
            private static readonly string[] ConfidenceNames = { "Confidence", "Probability", "Score" };

            /// <summary>
            /// 标签候选字段名。
            /// </summary>
            private static readonly string[] LabelNames = { "Label", "Name", "ClassName", "Category", "Class" };

            /// <summary>
            /// 类别 ID 候选字段名。
            /// </summary>
            private static readonly string[] ClassIdNames = { "ClassId", "ClassIndex", "Class" };

            /// <summary>
            /// 边界框候选字段名。
            /// </summary>
            private static readonly string[] BoxNames = { "BoundingBox", "Box", "Rect", "Rectangle", "Bbox" };
            private static readonly string[] BoxXNames = { "X", "Left" };
            private static readonly string[] BoxYNames = { "Y", "Top" };
            private static readonly string[] BoxWNames = { "Width" };
            private static readonly string[] BoxHNames = { "Height" };
            private static readonly string[] BoxRNames = { "Right" };
            private static readonly string[] BoxBNames = { "Bottom" };

            /// <summary>
            /// 按检测类型缓存访问器，降低反射开销。
            /// </summary>
            private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, DetectionAccessor> Cache = new();

            /// <summary>
            /// 按结果类型缓存检测集合属性。
            /// </summary>
            private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyInfo> DetPropCache = new();

            /// <summary>
            /// 当前类型反射到的关键属性句柄。
            /// </summary>
            private readonly PropertyInfo? _conf, _label, _classId, _box;

            /// <summary>
            /// 私有构造：初始化当前类型属性映射。
            /// </summary>
            private DetectionAccessor(Type t)
            {
                _conf = Find(t, ConfidenceNames);
                _label = Find(t, LabelNames);
                _classId = Find(t, ClassIdNames);
                _box = Find(t, BoxNames);
            }

            /// <summary>
            /// 获取指定类型的访问器（带缓存）。
            /// </summary>
            public static DetectionAccessor Get(Type t) => Cache.GetOrAdd(t, x => new DetectionAccessor(x));

            /// <summary>
            /// 获取结果对象中承载检测集合的属性。
            /// 优先匹配常见命名，失败时回退到首个可枚举属性。
            /// </summary>
            public static PropertyInfo? GetDetectionsProperty(Type t)
            {
                if (DetPropCache.TryGetValue(t, out var c)) return c;
                foreach (var name in new[] { "Detections", "Predictions", "Boxes", "Items", "Results", "Outputs" })
                {
                    var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(string))
                    { DetPropCache[t] = p; return p; }
                }
                var fb = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(string));
                if (fb != null) DetPropCache[t] = fb;
                return fb;
            }

            /// <summary>
            /// 读取置信度，失败时返回 0。
            /// </summary>
            public float GetConfidence(object det) => ToFloat(_conf?.GetValue(det));

            /// <summary>
            /// 读取标签文本，失败时回退到 ClassId/Unknown。
            /// </summary>
            public string GetLabel(object det)
            {
                var lbl = ExtractLabel(_label?.GetValue(det));
                if (!string.IsNullOrWhiteSpace(lbl)) return lbl;
                var cid = _classId?.GetValue(det);
                return cid != null ? $"Class {cid}" : "Unknown";
            }

            /// <summary>
            /// 读取并格式化边界框字符串，兼容 xywh 与 xyxy 两种表示。
            /// </summary>
            public string GetBox(object det)
            {
                var box = _box?.GetValue(det);
                if (box == null) return "-";
                var bt = box.GetType();
                var x = Find(bt, BoxXNames); var y = Find(bt, BoxYNames);
                var w = Find(bt, BoxWNames); var h = Find(bt, BoxHNames);
                var r = Find(bt, BoxRNames); var b = Find(bt, BoxBNames);
                if (x != null && y != null && w != null && h != null)
                    return $"{ToFloat(x.GetValue(box)):0},{ToFloat(y.GetValue(box)):0} {ToFloat(w.GetValue(box)):0}x{ToFloat(h.GetValue(box)):0}";
                if (x != null && y != null && r != null && b != null)
                    return $"{ToFloat(x.GetValue(box)):0},{ToFloat(y.GetValue(box)):0} -> {ToFloat(r.GetValue(box)):0},{ToFloat(b.GetValue(box)):0}";
                return box.ToString() ?? "-";
            }

            /// <summary>
            /// 按候选名称顺序查找公共实例属性。
            /// </summary>
            private static PropertyInfo? Find(Type t, string[] names)
            {
                foreach (var n in names) { var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance); if (p != null) return p; }
                return null;
            }

            /// <summary>
            /// 提取标签文本，兼容 string/Name/Text 等结构。
            /// </summary>
            private static string? ExtractLabel(object? v)
            {
                if (v == null) return null;
                if (v is string s) return s;
                var np = v.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
                    ?? v.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
                return np != null ? np.GetValue(v)?.ToString() : v.ToString();
            }

            /// <summary>
            /// 安全转 float，失败返回 0。
            /// </summary>
            private static float ToFloat(object? v) => v switch
            {
                null => 0f, float f => f, double d => (float)d, decimal m => (float)m,
                int i => i, long l => l, short s => s, byte b => b,
                _ => float.TryParse(v.ToString(), out var r) ? r : 0f
            };
        }

        /// <summary>
        /// UI 检测列表项模型。
        /// </summary>
        public class DetectionInfo
        {
            /// <summary>
            /// 检测标签名称。
            /// </summary>
            public string Label { get; set; }

            /// <summary>
            /// 检测置信度。
            /// </summary>
            public float Confidence { get; set; }

            /// <summary>
            /// 边界框文本信息。
            /// </summary>
            public string Box { get; set; }
        }

        /// <summary>
        /// 单路摄像头上下文模型。
        /// 包含运行状态、展示数据和后台任务资源引用。
        /// </summary>
        public class CameraItem : BindableBase
        {
            /// <summary>
            /// 摄像头名称。
            /// </summary>
            private string name;

            /// <summary>
            /// 摄像头名称绑定属性。
            /// </summary>
            public string Name { get => name; set => SetProperty(ref name, value); }

            /// <summary>
            /// 视频源文件路径。
            /// </summary>
            private string sourcePath;

            /// <summary>
            /// 视频源路径绑定属性。
            /// </summary>
            public string SourcePath { get => sourcePath; set => SetProperty(ref sourcePath, value); }

            /// <summary>
            /// 摄像头状态值。
            /// </summary>
            private CameraStatus status;

            /// <summary>
            /// 摄像头状态绑定属性。
            /// </summary>
            public CameraStatus Status { get => status; set => SetProperty(ref status, value); }

            /// <summary>
            /// 状态文本。
            /// </summary>
            private string statusText;

            /// <summary>
            /// 状态文本绑定属性。
            /// </summary>
            public string StatusText { get => statusText; set => SetProperty(ref statusText, value); }

            /// <summary>
            /// 是否暂停读取帧。
            /// </summary>
            private bool isPaused;

            /// <summary>
            /// 暂停状态绑定属性。
            /// </summary>
            public bool IsPaused { get => isPaused; set => SetProperty(ref isPaused, value); }

            /// <summary>
            /// 暂停按钮文本。
            /// </summary>
            private string pauseButtonText;

            /// <summary>
            /// 暂停按钮文本绑定属性。
            /// </summary>
            public string PauseButtonText { get => pauseButtonText; set => SetProperty(ref pauseButtonText, value); }

            /// <summary>
            /// 原始视频帧图像。
            /// </summary>
            private ImageSource originalFrame;

            /// <summary>
            /// 原始帧绑定属性。
            /// </summary>
            public ImageSource OriginalFrame { get => originalFrame; set => SetProperty(ref originalFrame, value); }

            /// <summary>
            /// 检测结果图像。
            /// </summary>
            private ImageSource detectionFrame;

            /// <summary>
            /// 检测结果图像绑定属性。
            /// </summary>
            public ImageSource DetectionFrame { get => detectionFrame; set => SetProperty(ref detectionFrame, value); }

            /// <summary>
            /// 当前帧检测详情列表。
            /// </summary>
            private ObservableCollection<DetectionInfo> detectionInfos = new();

            /// <summary>
            /// 检测详情列表绑定属性。
            /// </summary>
            public ObservableCollection<DetectionInfo> DetectionInfos { get => detectionInfos; set => SetProperty(ref detectionInfos, value); }

            /// <summary>
            /// 当前帧目标数量。
            /// </summary>
            private int detectionCount;

            /// <summary>
            /// 目标数量绑定属性。
            /// </summary>
            public int DetectionCount { get => detectionCount; set => SetProperty(ref detectionCount, value); }

            /// <summary>
            /// 当前帧最高置信度。
            /// </summary>
            private float maxConfidence;

            /// <summary>
            /// 最高置信度绑定属性。
            /// </summary>
            public float MaxConfidence { get => maxConfidence; set => SetProperty(ref maxConfidence, value); }

            /// <summary>
            /// 最近一次刷新时间。
            /// </summary>
            private string lastUpdateTime;

            /// <summary>
            /// 最近刷新时间绑定属性。
            /// </summary>
            public string LastUpdateTime { get => lastUpdateTime; set => SetProperty(ref lastUpdateTime, value); }

            /// <summary>
            /// 是否处于预警状态。
            /// </summary>
            private bool isAlert;

            /// <summary>
            /// 预警状态绑定属性。
            /// </summary>
            public bool IsAlert { get => isAlert; set => SetProperty(ref isAlert, value); }

            /// <summary>
            /// 预警消息文本。
            /// </summary>
            private string alertMessage;

            /// <summary>
            /// 预警消息绑定属性。
            /// </summary>
            public string AlertMessage { get => alertMessage; set => SetProperty(ref alertMessage, value); }

            /// <summary>
            /// 单帧推理耗时（毫秒）。
            /// </summary>
            private long inferenceTimeMs;

            /// <summary>
            /// 推理耗时绑定属性。
            /// </summary>
            public long InferenceTimeMs { get => inferenceTimeMs; set => SetProperty(ref inferenceTimeMs, value); }

            /// <summary>
            /// 检测帧率（FPS）。
            /// </summary>
            private double detectionFps;

            /// <summary>
            /// 检测 FPS 绑定属性。
            /// </summary>
            public double DetectionFps { get => detectionFps; set => SetProperty(ref detectionFps, value); }

            /// <summary>
            /// 当前检测任务取消令牌源。
            /// </summary>
            public CancellationTokenSource? Cts { get; set; }

            /// <summary>
            /// 解码帧通道（容量=1，拥塞时丢弃旧帧）。
            /// </summary>
            public Channel<Mat>? DecodeChannel { get; set; }

            /// <summary>
            /// 推理结果通道（容量=1，优先保留最新结果）。
            /// </summary>
            public Channel<CameraInferenceResult>? InferenceChannel { get; set; }

            /// <summary>
            /// 解码后台任务引用，用于等待任务结束后再 Dispose CTS。
            /// </summary>
            public Task? DecodeTask { get; set; }

            /// <summary>
            /// 推理后台任务引用。
            /// </summary>
            public Task? InferenceTask { get; set; }

            /// <summary>
            /// 渲染后台任务引用。
            /// </summary>
            public Task? RenderTask { get; set; }
        }

        /// <summary>
        /// 摄像头运行状态枚举。
        /// </summary>
        public enum CameraStatus { Stopped, Running, Paused, Error }
    }
}
