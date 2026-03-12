using Compunet.YoloSharp;
using Compunet.YoloSharp.Data;
using OpenCvSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.IO;
// using YourYoloLibraryNamespace; // 替换为你 YoloPredictor 所在的命名空间

public static class FireDetectionModule
{
    // 1. 静态实例：保证全生命周期只加载一次模型，节省内存和时间
    private static YoloPredictor _predictor;
    private static bool _isLoaded = false;

    /// <summary>
    /// 系统启动时调用一次，加载模型
    /// </summary>
    public static void Initialize(string modelPath)
    {
        if (_isLoaded) return;

        try
        {
            // 初始化你的预测器 (根据你使用的库，可能构造函数略有不同)
            _predictor = new YoloPredictor(modelPath);

            
            _isLoaded = true;
            
        }
        catch (Exception ex)
        {
            throw new Exception($"模型加载失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 核心检测方法 - 支持图片文件路径
    /// </summary>
    public static async Task<Compunet.YoloSharp.Data.YoloResult<Compunet.YoloSharp.Data.Detection>> DetectAsync(string imagePath, YoloConfiguration? configuration = null)
    {
        EnsureLoaded();
        using var image = Image.Load<Rgba32>(imagePath);
        return await Task.Run(() => _predictor.Detect(imagePath, configuration));
    }

    /// <summary>
    /// 核心检测方法 - 支持内存流/ImageSharp对象 (为视频帧准备)
    /// </summary>
    public static Compunet.YoloSharp.Data.YoloResult<Compunet.YoloSharp.Data.Detection> Detect(Image image)
    {
        EnsureLoaded();
        // 视频流通常要求同步快速处理，或者在外部包裹 Task
        return _predictor.Detect(image); // 假设你的库支持传入 Image 对象
    }

    /// <summary>
    /// 核心检测方法 - 专门针对 OpenCvSharp (摄像头/视频)
    /// </summary>
    public static Compunet.YoloSharp.Data.YoloResult<Compunet.YoloSharp.Data.Detection> DetectFrame(Mat cvFrame)
    {
        EnsureLoaded();

        // 【关键】将 OpenCV 的 Mat (BGR) 转换为 ImageSharp 的 Image (RGB)
       
        using var ms = new MemoryStream();
        cvFrame.WriteToStream(ms, ".jpg"); // 编码为流
        ms.Seek(0, SeekOrigin.Begin);

        using var image = Image.Load(ms);
        return _predictor.Detect(image); 
    }

    private static void EnsureLoaded()
    {
        if (!_isLoaded || _predictor == null)
            throw new InvalidOperationException("请先调用 FireDetectionModule.Initialize() 加载模型！");
    }
}