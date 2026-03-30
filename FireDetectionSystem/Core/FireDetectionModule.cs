using Compunet.YoloSharp;
using Compunet.YoloSharp.Data;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;

namespace FireDetectionSystem.Core
{
    /// <summary>
    /// 火灾检测核心模块
    /// 封装 YOLO 模型的加载和检测功能，提供统一的检测接口
    /// 使用单例模式确保模型只加载一次，节省内存和时间
    /// </summary>
    public static class FireDetectionModule
    {
        /// <summary>
        /// YOLO 预测器实例
        /// 全局唯一，在应用程序生命周期内只创建一次
        /// </summary>
        private static YoloPredictor? _predictor;

        /// <summary>
        /// 模型是否已加载的标志
        /// </summary>
        private static bool _isLoaded = false;

        /// <summary>
        /// 线程锁对象
        /// 确保多线程环境下的初始化安全
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// 初始化火灾检测模块
        /// 加载 YOLO 模型文件，系统启动时调用一次
        /// </summary>
        /// <param name="modelPath">YOLO 模型文件路径（.onnx 格式）</param>
        /// <exception cref="Exception">模型加载失败时抛出异常</exception>
        public static void Initialize(string modelPath)
        {
            // 如果已经加载，直接返回
            if (_isLoaded) return;

            // 使用锁确保线程安全
            lock (_lock)
            {
                // 双重检查，防止多线程重复加载
                if (_isLoaded) return;

                try
                {
                    // 验证模型文件是否存在
                    if (!File.Exists(modelPath))
                    {
                        throw new FileNotFoundException($"模型文件不存在: {modelPath}");
                    }

                    // 初始化 YOLO 预测器
                    // YoloPredictor 会加载模型到内存，这个过程可能需要几秒钟
                    _predictor = new YoloPredictor(modelPath);

                    // 标记为已加载
                    _isLoaded = true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"模型加载失败: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 异步检测图片文件中的火灾
        /// 适用于图片检测功能
        /// </summary>
        /// <param name="imagePath">图片文件路径</param>
        /// <param name="configuration">YOLO 配置参数（可选），可设置置信度阈值等</param>
        /// <returns>检测结果，包含检测到的目标列表</returns>
        /// <exception cref="InvalidOperationException">模型未初始化时抛出</exception>
        public static async Task<Compunet.YoloSharp.Data.YoloResult<Compunet.YoloSharp.Data.Detection>> DetectAsync(
            string imagePath, YoloConfiguration? configuration = null)
        {
            // 确保模型已加载
            EnsureLoaded();

            // 验证图片文件是否存在
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"图片文件不存在: {imagePath}");
            }

            // 加载图片
            using var image = Image.Load<Rgba32>(imagePath);

            // 在后台线程执行检测，避免阻塞 UI 线程
            return await Task.Run(() => _predictor!.Detect(imagePath, configuration));
        }

        /// <summary>
        /// 同步检测 ImageSharp 图像对象中的火灾
        /// 适用于视频帧检测，要求快速同步处理
        /// </summary>
        /// <param name="image">ImageSharp 图像对象</param>
        /// <returns>检测结果，包含检测到的目标列表</returns>
        /// <exception cref="InvalidOperationException">模型未初始化时抛出</exception>
        public static Compunet.YoloSharp.Data.YoloResult<Compunet.YoloSharp.Data.Detection> Detect(Image image)
        {
            // 确保模型已加载
            EnsureLoaded();

            // 执行检测
            // 视频流通常要求同步快速处理，因此使用同步方法
            return _predictor!.Detect(image);
        }

        /// <summary>
        /// 检测 OpenCV Mat 对象（视频帧）中的火灾
        /// 专门用于摄像头和视频检测
        /// </summary>
        /// <param name="cvFrame">OpenCV Mat 对象（BGR 格式）</param>
        /// <returns>检测结果，包含检测到的目标列表</returns>
        /// <exception cref="InvalidOperationException">模型未初始化时抛出</exception>
        public static Compunet.YoloSharp.Data.YoloResult<Compunet.YoloSharp.Data.Detection> DetectFrame(Mat cvFrame)
        {
            // 确保模型已加载
            EnsureLoaded();

            // 将 OpenCV 的 Mat (BGR 格式) 转换为 ImageSharp 的 Image (RGB 格式)
            // 这是必要的，因为 YOLO 模型需要 RGB 格式的输入

            // 将 Mat 编码为 JPEG 流
            using var ms = new MemoryStream();
            cvFrame.WriteToStream(ms, ".jpg");
            ms.Seek(0, SeekOrigin.Begin);

            // 从流加载为 ImageSharp 图像
            using var image = Image.Load(ms);

            // 执行检测
            return _predictor!.Detect(image);
        }

        /// <summary>
        /// 确保模型已加载
        /// 如果模型未加载，抛出异常
        /// </summary>
        /// <exception cref="InvalidOperationException">模型未初始化时抛出</exception>
        private static void EnsureLoaded()
        {
            if (!_isLoaded || _predictor == null)
            {
                throw new InvalidOperationException("请先调用 FireDetectionModule.Initialize() 加载模型！");
            }
        }

        /// <summary>
        /// 获取模型是否已加载
        /// </summary>
        public static bool IsLoaded => _isLoaded;
    }
}
