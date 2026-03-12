using Compunet.YoloSharp.Plotting;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FireDetectionSystem.ViewModels
{
    class VideoDetectionViewModel : BindableBase, IDialogAware
    {
        // Throttle UI updates so rendering doesn't block detection.
        private const int UiUpdateIntervalMs = 120;

        private string vediePath;

        public string VideoPath
        {
            get { return vediePath; }
            set { vediePath = value;RaisePropertyChanged(); }
        }

        public MediaElement MediaElement;

        private float confidence = 0.5f;
        public float Confidence
        {
            get { return confidence; }
            set { confidence = value; RaisePropertyChanged(); }
        }

        private ImageSource detetionSoure;
       

        public ImageSource DetetionSoure
        {
            get { return detetionSoure; }
            set { detetionSoure = value;RaisePropertyChanged(); }
        }

        private ImageSource origneImage;

        public ImageSource OrigneImage
        {
            get { return origneImage; }
            set { origneImage = value; RaisePropertyChanged(); }
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

        private string lastUpdateTime = "-";
        public string LastUpdateTime
        {
            get { return lastUpdateTime; }
            set { lastUpdateTime = value; RaisePropertyChanged(); }
        }


        private CancellationTokenSource _cts;
        private byte[] _frameBuffer; // Reuse buffer to avoid per-frame allocations.

        private bool isPaused;
        public bool IsPaused
        {
            get { return isPaused; }
            set { isPaused = value; RaisePropertyChanged(); }
        }

        private string pauseButtonText = "暂停";
        public string PauseButtonText
        {
            get { return pauseButtonText; }
            set { pauseButtonText = value; RaisePropertyChanged(); }
        }

        public DelegateCommand SelectVideoPathCommnad { get; set; }

        public DelegateCommand DetectionVideoCommand { get; set; }
        public DelegateCommand TogglePauseCommand { get; set; }


        public VideoDetectionViewModel()
        {
            SelectVideoPathCommnad = new DelegateCommand(SelectVideoPath);

            DetectionVideoCommand = new DelegateCommand(DetectionVideo);
            TogglePauseCommand = new DelegateCommand(TogglePause);
        }
        // 视频检测方法
        private async void DetectionVideo()
        {
            if(string.IsNullOrEmpty(VideoPath) || !File.Exists(VideoPath))
            {
                return;
            }
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsPaused = false;
            PauseButtonText = "暂停";

          

            App.Current.Dispatcher.Invoke(() =>
            {
                if (MediaElement!=null)
                {
                   
                    MediaElement.Play();
                }
            });



            await Task.Run(() =>
            {
                using var capture = new VideoCapture(VideoPath);
                using var frame = new Mat();
                if (capture is null || !capture.IsOpened())
                {
                    return;
                }

                var stopwatch = Stopwatch.StartNew();
                var lastUiUpdateMs = 0L;

                while (!token.IsCancellationRequested)
                {
                    if (IsPaused)
                    {
                        // Pause decoding and processing; keep current frame on UI.
                        Thread.Sleep(50);
                        continue;
                    }

                    if (!capture.Read(frame) || frame.Empty())
                    {
                        break;
                    }

                    using var imageSharpImage = MatToImageSharp(frame);
                    var result = FireDetectionModule.Detect(imageSharpImage);

                    var nowMs = stopwatch.ElapsedMilliseconds;
                    if (nowMs - lastUiUpdateMs >= UiUpdateIntervalMs)
                    {
                        lastUiUpdateMs = nowMs;

                        var originalImage = ImageSharpToBitmapImage(imageSharpImage);
                        using var plotted = result.PlotImage(imageSharpImage) as SixLabors.ImageSharp.Image<Rgba32>;
                        var detectionImage = plotted != null ? ImageSharpToBitmapImage(plotted) : originalImage;

                        var infos = BuildDetectionInfos(result, Confidence);
                        // Update both frames together so left/right stay in sync.
                        UpdateDetectionPanel(originalImage, detectionImage, infos);
                    }

                    // 控制帧率
                    Thread.Sleep(10);
                }
            }, token);
        }
        // ImageSharp 图像转换为 BitmapImage
        private ImageSource ImageSharpToBitmapImage(SixLabors.ImageSharp.Image<Rgba32> image)
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
        // 选择视频文件路径
        private void SelectVideoPath()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "视频文件|*.mp4;*.avi;*.mov;*.mkv|所有文件|*.*"
            };
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                VideoPath = openFileDialog.FileName;
            }      
           
        }
        // OpenCV Mat 转换为 ImageSharp 图像
        private SixLabors.ImageSharp.Image<Rgba32> MatToImageSharp(Mat mat)
        {
            using var rgba = new Mat();
            Cv2.CvtColor(mat, rgba, ColorConversionCodes.BGR2RGBA);

            var byteLength = rgba.Rows * rgba.Cols * rgba.ElemSize();
            if (_frameBuffer == null || _frameBuffer.Length != byteLength)
            {
                _frameBuffer = new byte[byteLength];
            }

            Marshal.Copy(rgba.Data, _frameBuffer, 0, byteLength);
            return SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(_frameBuffer, rgba.Cols, rgba.Rows);
        }

        private void TogglePause()
        {
            IsPaused = !IsPaused;
            PauseButtonText = IsPaused ? "继续" : "暂停";
        }

        private void UpdateDetectionPanel(ImageSource original, ImageSource detection, IReadOnlyList<DetectionInfo> infos)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                OrigneImage = original;
                DetetionSoure = detection;
                DetectionInfos = new ObservableCollection<DetectionInfo>(infos);
                DetectionCount = infos.Count;
                MaxConfidence = infos.Count > 0 ? infos.Max(i => i.Confidence) : 0f;
                LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");
            });
        }

        private static IReadOnlyList<DetectionInfo> BuildDetectionInfos(object result, float confidenceThreshold)
        {
            // Use reflection so this works across YoloSharp result shapes.
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
            // Property name fallbacks for different detection models.
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

            private readonly PropertyInfo? _confidenceProperty;
            private readonly PropertyInfo? _labelProperty;
            private readonly PropertyInfo? _classIdProperty;
            private readonly PropertyInfo? _boxProperty;

            private DetectionAccessor(Type type)
            {
                _confidenceProperty = FindProperty(type, ConfidencePropertyNames);
                _labelProperty = FindProperty(type, LabelPropertyNames);
                _classIdProperty = FindProperty(type, ClassIdPropertyNames);
                _boxProperty = FindProperty(type, BoxPropertyNames);
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
                return ConvertToSingle(_confidenceProperty?.GetValue(detection));
            }

            public string GetLabel(object detection)
            {
                var label = _labelProperty?.GetValue(detection);
                var labelText = ExtractLabel(label);
                if (!string.IsNullOrWhiteSpace(labelText))
                {
                    return labelText;
                }

                var classIdValue = _classIdProperty?.GetValue(detection);
                if (classIdValue != null)
                {
                    return $"Class {classIdValue}";
                }

                return "Unknown";
            }

            public string GetBox(object detection)
            {
                var box = _boxProperty?.GetValue(detection);
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



        #region
        public DialogCloseListener RequestClose => throw new NotImplementedException();

        public bool CanCloseDialog()
        {
            throw new NotImplementedException();
        }

        public void OnDialogClosed()
        {
            throw new NotImplementedException();
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
