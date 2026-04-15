using Compunet.YoloSharp.Plotting;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using FireDetectionSystem.Core;

namespace FireDetectionSystem.ViewModels
{
    /// <summary>
    /// 视频检测页面 ViewModel。
    /// 采用“解码 -> 推理 -> 渲染”三段式异步流水线：
    /// 1) 解码线程负责读取视频帧并更新原始画面；
    /// 2) 推理线程负责 YOLO 检测与结果后处理；
    /// 3) 渲染线程负责将检测结果同步到 UI。
    /// </summary>
    class VideoDetectionViewModel : BindableBase
    {
        /// <summary>
        /// 当前选中的视频文件路径。
        /// 变更后会主动刷新“开始检测”按钮可用状态。
        /// </summary>
        private string videoPath;

        /// <summary>
        /// 当前视频文件路径。
        /// </summary>
        public string VideoPath
        {
            get { return videoPath; }
            set
            {
                if (videoPath == value) return;
                videoPath = value;
                RaisePropertyChanged();
                DetectionVideoCommand?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 检测结果过滤置信度阈值（仅用于 UI 结果显示过滤）。
        /// </summary>
        private float confidence = 0.5f;

        /// <summary>
        /// 结果显示置信度阈值，范围通常为 0~1。
        /// </summary>
        public float Confidence
        {
            get { return confidence; }
            set { confidence = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 左侧原始视频帧图像（未绘制检测框）。
        /// </summary>
        private ImageSource originalImage;

        /// <summary>
        /// 原始视频帧绑定属性。
        /// </summary>
        public ImageSource OriginalImage
        {
            get { return originalImage; }
            set { originalImage = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 右侧检测结果图像（已绘制检测框）。
        /// </summary>
        private ImageSource detetionSoure;

        /// <summary>
        /// 检测结果图像绑定属性。
        /// </summary>
        public ImageSource DetetionSoure
        {
            get { return detetionSoure; }
            set { detetionSoure = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 当前帧检测详情列表（标签、置信度、位置）。
        /// </summary>
        private ObservableCollection<DetectionInfo> detectionInfos = new ObservableCollection<DetectionInfo>();

        /// <summary>
        /// 检测详情绑定集合。
        /// </summary>
        public ObservableCollection<DetectionInfo> DetectionInfos
        {
            get { return detectionInfos; }
            set { detectionInfos = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 当前帧检测到的目标数量。
        /// </summary>
        private int detectionCount;

        /// <summary>
        /// 检测目标数量绑定属性。
        /// </summary>
        public int DetectionCount
        {
            get { return detectionCount; }
            set { detectionCount = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 当前帧最高置信度。
        /// </summary>
        private float maxConfidence;

        /// <summary>
        /// 最高置信度绑定属性。
        /// </summary>
        public float MaxConfidence
        {
            get { return maxConfidence; }
            set { maxConfidence = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 最后一次 UI 刷新时间（用于界面显示）。
        /// </summary>
        private string lastUpdateTime = "-";

        /// <summary>
        /// 最后更新时间绑定属性。
        /// </summary>
        public string LastUpdateTime
        {
            get { return lastUpdateTime; }
            set { lastUpdateTime = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 是否处于暂停状态。
        /// 暂停只会阻止解码环节继续读取新帧，不会销毁流水线。
        /// </summary>
        private bool isPaused;

        /// <summary>
        /// 暂停状态绑定属性。
        /// </summary>
        public bool IsPaused
        {
            get { return isPaused; }
            set { isPaused = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 暂停按钮显示文本（暂停/继续）。
        /// </summary>
        private string pauseButtonText = "暂停";

        /// <summary>
        /// 暂停按钮文本绑定属性。
        /// </summary>
        public string PauseButtonText
        {
            get { return pauseButtonText; }
            set { pauseButtonText = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 单帧推理耗时（毫秒）。
        /// </summary>
        private double inferenceTimeMs;

        /// <summary>
        /// 推理耗时绑定属性。
        /// </summary>
        public double InferenceTimeMs
        {
            get { return inferenceTimeMs; }
            set { inferenceTimeMs = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 检测帧率（按滑动窗口估算）。
        /// </summary>
        private double detectionFps;

        /// <summary>
        /// 检测 FPS 绑定属性。
        /// </summary>
        public double DetectionFps
        {
            get { return detectionFps; }
            set { detectionFps = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 选择视频文件命令。
        /// </summary>
        public DelegateCommand SelectVideoPathCommnad { get; }

        /// <summary>
        /// 开始检测命令。
        /// </summary>
        public DelegateCommand DetectionVideoCommand { get; }

        /// <summary>
        /// 停止检测命令。
        /// </summary>
        public DelegateCommand StopDetectionCommand { get; }

        /// <summary>
        /// 检测任务取消令牌源。
        /// 用于统一取消解码、推理、渲染三个后台任务。
        /// </summary>
        private CancellationTokenSource _cts;

        /// <summary>
        /// 解码通道：承载 Mat 帧数据（容量为 1，拥塞时丢弃旧帧）。
        /// </summary>
        private Channel<Mat> _decodeChannel;

        /// <summary>
        /// 推理结果通道：承载可直接渲染到 UI 的检测结果。
        /// </summary>
        private Channel<InferenceResult> _inferenceChannel;

        /// <summary>
        /// 当前是否处于检测中。
        /// </summary>
        private bool _isDetecting;

        /// <summary>
        /// 检测运行状态绑定属性。
        /// 变更时会刷新“开始/停止”命令的可用状态。
        /// </summary>
        public bool IsDetecting
        {
            get { return _isDetecting; }
            set
            {
                if (_isDetecting == value) return;
                _isDetecting = value;
                RaisePropertyChanged();
                DetectionVideoCommand?.RaiseCanExecuteChanged();
                StopDetectionCommand?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 构造函数：初始化命令并绑定可执行条件。
        /// </summary>
        public VideoDetectionViewModel()
        {
            SelectVideoPathCommnad = new DelegateCommand(SelectVideoPath);
            DetectionVideoCommand = new DelegateCommand(DetectionVideo, CanStartDetection)
                .ObservesProperty(() => VideoPath);
            StopDetectionCommand = new DelegateCommand(StopDetection, CanStopDetection);
        }

        /// <summary>
        /// 打开文件选择框，选择待检测视频。
        /// </summary>
        private void SelectVideoPath()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.avi;*.mov;*.mkv|所有文件|*.*",
                Title = "选择视频文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                VideoPath = openFileDialog.FileName;
            }
        }

        /// <summary>
        /// 判断“开始检测”命令是否可执行。
        /// 仅当：未在检测中 + 视频路径有效时返回 true。
        /// </summary>
        private bool CanStartDetection()
        {
            // 选择了有效视频且当前不在检测中时，允许开始。
            return !IsDetecting
                && !string.IsNullOrWhiteSpace(VideoPath)
                && File.Exists(VideoPath);
        }
        

        /// <summary>
        /// 判断“停止检测”命令是否可执行。
        /// </summary>
        private bool CanStopDetection()
        {
            return IsDetecting;
        }
        

        /// <summary>
        /// 停止检测流程：
        /// 1) 取消后台任务；
        /// 2) 释放取消令牌；
        /// 3) 复位 UI 状态。
        /// </summary>
        private void StopDetection()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            IsDetecting = false;
            IsPaused = false;
            PauseButtonText = "暂停";
        }

        /// <summary>
        /// 启动检测流程：
        /// 1) 初始化取消令牌与运行状态；
        /// 2) 初始化两个有界通道；
        /// 3) 启动解码/推理/渲染三段后台任务。
        /// </summary>
        private void DetectionVideo()
        {
            if (!CanStartDetection())
                return;

            _cts = new CancellationTokenSource();
            IsDetecting = true;
            IsPaused = false;
            PauseButtonText = "暂停";

            var channelOpts = new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest };
            _decodeChannel = Channel.CreateBounded<Mat>(channelOpts);
            _inferenceChannel = Channel.CreateBounded<InferenceResult>(channelOpts);

            var token = _cts.Token;
            _ = Task.Run(() => DecodeLoopAsync(token), token);
            _ = Task.Run(() => InferenceLoopAsync(token), token);
            _ = Task.Run(() => RenderLoopAsync(token), token);

        }

        /// <summary>
        /// 视频解码循环。
        /// 职责：按原始帧率读取视频帧，更新左侧原图，并将帧发送到推理通道。
        /// </summary>
        private async Task DecodeLoopAsync(CancellationToken token)
        {
            try
            {
                using var capture = new VideoCapture(VideoPath);
                if (!capture.IsOpened())
                {
                    await App.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show("无法打开视频文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        StopDetection();
                    });
                    return;
                }

                var fps = capture.Fps;
                if (fps <= 0) fps = 25;
                var frameIntervalMs = (int)(1000.0 / fps);
                var sw = Stopwatch.StartNew();

                while (!token.IsCancellationRequested)
                {
                    if (IsPaused) { await Task.Delay(50, token); continue; }

                    var frameStart = sw.ElapsedMilliseconds;
                    var frame = new Mat();
                    if (!capture.Read(frame) || frame.Empty())
                    {
                        frame.Dispose();
                        // 视频读取结束后自动停止检测。
                        await App.Current.Dispatcher.InvokeAsync(() => StopDetection());
                        break;
                    }

                    // 先更新原始画面，保证用户能实时看到视频流。
                    var originalBmp = MatToWriteableBitmap(frame);
                    await App.Current.Dispatcher.InvokeAsync(() => OriginalImage = originalBmp);

                    // 将帧写入推理通道；若拥塞则丢弃当前帧，避免积压导致延迟不断增加。
                    if (!_decodeChannel.Writer.TryWrite(frame))
                        frame.Dispose();

                    // 按源视频 FPS 进行节奏控制，避免无意义“超速解码”占用 CPU。
                    var elapsed = (int)(sw.ElapsedMilliseconds - frameStart);
                    var sleep = frameIntervalMs - elapsed;
                    if (sleep > 0) await Task.Delay(sleep, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"视频解码错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    StopDetection();
                });
            }
            finally
            {
                _decodeChannel?.Writer.TryComplete();
            }
        }

        /// <summary>
        /// 推理循环。
        /// 职责：消费解码帧、执行 YOLO 检测、绘制检测框、构建检测信息并输出到渲染通道。
        /// </summary>
        private async Task InferenceLoopAsync(CancellationToken token)
        {
            var fpsWindow = new Queue<long>(10);
            var sw = Stopwatch.StartNew();

            try
            {
                await foreach (var frame in _decodeChannel.Reader.ReadAllAsync(token))
                {
                    try
                    {
                        var t0 = sw.ElapsedMilliseconds;

                        using var imageSharp = MatToImageSharp(frame);
                        var result = FireDetectionModule.Detect(imageSharp);
                        using var plotted = result.PlotImage(imageSharp) as SixLabors.ImageSharp.Image<Rgba32>;

                        // 兼容兜底：绘图失败时回退原图，避免本帧直接丢失。
                        if (plotted == null)
                        {
                            System.Diagnostics.Debug.WriteLine("警告: PlotImage 返回 null，使用原始图像");
                        }

                        // 计算推理滑动窗口 FPS，减小单帧抖动。
                        var elapsed = sw.ElapsedMilliseconds - t0;
                        fpsWindow.Enqueue(elapsed);
                        if (fpsWindow.Count > 10) fpsWindow.Dequeue();
                        var avgMs = fpsWindow.Average();
                        var fps = avgMs > 0 ? 1000.0 / avgMs : 0;

                        var detBmp = ImageSharpToWriteableBitmap(plotted ?? imageSharp);
                        var infos = BuildDetectionInfos(result, Confidence);

                        // 打包渲染所需数据并送入渲染通道。
                        var ir = new InferenceResult
                        {
                            DetectionImage = detBmp,
                            Infos = infos,
                            InferenceTimeMs = elapsed,
                            Fps = fps
                        };

                        _inferenceChannel.Writer.TryWrite(ir);
                    }
                    finally
                    {
                        frame.Dispose();
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _inferenceChannel?.Writer.TryComplete();
            }
        }

        /// <summary>
        /// 渲染循环。
        /// 职责：将推理结果同步到 UI 绑定属性，避免在推理线程直接操作界面对象。
        /// </summary>
        private async Task RenderLoopAsync(CancellationToken token)
        {
            try
            {
                await foreach (var ir in _inferenceChannel.Reader.ReadAllAsync(token))
                {
                    await App.Current.Dispatcher.InvokeAsync(() =>
                    {
                        DetetionSoure = ir.DetectionImage;
                        DetectionInfos = new ObservableCollection<DetectionInfo>(ir.Infos);
                        DetectionCount = ir.Infos.Count;
                        MaxConfidence = ir.Infos.Count > 0 ? ir.Infos.Max(i => i.Confidence) : 0f;
                        InferenceTimeMs = ir.InferenceTimeMs;
                        DetectionFps = ir.Fps;
                        LastUpdateTime = DateTime.Now.ToString("HH:mm:ss.fff");
                    });
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 推理阶段到渲染阶段的数据传输对象。
        /// </summary>
        private sealed class InferenceResult
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
            /// 检测帧率（滑动窗口估算）。
            /// </summary>
            public double Fps { get; set; }
        }

        /// <summary>
        /// OpenCV Mat（BGR24）转 WriteableBitmap。
        /// 通过 MemoryCopy 直接拷贝像素，减少中间编码开销。
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
        /// 逐像素写入 BackBuffer，避免 PNG 编码/解码开销。
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
                    var ptr = (byte*)bitmap.BackBuffer.ToPointer();
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
        /// 该格式转换用于适配 YOLO 推理输入类型。
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
        /// 从检测结果对象中提取并过滤检测信息。
        /// 使用阈值过滤后，按置信度降序保留最多 50 条用于 UI 展示。
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
        /// 枚举检测项的统一入口。
        /// 同时兼容“结果本身可枚举”与“结果对象包含 Detections 属性”两种结构。
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
        /// 检测结果访问器：
        /// 通过反射兼容不同模型/库版本下的字段命名差异。
        /// </summary>
        private sealed class DetectionAccessor
        {
            /// <summary>
            /// 置信度候选属性名。
            /// </summary>
            private static readonly string[] ConfidenceNames = { "Confidence", "Probability", "Score" };

            /// <summary>
            /// 标签候选属性名。
            /// </summary>
            private static readonly string[] LabelNames = { "Label", "Name", "ClassName", "Category", "Class" };

            /// <summary>
            /// 类别 ID 候选属性名。
            /// </summary>
            private static readonly string[] ClassIdNames = { "ClassId", "ClassIndex", "Class" };

            /// <summary>
            /// 边界框候选属性名。
            /// </summary>
            private static readonly string[] BoxNames = { "BoundingBox", "Box", "Rect", "Rectangle", "Bbox" };
            private static readonly string[] BoxXNames = { "X", "Left" };
            private static readonly string[] BoxYNames = { "Y", "Top" };
            private static readonly string[] BoxWNames = { "Width" };
            private static readonly string[] BoxHNames = { "Height" };
            private static readonly string[] BoxRNames = { "Right" };
            private static readonly string[] BoxBNames = { "Bottom" };

            /// <summary>
            /// 按检测项类型缓存访问器，减少重复反射。
            /// </summary>
            private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, DetectionAccessor> Cache = new();

            /// <summary>
            /// 按推理结果类型缓存检测集合属性。
            /// </summary>
            private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyInfo> DetPropCache = new();

            /// <summary>
            /// 当前检测类型的关键属性句柄。
            /// </summary>
            private readonly PropertyInfo? _conf, _label, _classId, _box;

            private DetectionAccessor(Type t)
            {
                _conf = Find(t, ConfidenceNames);
                _label = Find(t, LabelNames);
                _classId = Find(t, ClassIdNames);
                _box = Find(t, BoxNames);
            }

            public static DetectionAccessor Get(Type t) => Cache.GetOrAdd(t, x => new DetectionAccessor(x));

            /// <summary>
            /// 根据候选属性名查找检测集合属性。
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
            /// 从检测对象中读取置信度。
            /// </summary>
            public float GetConfidence(object det) => ToFloat(_conf?.GetValue(det));

            /// <summary>
            /// 从检测对象中读取标签文本，失败时回退到 ClassId/Unknown。
            /// </summary>
            public string GetLabel(object det)
            {
                var lbl = ExtractLabel(_label?.GetValue(det));
                if (!string.IsNullOrWhiteSpace(lbl)) return lbl;
                var cid = _classId?.GetValue(det);
                return cid != null ? $"Class {cid}" : "Unknown";
            }
            /// <summary>
            /// 从检测对象中读取框信息并格式化字符串。
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
            /// 提取标签文本，兼容 string / Name / Text 多种结构。
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
            /// 安全转换为 float，异常或失败时返回 0。
            /// </summary>
            private static float ToFloat(object? v) => v switch
            {
                null => 0f, float f => f, double d => (float)d, decimal m => (float)m,
                int i => i, long l => l, short s => s, byte b => b,
                _ => float.TryParse(v.ToString(), out var r) ? r : 0f
            };
        }

        /// <summary>
        /// UI 展示用检测条目模型。
        /// </summary>
        public class DetectionInfo
        {
            /// <summary>
            /// 目标标签名称。
            /// </summary>
            public string Label { get; set; }

            /// <summary>
            /// 目标置信度（0~1）。
            /// </summary>
            public float Confidence { get; set; }

            /// <summary>
            /// 目标框位置字符串。
            /// </summary>
            public string Box { get; set; }
        }
    }
}
