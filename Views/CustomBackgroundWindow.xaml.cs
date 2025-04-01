using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Consts.ConfigConsts;
using static SNIBypassGUI.Consts.PathConsts;
using SNIBypassGUI.Utils;
using static SNIBypassGUI.Utils.ConvertUtils.FileSizeConverter;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Utils.IniFileUtils;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.StringUtils;
using MessageBox = HandyControl.Controls.MessageBox;

namespace SNIBypassGUI.Views
{
    public partial class CustomBackgroundWindow : Window
    {
        // 暴露 BackgroundService 以便访问
        public ImageSwitcherService BackgroundService => MainWindow.BackgroundService;

        // 当前图片路径
        private string currentImagePath;

        // 哈希值到文件路径的映射
        private readonly Dictionary<string, string> hashToPathMap = [];

        // 图像项类
        public class ImageItem
        {
            public BitmapImage ImageObj { get; set; }
            public string ImageName { get; set; }
            public string ImagePath { get; set; }
            public string ImageResolution { get; set; }
            public string ImageSize { get; set; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public CustomBackgroundWindow()
        {
            WriteLog("进入 CustomBackgroundWindow。", LogLevel.Debug);

            InitializeComponent();

            // 使窗口可拖动
            TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };

            // 设置 DataContext
            DataContext = this;

            // 加载图像列表
            LoadImagesToList();

            // 订阅事件
            BackgroundService.PropertyChanged += OnBackgroundChanged;
            CurrentImage.Source = BackgroundService.CurrentImage;

            WriteLog("完成 CustomBackgroundWindow。", LogLevel.Debug);
        }

        /// <summary>
        /// 加载图像列表
        /// </summary>
        private void LoadImagesToList()
        {
            WriteLog("进入 LoadImagesToList。", LogLevel.Debug);

            ImageListBox.ItemsSource = null;

            // 获取文件夹中所有的图像文件并按照 ImageOrder 中的哈希值顺序
            string[] imageOrder = INIRead(BackgroundSettings, ImageOrder, INIPath).Split([','], StringSplitOptions.RemoveEmptyEntries);
            foreach (var filePath in Directory.EnumerateFiles(BackgroundDirectory, "*.*", SearchOption.AllDirectories).Where(file => ImageExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase))))
            {
                string hash = CalculateFileHash(filePath);
                hashToPathMap[hash] = filePath;
            }

            // 创建一个图像项的列表
            var imageList = new List<ImageItem>();
            foreach (var hash in imageOrder)
            {
                if (hashToPathMap.TryGetValue(hash, out string path))
                {
                    // 仅加载小图降低内存用量
                    BitmapImage bitmapImage = LoadImage(path, maxDecodeSize:100);

                    (int, int) imageSize = GetImageSize(path);

                    // 创建 ImageItem 对象
                    var imageItem = new ImageItem
                    {
                        ImageName = Path.GetFileName(path),
                        ImagePath = path,
                        ImageObj = bitmapImage,
                        ImageResolution = $"{imageSize.Item1} x {imageSize.Item2}",
                        ImageSize = $"{ConvertBetweenUnits(GetFileSize(path), SizeUnit.B, SizeUnit.MB):0.00} MB"
                    };

                    // 添加到列表中
                    imageList.Add(imageItem);
                }
            }

            // 设置 ImageListBox 的 ItemSource
            ImageListBox.ItemsSource = imageList;

            WriteLog("完成 LoadImagesToList。", LogLevel.Debug);
        }

