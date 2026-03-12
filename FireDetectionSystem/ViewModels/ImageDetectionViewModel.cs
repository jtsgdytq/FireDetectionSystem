using Compunet.YoloSharp;
using Compunet.YoloSharp.Plotting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FireDetectionSystem.ViewModels
{
   public class ImageDetectionViewModel :BindableBase ,IDialogAware
    {


        private String imagePath;

        public String ImagePath
        {
            get { return imagePath; }
            set { imagePath = value; RaisePropertyChanged(); }
        }
        private ImageSource detectImage;

        public ImageSource DetectImage
        {
            get { return detectImage; }
            set { detectImage = value; RaisePropertyChanged(); }
        }

        private float confidence =0.5f;

        public float Confidence
        {
            get { return confidence; }
            set { confidence = value;RaisePropertyChanged(nameof(Confidence)); }
        }


        public DelegateCommand DetectionImageCommand { get; set; }

        public DelegateCommand SelectImagePathCommnad { get; set; }

        public ImageDetectionViewModel()
        {
            
            DetectionImageCommand = new DelegateCommand(DetectionImage);

            SelectImagePathCommnad = new DelegateCommand(SelectImagePath);
        }

        private void SelectImagePath()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif",
                Title = "Select an Image File"
            };

            if(openFileDialog.ShowDialog() == true)
            {
                ImagePath = openFileDialog.FileName;

            }
        }

        private async void DetectionImage()
        {
            var result = await FireDetectionModule.DetectAsync(ImagePath, new YoloConfiguration { Confidence=confidence });

            using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(ImagePath);

            // 修正：强制转换为 Image<Rgba32>
            using var plotted = result.PlotImage(img) as SixLabors.ImageSharp.Image<Rgba32>;

            if (plotted != null)
            {
                DetectImage = ImageSharpToBitmapImage(plotted);
            }
        }

        private BitmapImage ImageSharpToBitmapImage(
    SixLabors.ImageSharp.Image<Rgba32> image)
        {
            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze(); // ⭐ 跨线程必须

            return bitmap;
        }



        private async void Button_Click()
        {
            using var predictor = new YoloPredictor("D:\\下载\\ultralytics-main\\runs\\detect\\train4\\weights\\best.onnx");

            // 等待异步检测结果
            var result = await Task.Run(
                () => predictor.Detect("C:\\Users\\Seeney\\Desktop\\fire.jpg")
            );



            // 修正：使用 PlottingExtensions.PlotImage 并传入检测结果
            using var img = SixLabors.ImageSharp.Image.Load("C:\\Users\\Seeney\\Desktop\\fire.jpg");
            System.Diagnostics.Debug.WriteLine( result.GetType());
            using var plotted = result.PlotImage(img);
            using var ms = new MemoryStream();
            plotted.Save(ms, new PngEncoder());
            ms.Seek(0, SeekOrigin.Begin);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

        }


        #region 
        public DialogCloseListener RequestClose => throw new NotImplementedException();

        public bool CanCloseDialog()
        {
            return true;
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
