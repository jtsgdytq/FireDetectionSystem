using Compunet.YoloSharp.Plotting;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Concurrent;
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
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FireDetectionSystem.ViewModels
{
    class CameraDetectionViewModel : BindableBase
    {
        // UI 刷新节流，避免频繁更新导致卡顿
        private const int UiUpdateIntervalMs = 120;

        private readonly ObservableCollection<CameraItem> cameras = new ObservableCollection<CameraItem>();
        public ObservableCollection<CameraItem> Cameras => cameras;

        private CameraItem selectedCamera;
        public CameraItem SelectedCamera
        {
            get { return selectedCamera; }
            set { selectedCamera = value; RaisePropertyChanged(); }
        }

        private float confidenceThreshold = 0.5f;
        public float ConfidenceThreshold
        {
            get { return confidenceThreshold; }
            set { confidenceThreshold = value; RaisePropertyChanged(); }
        }

        private float alertThreshold = 0.7f;
        public float AlertThreshold
        {
            get { return alertThreshold; }
            set { alertThreshold = value; RaisePropertyChanged(); }
        }

        public int ConnectedCount => Cameras.Count;
        public int RunningCount => Cameras.Count(c => c.Status == CameraStatus.Running);

        public DelegateCommand AddVideoSourceCommand { get; }
        public DelegateCommand RemoveSelectedCommand { get; }
        public DelegateCommand ToggleSelectedPauseCommand { get; }
        public DelegateCommand StartSelectedCommand { get; }

        private int cameraIndex = 1;

        public CameraDetectionViewModel()
        {
            AddVideoSourceCommand = new DelegateCommand(AddVideoSource);
            RemoveSelectedCommand = new DelegateCommand(RemoveSelected, CanRemoveSelected)
                .ObservesProperty(() => SelectedCamera);
            ToggleSelectedPauseCommand = new DelegateCommand(ToggleSelectedPause, CanToggleSelected)
                .ObservesProperty(() => SelectedCamera);
            StartSelectedCommand = new DelegateCommand(StartSelected, CanStartSelected)
                .ObservesProperty(() => SelectedCamera);

            cameras.CollectionChanged += OnCamerasChanged;
        }

        private void OnCamerasChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (CameraItem item in e.NewItems)
                {
                    item.PropertyChanged += OnCameraItemChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (CameraItem item in e.OldItems)
                {
                    item.PropertyChanged -= OnCameraItemChanged;
                }
            }

            RaisePropertyChanged(nameof(ConnectedCount));
            RaisePropertyChanged(nameof(RunningCount));
        }

        private void OnCameraItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CameraItem.Status))
            {
                RaisePropertyChanged(nameof(RunningCount));
            }
        }

        private void AddVideoSource()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.avi;*.mov;*.mkv|所有文件|*.*",
                Title = "选择视频文件作为摄像头源"
            };

            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            var camera = new CameraItem
            {
                Name = $"模拟摄像头 {cameraIndex++}",
                SourcePath = openFileDialog.FileName,
                Status = CameraStatus.Stopped,
                StatusText = "未启动",
                PauseButtonText = "暂停",
                LastUpdateTime = "-"
            };

            Cameras.Add(camera);
            SelectedCamera = camera;

            // 立即启动检测，模拟摄像头连接后自动工作
            StartCamera(camera);
        }

        private void RemoveSelected()
        {
            if (SelectedCamera == null)
            {
                return;
            }

            StopCamera(SelectedCamera);
            Cameras.Remove(SelectedCamera);
            SelectedCamera = Cameras.FirstOrDefault();
        }

        private bool CanRemoveSelected()
        {
            return SelectedCamera != null;
        }

        private void ToggleSelectedPause()
        {
            if (SelectedCamera == null)
            {
                return;
            }

            SelectedCamera.IsPaused = !SelectedCamera.IsPaused;
            SelectedCamera.PauseButtonText = SelectedCamera.IsPaused ? "继续" : "暂停";
            SelectedCamera.Status = SelectedCamera.IsPaused ? CameraStatus.Paused : CameraStatus.Running;
            SelectedCamera.StatusText = SelectedCamera.IsPaused ? "已暂停" : "运行中";
        }

        private bool CanToggleSelected()
        {
            return SelectedCamera != null;
        }

        private void StartSelected()
        {
            if (SelectedCamera == null)
            {
                return;
            }

            StartCamera(SelectedCamera);
        }

        private bool CanStartSelected()
        {
            return SelectedCamera != null;
        }

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

            var token = camera.Cts.Token;
            camera.DetectionTask = Task.Run(() => CameraLoop(camera, token), token);
        }

        private void StopCamera(CameraItem camera)
        {
            if (camera.Cts != null)
            {
                camera.Cts.Cancel();
                camera.Cts.Dispose();
                camera.Cts = null;
            }

            camera.Status = CameraStatus.Stopped;
            camera.StatusText = "已停止";
            camera.AlertMessage = "已停止";
            camera.IsAlert = false;
        }

        private void CameraLoop(CameraItem camera, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(camera.SourcePath) || !File.Exists(camera.SourcePath))
            {
                UpdateCameraStatus(camera, CameraStatus.Error, "源文件不存在");
                return;
            }

            using var capture = new VideoCapture(camera.SourcePath);
            using var frame = new Mat();
            if (!capture.IsOpened())
            {
                UpdateCameraStatus(camera, CameraStatus.Error, "打开失败");
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var lastUiUpdateMs = 0L;

            while (!token.IsCancellationRequested)
            {
                if (camera.IsPaused)
                {
                    Thread.Sleep(50);
                    continue;
                }

                if (!capture.Read(frame) || frame.Empty())
                {
                    // 模拟摄像头：视频播完后从头开始循环
                    capture.Set(VideoCaptureProperties.PosFrames, 0);
                    continue;
                }

                using var imageSharpImage = MatToImageSharp(frame, camera);
                var result = FireDetectionModule.Detect(imageSharpImage);

                var nowMs = stopwatch.ElapsedMilliseconds;
                if (nowMs - lastUiUpdateMs >= UiUpdateIntervalMs)
                {
                    lastUiUpdateMs = nowMs;

                    var originalImage = ImageSharpToBitmapImage(imageSharpImage);
                    using var plotted = result.PlotImage(imageSharpImage) as SixLabors.ImageSharp.Image<Rgba32>;
                    var detectionImage = plotted != null ? ImageSharpToBitmapImage(plotted) : originalImage;

                    var infos = BuildDetectionInfos(result, ConfidenceThreshold);
                    UpdateCameraResult(camera, originalImage, detectionImage, infos);
                }

                Thread.Sleep(10);
            }
        }

        // OpenCV Mat 转换为 ImageSharp，使用复用缓存降低分配
        private static SixLabors.ImageSharp.Image<Rgba32> MatToImageSharp(Mat mat, CameraItem camera)
        {
            using var rgba = new Mat();
            Cv2.CvtColor(mat, rgba, ColorConversionCodes.BGR2RGBA);

            var byteLength = rgba.Rows * rgba.Cols * rgba.ElemSize();
            if (camera.FrameBuffer == null || camera.FrameBuffer.Length != byteLength)
            {
                camera.FrameBuffer = new byte[byteLength];
            }

            Marshal.Copy(rgba.Data, camera.FrameBuffer, 0, byteLength);
            return SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(camera.FrameBuffer, rgba.Cols, rgba.Rows);
        }

        // ImageSharp 图像转换为 WPF 的 BitmapImage
        private static ImageSource ImageSharpToBitmapImage(SixLabors.ImageSharp.Image<Rgba32> image)
        {
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        private void UpdateCameraStatus(CameraItem camera, CameraStatus status, string text)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                camera.Status = status;
                camera.StatusText = text;
                camera.AlertMessage = status == CameraStatus.Error ? $"错误：{text}" : camera.AlertMessage;
                camera.IsAlert = status == CameraStatus.Error;
            });
        }

        private void UpdateCameraResult(CameraItem camera, ImageSource original, ImageSource detection, IReadOnlyList<DetectionInfo> infos)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                camera.OriginalFrame = original;
                camera.DetectionFrame = detection;
                camera.DetectionInfos = new ObservableCollection<DetectionInfo>(infos);
                camera.DetectionCount = infos.Count;
                camera.MaxConfidence = infos.Count > 0 ? infos.Max(i => i.Confidence) : 0f;
                camera.LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");

                // 预警规则：存在目标且置信度超过阈值
                var isAlert = infos.Count > 0 && camera.MaxConfidence >= AlertThreshold;
                camera.IsAlert = isAlert;
                camera.AlertMessage = isAlert
                    ? $"预警：检测到目标，最高置信度 {camera.MaxConfidence:P1}"
                    : "状态正常";
            });
        }

        private static IReadOnlyList<DetectionInfo> BuildDetectionInfos(object result, float confidenceThreshold)
        {
            // 通过反射适配不同检测结果结构
            var list = new List<DetectionInfo>();
            foreach (var detection in EnumerateDetections(result))
            {
                if (detection == null)
                {
                    continue;
                }

                var accessor = DetectionAccessor.Get(detection.GetType());
                var confidence = accessor.GetConfidence(detection);
                if (confidence < confidenceThreshold)
                {
                    continue;
                }

                list.Add(new DetectionInfo
                {
                    Label = accessor.GetLabel(detection),
                    Confidence = confidence,
                    Box = accessor.GetBox(detection)
                });
            }

            return list.OrderByDescending(info => info.Confidence).Take(50).ToList();
        }

        private static IEnumerable<object> EnumerateDetections(object result)
        {
            if (result is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        yield return item;
                    }
                }
                yield break;
            }

            var type = result.GetType();
            var property = DetectionAccessor.GetDetectionsProperty(type);
            if (property == null)
            {
                yield break;
            }

            if (property.GetValue(result) is System.Collections.IEnumerable list)
            {
                foreach (var item in list)
                {
                    if (item != null)
                    {
                        yield return item;
                    }
                }
            }
        }

        private sealed class DetectionAccessor
        {
            // 属性名兜底，兼容不同模型输出
            private static readonly string[] ConfidencePropertyNames = { "Confidence", "Probability", "Score" };
            private static readonly string[] LabelPropertyNames = { "Label", "Name", "ClassName", "Category", "Class" };
            private static readonly string[] ClassIdPropertyNames = { "ClassId", "ClassIndex", "Class" };
            private static readonly string[] BoxPropertyNames = { "BoundingBox", "Box", "Rect", "Rectangle", "Bbox" };
            private static readonly string[] BoxXPropertyNames = { "X", "Left" };
            private static readonly string[] BoxYPropertyNames = { "Y", "Top" };
            private static readonly string[] BoxWPropertyNames = { "Width" };
            private static readonly string[] BoxHPropertyNames = { "Height" };
            private static readonly string[] BoxRPropertyNames = { "Right" };
            private static readonly string[] BoxBPropertyNames = { "Bottom" };

            private static readonly ConcurrentDictionary<Type, DetectionAccessor> Cache = new ConcurrentDictionary<Type, DetectionAccessor>();
            private static readonly ConcurrentDictionary<Type, PropertyInfo> DetectionsPropertyCache = new ConcurrentDictionary<Type, PropertyInfo>();

            private readonly PropertyInfo? confidenceProperty;
            private readonly PropertyInfo? labelProperty;
            private readonly PropertyInfo? classIdProperty;
            private readonly PropertyInfo? boxProperty;

            private DetectionAccessor(Type type)
            {
                confidenceProperty = FindProperty(type, ConfidencePropertyNames);
                labelProperty = FindProperty(type, LabelPropertyNames);
                classIdProperty = FindProperty(type, ClassIdPropertyNames);
                boxProperty = FindProperty(type, BoxPropertyNames);
            }

            public static DetectionAccessor Get(Type type)
            {
                return Cache.GetOrAdd(type, t => new DetectionAccessor(t));
            }

            public static PropertyInfo? GetDetectionsProperty(Type type)
            {
                if (DetectionsPropertyCache.TryGetValue(type, out var cached))
                {
                    return cached;
                }

                foreach (var name in new[] { "Detections", "Predictions", "Boxes", "Items", "Results", "Outputs" })
                {
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
                    {
                        DetectionsPropertyCache[type] = prop;
                        return prop;
                    }
                }

                var fallback = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(string));

                if (fallback != null)
                {
                    DetectionsPropertyCache[type] = fallback;
                }

                return fallback;
            }

            public float GetConfidence(object detection)
            {
                return ConvertToSingle(confidenceProperty?.GetValue(detection));
            }

            public string GetLabel(object detection)
            {
                var label = labelProperty?.GetValue(detection);
                var labelText = ExtractLabel(label);
                if (!string.IsNullOrWhiteSpace(labelText))
                {
                    return labelText;
                }

                var classIdValue = classIdProperty?.GetValue(detection);
                if (classIdValue != null)
                {
                    return $"Class {classIdValue}";
                }

                return "Unknown";
            }

            public string GetBox(object detection)
            {
                var box = boxProperty?.GetValue(detection);
                if (box == null)
                {
                    return "-";
                }

                var boxType = box.GetType();
                var x = FindProperty(boxType, BoxXPropertyNames);
                var y = FindProperty(boxType, BoxYPropertyNames);
                var w = FindProperty(boxType, BoxWPropertyNames);
                var h = FindProperty(boxType, BoxHPropertyNames);
                var r = FindProperty(boxType, BoxRPropertyNames);
                var b = FindProperty(boxType, BoxBPropertyNames);

                if (x != null && y != null && w != null && h != null)
                {
                    return $"{ConvertToSingle(x.GetValue(box)):0},{ConvertToSingle(y.GetValue(box)):0} {ConvertToSingle(w.GetValue(box)):0}x{ConvertToSingle(h.GetValue(box)):0}";
                }

                if (x != null && y != null && r != null && b != null)
                {
                    var left = ConvertToSingle(x.GetValue(box));
                    var top = ConvertToSingle(y.GetValue(box));
                    var right = ConvertToSingle(r.GetValue(box));
                    var bottom = ConvertToSingle(b.GetValue(box));
                    return $"{left:0},{top:0} -> {right:0},{bottom:0}";
                }

                return box.ToString() ?? "-";
            }

            private static PropertyInfo? FindProperty(Type type, string[] names)
            {
                foreach (var name in names)
                {
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        return prop;
                    }
                }
                return null;
            }

            private static string? ExtractLabel(object labelValue)
            {
                if (labelValue == null)
                {
                    return null;
                }

                if (labelValue is string text)
                {
                    return text;
                }

                var nameProperty = labelValue.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
                    ?? labelValue.GetType().GetProperty("Text", BindingFlags.Public | BindingFlags.Instance);
                if (nameProperty != null)
                {
                    return nameProperty.GetValue(labelValue)?.ToString();
                }

                return labelValue.ToString();
            }

            private static float ConvertToSingle(object value)
            {
                if (value == null)
                {
                    return 0f;
                }

                return value switch
                {
                    float f => f,
                    double d => (float)d,
                    decimal m => (float)m,
                    int i => i,
                    long l => l,
                    short s => s,
                    byte b => b,
                    _ => float.TryParse(value.ToString(), out var parsed) ? parsed : 0f
                };
            }
        }

        public class DetectionInfo
        {
            public string Label { get; set; }
            public float Confidence { get; set; }
            public string Box { get; set; }
        }

        public class CameraItem : BindableBase
        {
            private string name;
            public string Name
            {
                get { return name; }
                set { name = value; RaisePropertyChanged(); }
            }

            private string sourcePath;
            public string SourcePath
            {
                get { return sourcePath; }
                set { sourcePath = value; RaisePropertyChanged(); }
            }

            private CameraStatus status;
            public CameraStatus Status
            {
                get { return status; }
                set { status = value; RaisePropertyChanged(); }
            }

            private string statusText;
            public string StatusText
            {
                get { return statusText; }
                set { statusText = value; RaisePropertyChanged(); }
            }

            private bool isPaused;
            public bool IsPaused
            {
                get { return isPaused; }
                set { isPaused = value; RaisePropertyChanged(); }
            }

            private string pauseButtonText;
            public string PauseButtonText
            {
                get { return pauseButtonText; }
                set { pauseButtonText = value; RaisePropertyChanged(); }
            }

            private ImageSource originalFrame;
            public ImageSource OriginalFrame
            {
                get { return originalFrame; }
                set { originalFrame = value; RaisePropertyChanged(); }
            }

            private ImageSource detectionFrame;
            public ImageSource DetectionFrame
            {
                get { return detectionFrame; }
                set { detectionFrame = value; RaisePropertyChanged(); }
            }

            private ObservableCollection<DetectionInfo> detectionInfos = new ObservableCollection<DetectionInfo>();
            public ObservableCollection<DetectionInfo> DetectionInfos
            {
                get { return detectionInfos; }
                set { detectionInfos = value; RaisePropertyChanged(); }
            }

            private int detectionCount;
            public int DetectionCount
            {
                get { return detectionCount; }
                set { detectionCount = value; RaisePropertyChanged(); }
            }

            private float maxConfidence;
            public float MaxConfidence
            {
                get { return maxConfidence; }
                set { maxConfidence = value; RaisePropertyChanged(); }
            }

            private string lastUpdateTime;
            public string LastUpdateTime
            {
                get { return lastUpdateTime; }
                set { lastUpdateTime = value; RaisePropertyChanged(); }
            }

            private bool isAlert;
            public bool IsAlert
            {
                get { return isAlert; }
                set { isAlert = value; RaisePropertyChanged(); }
            }

            private string alertMessage;
            public string AlertMessage
            {
                get { return alertMessage; }
                set { alertMessage = value; RaisePropertyChanged(); }
            }

            public CancellationTokenSource? Cts { get; set; }
            public Task? DetectionTask { get; set; }
            public byte[]? FrameBuffer { get; set; }
        }

        public enum CameraStatus
        {
            Stopped,
            Running,
            Paused,
            Error
        }
    }
}