        /// <summary>
        /// 窗口加载完成事件
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 Window_Loaded。", LogLevel.Debug);
            SyncControlsFromConfig();
            FadeIn();
            WriteLog("完成 Window_Loaded。", LogLevel.Debug);
        }

        /// <summary>
        /// 移除按钮点击事件
        /// </summary>
        private void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 RemoveBtn_Click。", LogLevel.Debug);

            if (ImageListBox.SelectedItem is not ImageItem selectedItem) return;

            // 如果只有一张图片则提示
            if (ImageListBox.Items.Count == 1)
            {
                MessageBox.Show("至少要有一张背景图片！", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 删除选中的图片并更新配置文件
                string hashString = INIRead(BackgroundSettings, ImageOrder, INIPath);
                string hashToRemove = CalculateFileHash(selectedItem.ImagePath);
                hashString = RemoveHash(hashString, hashToRemove);

                // 写入配置文件
                INIWrite(BackgroundSettings, ImageOrder, hashString, INIPath);

                // 删除文件
                TryDelete(selectedItem.ImagePath);

                // 释放资源
                ImageCropperControl.Source = null;
                ImageCropperControl.ResetDrawThumb();
                ImageListBox.SelectedItem = null;

                // 通知服务重载
                BackgroundService.CleanCacheByPath(currentImagePath);
                BackgroundService.ReloadConfig();
                BackgroundService.ValidateCurrentImage();

                // 更新列表显示
                LoadImagesToList();
            }
            catch (Exception ex)
            {
                WriteLog("遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"操作失败。\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成 RemoveBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 添加按钮点击事件
        /// </summary>
        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 AddBtn_Click。", LogLevel.Debug);

            OpenFileDialog openFileDialog = new()
            {
                Title = "选择图片",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Filter = "图片 (*.jpg; *.jpeg; *.png)|*.jpg;*.jpeg;*.png",
                Multiselect = true
            };

            // 如果用户选择了文件
            if (openFileDialog.ShowDialog() == true)
            {
                var files = openFileDialog.FileNames;
                // 检查是否超过最大数量，按照 INIRead 中的最大缓冲区计算，最多可容纳 1008 个 SHA-256 哈希，因此上限应为 1008 张图片
                //if (ImageListBox.Items.Count + files.Length > MaxImageCount)
                //{
                //    MessageBox.Show($"您添加的图片数量已超过最大 {MaxImageCount} 张的限制，为避免内存占用过高，请勿添加过多图片。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                //    return;
                //}
                // 将文件复制到背景文件夹并更新配置文件
                foreach (var file in files)
                {
                    try
                    {
                        File.Copy(file, Path.Combine(BackgroundDirectory, Path.GetFileName(file)), true);

                        // 追加新哈希值
                        string hashString = INIRead(BackgroundSettings, ImageOrder, INIPath);
                        string hashToAdd = CalculateFileHash(file);
                        string newHashString = AppendHash(hashString, hashToAdd);

                        // 写入配置文件
                        INIWrite(BackgroundSettings, ImageOrder, newHashString, INIPath);

                        // 通知服务重载
                        BackgroundService.ReloadConfig();
                    }
                    catch (Exception ex)
                    {
                        WriteLog("遇到异常。", LogLevel.Error, ex);
                        MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // 更新列表显示
                LoadImagesToList();
            }

            WriteLog("完成 AddBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 上移按钮点击事件
        /// </summary>
        private void UpBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 UpBtn_Click。", LogLevel.Debug);

            if (ImageListBox.SelectedItem is not ImageItem selectedItem) return;

            // 获取当前选中索引
            int currentIndex = ImageListBox.SelectedIndex;
            if (currentIndex <= 0) return;

            try
            {
                // 上移选中的图片并更新配置文件
                string prevName = ((ImageItem)ImageListBox.Items[currentIndex - 1]).ImagePath;
                string currentName = selectedItem.ImagePath;
                string hashString = INIRead(BackgroundSettings, ImageOrder, INIPath);
                string prevHash = CalculateFileHash(prevName);
                string currentHash = CalculateFileHash(currentName);
                string updatedHashString = SwapHashPositions(hashString, prevHash, currentHash);
                INIWrite(BackgroundSettings, ImageOrder, updatedHashString, INIPath);

                // 通知服务重载
                BackgroundService.ReloadConfig();

                // 更新列表显示
                LoadImagesToList();

                // 重新选中上一个
                ImageListBox.SelectedIndex = currentIndex - 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"上移失败！\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error );
                LoadImagesToList();
            }

            WriteLog("完成 UpBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 下移按钮点击事件
        /// </summary>
        private void DownBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 DownBtn_Click。", LogLevel.Debug);

            if (ImageListBox.SelectedItem is not ImageItem selectedItem) return;

            // 获取当前选中索引
            int currentIndex = ImageListBox.SelectedIndex;
            if (currentIndex >= ImageListBox.Items.Count - 1) return;

            try
            {
                // 下移选中的图片并更新配置文件
                string nextName = ((ImageItem)ImageListBox.Items[currentIndex + 1]).ImagePath;
                string currentName = selectedItem.ImagePath;
                string hashString = INIRead(BackgroundSettings, ImageOrder, INIPath);
                string nextHash = CalculateFileHash(nextName);
                string currentHash = CalculateFileHash(currentName);
                string updatedHashString = SwapHashPositions(hashString, nextHash, currentHash);
                INIWrite(BackgroundSettings, ImageOrder, updatedHashString, INIPath);

                // 通知服务重载
                BackgroundService.ReloadConfig();

                // 更新列表显示
                LoadImagesToList();

                // 重新选中下一个
                ImageListBox.SelectedIndex = currentIndex + 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下移失败！\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadImagesToList();
            }

            WriteLog("完成 DownBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 设置时间按钮点击事件
        /// </summary>
        private void SetTimeBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 SetTimeBtn_Click。", LogLevel.Debug);

            // 检查输入是否有效
            if (string.IsNullOrEmpty(TimeTb.Text)) MessageBox.Show("请输入一个有效的值！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            else if (TimeTb.Text.Contains(".")) MessageBox.Show("请输入一个整数！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            else if (ConvertUtils.StringToInt(TimeTb.Text) < 1) MessageBox.Show("时间间隔不能小于一秒！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
            {
                // 写入配置文件
                INIWrite(BackgroundSettings, ChangeInterval, TimeTb.Text, INIPath);

                // 提示设置成功
                MessageBox.Show("设置成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                // 从配置文件同步控件
                SyncControlsFromConfig();

                // 通知服务重载
                BackgroundService.ReloadConfig();
            }

            WriteLog("完成 SetTimeBtn_Click。", LogLevel.Debug);
        }

        private void ToggleModeBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 ToggleModeBtn_Click。", LogLevel.Debug);

            if (INIRead(BackgroundSettings, ChangeMode, INIPath) == SequentialMode) INIWrite(BackgroundSettings, ChangeMode, RandomMode, INIPath);
            else INIWrite(BackgroundSettings, ChangeMode, SequentialMode, INIPath);
            SyncControlsFromConfig();

            // 通知服务重载
            BackgroundService.ReloadConfig();

            WriteLog("完成 ToggleModeBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 裁剪按钮点击事件
        /// </summary>
        private void CutBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 CutBtn_Click。", LogLevel.Debug);

            if (!string.IsNullOrEmpty(currentImagePath))
            {
                CutBtn.IsEnabled = false;

                // 定义裁剪区域             
                Rectangle cropArea = new((int)ImageCropperControl.CroppedRegion.X, (int)ImageCropperControl.CroppedRegion.Y, (int)ImageCropperControl.CroppedRegion.Width, (int)ImageCropperControl.CroppedRegion.Height);

                try
                {
                    string originHash = CalculateFileHash(currentImagePath);

                    // 使用 GetImage 获取 BitmapImage
                    BitmapImage bitmapImage = LoadImage(currentImagePath);

                    // 将 BitmapImage 转换为 Bitmap
                    using MemoryStream memoryStream = new();

                    // 将 BitmapImage 写入到 MemoryStream 中
                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(bitmapImage));
                    encoder.Save(memoryStream);

                    // 使用 MemoryStream 创建一个 Bitmap 对象
                    using Bitmap original = new(memoryStream);

                    // 创建一个新的 Bitmap 对象，大小与裁剪区域一致
                    using Bitmap croppedImage = new(cropArea.Width, cropArea.Height);

                    // 创建 Graphics 对象，绘制裁剪后的图像
                    using Graphics g = Graphics.FromImage(croppedImage);
                    g.DrawImage(original, new Rectangle(0, 0, cropArea.Width, cropArea.Height), cropArea, GraphicsUnit.Pixel);

                    // 生成一个临时文件路径
                    string tempImagePath = Path.Combine(Path.GetDirectoryName(currentImagePath), $"cropped_{Path.GetFileName(currentImagePath)}");

                    // 保存裁剪后的图片
                    croppedImage.Save(tempImagePath);

                    // 尝试释放资源，确保文件可以替换
                    original.Dispose();
                    croppedImage.Dispose();

                    // 删除原始文件并替换
                    File.Replace(tempImagePath, currentImagePath, null);

                    // 替换哈希值
                    string newHash = CalculateFileHash(currentImagePath);
                    string hashString = INIRead(BackgroundSettings, ImageOrder, INIPath);
                    hashString = ReplaceHash(hashString, originHash, newHash);

                    // 写入配置文件
                    INIWrite(BackgroundSettings, ImageOrder, hashString, INIPath);

                    // 通知服务重载
                    BackgroundService.ReloadByPath(currentImagePath);

                    // 释放资源
                    ImageCropperControl.Source = null;
                    ImageCropperControl.ResetDrawThumb();
                    ImageListBox.SelectedItem = null;

                    // 提示操作成功
                    MessageBox.Show("图像裁剪完成！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 更新列表显示
                    LoadImagesToList();
                }
                catch (IOException ex)
                {
                    WriteLog("遇到异常。", LogLevel.Error, ex);
                    MessageBox.Show($"文件操作失败！\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    WriteLog("遇到异常。", LogLevel.Error, ex);
                    MessageBox.Show($"图像裁剪失败！\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // 释放资源
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            WriteLog("完成 CutBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 重置按钮点击事件
        /// </summary>
        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 ResetBtn_Click。", LogLevel.Debug);
            ImageCropperControl.ResetDrawThumb();
            WriteLog("完成 ResetBtn_Click。", LogLevel.Debug);
        }

        private async void DoneBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 DoneBtn_Click。", LogLevel.Debug);

            // 禁用按钮
            DoneBtn.IsEnabled = false;

            // 通知服务重载
            BackgroundService.ReloadConfig();

            // 释放资源
            ImageListBox.ItemsSource = null;
            ImageCropperControl.Source = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 淡出
            await FadeOut(true);

            WriteLog("完成 DoneBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 图片列表选择改变事件
        /// </summary>
        private void ImageListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WriteLog("进入 ImageListBox_SelectionChanged。", LogLevel.Debug);

            if (ImageListBox.SelectedItem is not ImageItem imageItem) {
                currentImagePath = null;
                CutBtn.IsEnabled = false;
                return;
            }
            currentImagePath = imageItem.ImagePath;

            // 将文件内容复制到内存流
            using (var fileStream = new FileStream(currentImagePath, FileMode.Open, FileAccess.Read))
            {
                using var memoryStream = new MemoryStream();
                fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                // 加载图片
                ImageCropperControl.LoadImageFromStream(memoryStream);

                // 启用裁剪按钮
                CutBtn.IsEnabled = true;
            }

            // 重置裁剪区域
            ImageCropperControl.ResetDrawThumb();

            WriteLog("完成 ImageListBox_SelectionChanged。", LogLevel.Debug);
        }

        /// <summary>
        /// 从配置文件同步控件
        /// </summary>
        private void SyncControlsFromConfig()
        {
            WriteLog("进入 SyncControlsFromConfig。", LogLevel.Debug);
            TimeTb.Text = INIRead(BackgroundSettings, ChangeInterval, INIPath);
            if (INIRead(BackgroundSettings, ChangeMode, INIPath) == RandomMode) ToggleModeBtn.Content = "随机模式";
            else ToggleModeBtn.Content = "顺序模式";
            WriteLog("完成 SyncControlsFromConfig。", LogLevel.Debug);
        }

        /// <summary>
        /// 淡入
        /// </summary>
        private void FadeIn()
        {
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.8)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);
        }

        /// <summary>
        /// 淡出
        /// </summary>
        private async Task FadeOut(bool dialogResult = false)
        {
            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(0.8)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            BeginAnimation(OpacityProperty, fadeOut);

            // 等待动画完成
            await Task.Delay(800);

            // 设置对话框结果并关闭
            DialogResult = dialogResult;
            Close();
        }

        /// <summary>
        /// 背景动画逻辑
        /// </summary>
        private void OnBackgroundChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ImageSwitcherService.CurrentImage)) return;

            Dispatcher.BeginInvoke(() =>
            {
                NextImage.Opacity = 0;
                NextImage.Source = BackgroundService.CurrentImage;

                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1));
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1));

                CurrentImage.BeginAnimation(OpacityProperty, fadeOut);
                NextImage.BeginAnimation(OpacityProperty, fadeIn);

                (NextImage, CurrentImage) = (CurrentImage, NextImage);
            });
        }
    }
}
