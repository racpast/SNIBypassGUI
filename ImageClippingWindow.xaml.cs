using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;

namespace SNIBypassGUI
{
    /// <summary>
    /// ImageClippingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ImageClippingWindow : Window
    {
        private string imagePath;

        public ImageClippingWindow(string _imagePath)
        {
            InitializeComponent();
            imagePath = _imagePath;
            // 窗口可拖动
            this.TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载图片
            ImageCropperControl.LoadImageFromFile(imagePath);
        }

        private void OKBtn_Click(object sender, RoutedEventArgs e)
        {       
            // 定义裁剪区域             
            Rectangle cropArea = new Rectangle((int)ImageCropperControl.CroppedRegion.X, (int)ImageCropperControl.CroppedRegion.Y, (int)ImageCropperControl.CroppedRegion.Width, (int)ImageCropperControl.CroppedRegion.Height);

            // 加载图片
            Bitmap original = new Bitmap(imagePath);

            // 创建一个新的Bitmap对象，大小与裁剪区域一致
            Bitmap croppedImage = new Bitmap(cropArea.Width, cropArea.Height);

            // 创建Graphics对象，绘制裁剪后的图像
            using (Graphics g = Graphics.FromImage(croppedImage))
            {
                // 执行裁剪操作，将裁剪区域绘制到新的图像中
                g.DrawImage(original, new Rectangle(0, 0, cropArea.Width, cropArea.Height), cropArea, GraphicsUnit.Pixel);
            }

            // 保存裁剪后的图片
            croppedImage.Save(Path.Combine(PathsSet.CustomBackground));

            this.DialogResult = true;

            this.Close();
        }

        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            ImageCropperControl.ResetDrawThumb();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;

            this.Close();
        }

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            HelpBadge.Visibility = Visibility.Collapsed;
            Process.Start(new ProcessStartInfo(LinksSet.如何使用自定义背景功能) { UseShellExecute = true });
        }
    }

}
