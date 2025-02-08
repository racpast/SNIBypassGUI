using System.Drawing;
using System.IO;
using System.Windows;
using static SNIBypassGUI.Utils.ProcessUtils;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Consts.LinksConsts;

namespace SNIBypassGUI
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
        private void Window_Loaded(object sender, RoutedEventArgs e) => ImageCropperControl.LoadImageFromFile(imagePath);

        /// <summary>
        /// 确认裁剪按钮点击事件
        /// </summary>
        private void OKBtn_Click(object sender, RoutedEventArgs e)
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

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 重置按钮点击事件
        /// </summary>
        private void ResetBtn_Click(object sender, RoutedEventArgs e) => ImageCropperControl.ResetDrawThumb();

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 帮助按钮点击事件
        /// </summary>
        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            HelpBadge.Visibility = Visibility.Collapsed;
            StartProcess(如何使用自定义背景功能, useShellExecute: true);
        }
    }

}
