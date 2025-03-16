using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using static SNIBypassGUI.Utils.ProcessUtils;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Consts.LinksConsts;

namespace SNIBypassGUI.Views
{
    public partial class ImageClippingWindow : Window
    {
        private readonly string imagePath;

        /// <summary>
        /// 窗口构造函数
        /// </summary>
        public ImageClippingWindow(string _imagePath)
        {
            InitializeComponent();
            imagePath = _imagePath;
            TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };
        }

        /// <summary>
        /// 窗口加载事件
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ImageCropperControl.LoadImageFromFile(imagePath);
            FadeIn();
        }

        /// <summary>
        /// 确认裁剪按钮点击事件
        /// </summary>
        private async void OKBtn_Click(object sender, RoutedEventArgs e)
        {       
            // 定义裁剪区域             
            Rectangle cropArea = new((int)ImageCropperControl.CroppedRegion.X, (int)ImageCropperControl.CroppedRegion.Y, (int)ImageCropperControl.CroppedRegion.Width, (int)ImageCropperControl.CroppedRegion.Height);

            // 加载图片
            Bitmap original = new(imagePath);

            // 创建一个新的 Bitmap 对象，大小与裁剪区域一致
            Bitmap croppedImage = new(cropArea.Width, cropArea.Height);

            // 创建 Graphics 对象，绘制裁剪后的图像
            using (Graphics g = Graphics.FromImage(croppedImage))
            {
                g.DrawImage(original, new Rectangle(0, 0, cropArea.Width, cropArea.Height), cropArea, GraphicsUnit.Pixel);
            }

            // 保存裁剪后的图片
            croppedImage.Save(Path.Combine(CustomBackground));

            await FadeOut(true);
        }

        /// <summary>
        /// 重置按钮点击事件
        /// </summary>
        private void ResetBtn_Click(object sender, RoutedEventArgs e) => ImageCropperControl.ResetDrawThumb();

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private async void CancelBtn_Click(object sender, RoutedEventArgs e) => await FadeOut();

        /// <summary>
        /// 帮助按钮点击事件
        /// </summary>
        private void HelpBtn_Click(object sender, RoutedEventArgs e) =>  StartProcess(如何使用自定义背景功能, useShellExecute: true);

        private void FadeIn()
        {
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.8)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);
        }

        private async Task FadeOut(bool dialogResult = false)
        {
            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.8)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            BeginAnimation(OpacityProperty, fadeOut);
            await Task.Delay(800);
            DialogResult = dialogResult;
            Close();
        }
    }

}
