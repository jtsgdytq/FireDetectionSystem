using Compunet.YoloSharp;
using Compunet.YoloSharp.Plotting;
using FireDetectionSystem.Core;
using FireDetectionSystem.Models;
using FireDetectionSystem.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FireDetectionSystem.ViewModels
{
    /// <summary>
    /// 图片检测视图模型
    /// 负责处理单张图片的火灾检测功能
    /// </summary>
    public class ImageDetectionViewModel : BindableBase
    {
        private readonly ILoggerService _logger;
        private readonly IConfigurationService _configService;
        private readonly IDatabaseService _dbService;
        private readonly IAlarmService _alarmService;

        #region 绑定属性

        private string? _imagePath;
        /// <summary>
        /// 选择的图片文件路径
        /// </summary>
        public string? ImagePath
        {
            get => _imagePath;
            set { _imagePath = value; RaisePropertyChanged(); }
        }

        private ImageSource? _detectImage;
        /// <summary>
        /// 检测结果图片（带标注框）
        /// </summary>
        public ImageSource? DetectImage
        {
            get => _detectImage;
            set { _detectImage = value; RaisePropertyChanged(); }
        }

        private float _confidence = 0.5f;
        /// <summary>
        /// 置信度阈值
        /// 只显示置信度高于此值的检测结果
        /// </summary>
        public float Confidence
        {
            get => _confidence;
            set { _confidence = value; RaisePropertyChanged(); }
        }

        private bool _isDetecting;
        /// <summary>
        /// 是否正在检测中
        /// 用于显示加载动画和禁用按钮
        /// </summary>
        public bool IsDetecting
        {
            get => _isDetecting;
            set { _isDetecting = value; RaisePropertyChanged(); DetectionImageCommand.RaiseCanExecuteChanged(); }
        }

        private string _statusMessage = "请选择图片进行检测";
        /// <summary>
        /// 状态消息
        /// 显示当前操作状态或检测结果
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; RaisePropertyChanged(); }
        }

        private int _detectionCount;
        /// <summary>
        /// 检测到的目标数量
        /// </summary>
        public int DetectionCount
        {
            get => _detectionCount;
            set { _detectionCount = value; RaisePropertyChanged(); }
        }

        private float _maxConfidence;
        /// <summary>
        /// 最高置信度
        /// </summary>
        public float MaxConfidence
        {
            get => _maxConfidence;
            set { _maxConfidence = value; RaisePropertyChanged(); }
        }

        #endregion

        #region 命令

        /// <summary>
        /// 检测图片命令
        /// </summary>
        public DelegateCommand DetectionImageCommand { get; set; }

        /// <summary>
        /// 选择图片路径命令
        /// </summary>
        public DelegateCommand SelectImagePathCommand { get; set; }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志服务</param>
        /// <param name="configService">配置服务</param>
        /// <param name="dbService">数据库服务</param>
        /// <param name="alarmService">报警服务</param>
        public ImageDetectionViewModel(
            ILoggerService logger,
            IConfigurationService configService,
            IDatabaseService dbService,
            IAlarmService alarmService)
        {
            _logger = logger;
            _configService = configService;
            _dbService = dbService;
            _alarmService = alarmService;

            // 从配置文件读取默认置信度
            _confidence = _configService.ConfidenceThreshold;

            // 初始化命令
            DetectionImageCommand = new DelegateCommand(ExecuteDetection, CanExecuteDetection);
            SelectImagePathCommand = new DelegateCommand(SelectImagePath);
        }

        /// <summary>
        /// 选择图片文件
        /// 打开文件选择对话框，让用户选择要检测的图片
        /// </summary>
        private void SelectImagePath()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif|所有文件|*.*",
                    Title = "选择要检测的图片"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    ImagePath = openFileDialog.FileName;
                    StatusMessage = $"已选择图片：{Path.GetFileName(ImagePath)}";
                    _logger.Info($"选择图片: {ImagePath}");

                    // 清空之前的检测结果
                    DetectImage = null;
                    DetectionCount = 0;
                    MaxConfidence = 0;

                    // 更新命令状态
                    DetectionImageCommand.RaiseCanExecuteChanged();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"选择图片失败: {ex.Message}", ex);
                ShowError("选择图片失败", ex.Message);
            }
        }

        /// <summary>
        /// 判断是否可以执行检测
        /// 图片路径不为空且不在检测中时才允许检测
        /// </summary>
        /// <returns>是否可以执行检测</returns>
        private bool CanExecuteDetection()
        {
            return !string.IsNullOrEmpty(ImagePath) && !IsDetecting;
        }

        /// <summary>
        /// 执行图片检测
        /// 使用 YOLO 模型检测图片中的火灾，并保存结果到数据库
        /// </summary>
        private async void ExecuteDetection()
        {
            if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
            {
                ShowError("错误", "请先选择有效的图片文件");
                return;
            }

            try
            {
                // 设置检测中状态
                IsDetecting = true;
                StatusMessage = "正在检测中，请稍候...";
                _logger.Info($"开始检测图片: {ImagePath}");

                // 调用 YOLO 模型进行检测
                var result = await FireDetectionModule.DetectAsync(
                    ImagePath,
                    new YoloConfiguration { Confidence = Confidence });

                // 加载原始图片
                using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(ImagePath);

                // 在图片上绘制检测结果（边界框和标签）
                using var plotted = result.PlotImage(img) as SixLabors.ImageSharp.Image<Rgba32>;

                if (plotted != null)
                {
                    // 转换为 WPF 可显示的图片格式
                    DetectImage = ImageSharpToBitmapImage(plotted);
                }

                // 统计检测结果
                // YoloResult 本身就是一个集合，可以直接遍历
                var detections = result.ToList();
                DetectionCount = detections.Count;
                MaxConfidence = detections.Any() ? detections.Max(d => d.Confidence) : 0;

                // 更新状态消息
                if (DetectionCount > 0)
                {
                    StatusMessage = $"检测完成：发现 {DetectionCount} 个目标，最高置信度 {MaxConfidence:P1}";
                    _logger.Info($"检测完成: 发现 {DetectionCount} 个目标");
                }
                else
                {
                    StatusMessage = "检测完成：未发现火灾";
                    _logger.Info("检测完成: 未发现火灾");
                }

                // 保存检测记录到数据库
                await SaveDetectionRecordAsync(result, detections);

                // 如果检测到火灾且置信度超过阈值，触发报警
                if (DetectionCount > 0 && MaxConfidence >= _configService.AlertThreshold)
                {
                    _logger.Warning($"检测到火灾！置信度: {MaxConfidence:P1}");
                }
            }
            catch (FileNotFoundException ex)
            {
                _logger.Error($"图片文件不存在: {ex.Message}", ex);
                ShowError("文件不存在", "选择的图片文件不存在，请重新选择");
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error($"模型未初始化: {ex.Message}", ex);
                ShowError("模型未加载", "YOLO 模型未加载，请检查配置文件中的模型路径");
            }
            catch (Exception ex)
            {
                _logger.Error($"检测失败: {ex.Message}", ex);
                ShowError("检测失败", $"检测过程中发生错误：{ex.Message}");
            }
            finally
            {
                // 无论成功失败，都要恢复状态
                IsDetecting = false;
            }
        }

        /// <summary>
        /// 保存检测记录到数据库
        /// </summary>
        /// <param name="result">YOLO 检测结果</param>
        /// <param name="detections">检测到的目标列表</param>
        private async System.Threading.Tasks.Task SaveDetectionRecordAsync(
            Compunet.YoloSharp.Data.YoloResult<Compunet.YoloSharp.Data.Detection> result,
            System.Collections.Generic.List<Compunet.YoloSharp.Data.Detection> detections)
        {
            try
            {
                // 构建检测详情 JSON
                // 使用反射访问 Detection 属性，因为不同版本的 YoloSharp 可能有不同的属性名
                var detectionDetails = new System.Collections.Generic.List<object>();

                foreach (var d in detections)
                {
                    var detectionType = d.GetType();

                    // 尝试获取 Label 属性
                    var labelProp = detectionType.GetProperty("Label");
                    var labelValue = labelProp?.GetValue(d);
                    var labelName = labelValue?.GetType().GetProperty("Name")?.GetValue(labelValue)?.ToString() ?? "Unknown";

                    // 尝试获取 Bounds 或 BoundingBox 属性
                    var boundsProp = detectionType.GetProperty("Bounds") ?? detectionType.GetProperty("BoundingBox");
                    var boundsValue = boundsProp?.GetValue(d);
                    var boundsStr = "-";

                    if (boundsValue != null)
                    {
                        var boundsType = boundsValue.GetType();
                        var x = boundsType.GetProperty("X")?.GetValue(boundsValue);
                        var y = boundsType.GetProperty("Y")?.GetValue(boundsValue);
                        var width = boundsType.GetProperty("Width")?.GetValue(boundsValue);
                        var height = boundsType.GetProperty("Height")?.GetValue(boundsValue);

                        if (x != null && y != null && width != null && height != null)
                        {
                            boundsStr = $"{x},{y} {width}x{height}";
                        }
                    }

                    detectionDetails.Add(new
                    {
                        Label = labelName,
                        Confidence = d.Confidence,
                        Box = boundsStr
                    });
                }

                var detailsJson = JsonSerializer.Serialize(detectionDetails);

                // 创建检测记录
                var record = new DetectionRecord
                {
                    DetectionTime = DateTime.Now,
                    SourceType = "Image",
                    SourcePath = ImagePath,
                    IsFireDetected = DetectionCount > 0,
                    DetectionCount = DetectionCount,
                    MaxConfidence = MaxConfidence,
                    DetectionDetails = detailsJson,
                    IsAlarmTriggered = DetectionCount > 0 && MaxConfidence >= _configService.AlertThreshold,
                    UserId = LoginViewModel.CurrentUser?.Id
                };

                // 保存到数据库
                var recordId = await _dbService.SaveDetectionRecordAsync(record);
                record.Id = recordId;

                _logger.Info($"检测记录已保存: ID={recordId}");

                // 如果需要报警，触发报警
                if (record.IsAlarmTriggered)
                {
                    await _alarmService.TriggerAlarmAsync(record);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"保存检测记录失败: {ex.Message}", ex);
                // 保存失败不影响检测功能，只记录日志
            }
        }

        /// <summary>
        /// 将 ImageSharp 图像转换为 WPF 的 BitmapImage
        /// </summary>
        /// <param name="image">ImageSharp 图像对象</param>
        /// <returns>WPF BitmapImage 对象</returns>
        private BitmapImage ImageSharpToBitmapImage(SixLabors.ImageSharp.Image<Rgba32> image)
        {
            using var ms = new MemoryStream();
            // 将图像保存为 PNG 格式到内存流
            image.SaveAsPng(ms);
            ms.Seek(0, SeekOrigin.Begin);

            // 从内存流创建 BitmapImage
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // 立即加载到内存
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze(); // 冻结对象，使其可以跨线程使用

            return bitmap;
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        /// <param name="title">错误标题</param>
        /// <param name="message">错误消息</param>
        private void ShowError(string title, string message)
        {
            StatusMessage = $"错误：{message}";
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
