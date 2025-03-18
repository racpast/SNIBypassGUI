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
using SNIBypassGUI.Utils;
using static SNIBypassGUI.Utils.ConvertUtils.FileSizeConverter;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Utils.IniFileUtils;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Consts.PathConsts;
using MessageBox = HandyControl.Controls.MessageBox;
using System.Net.Http.Headers;

namespace SNIBypassGUI.Views
{
    public partial class CustomBackgroundWindow : Window
    {
        private bool _isFirstImage = true;
        public ImageSwitcherService BackgroundService => MainWindow.BackgroundService;
        private string currentImagePath;

        public class ImageItem
        {
            public BitmapImage ImageObj { get; set; }
            public string ImageName { get; set; }
            public string ImageResolution { get; set; }
            public string ImageSize { get; set; }
        }

        public CustomBackgroundWindow()
        {
            InitializeComponent();

            TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };

            DataContext = this;

            LoadImagesToList();
            BackgroundService.PropertyChanged += OnBackgroundChanged;
            BackgroundService._currentIndex = -1;
        }

        private void LoadImagesToList()
        {
            ImageListBox.ItemsSource = null;

            // 获取文件夹中的所有图像文件
            var imageFiles = Directory.GetFiles(BackgroundDirectory, "*.*", SearchOption.TopDirectoryOnly)
                                            .Where(file => Path.GetExtension(file).ToLower() == ".jpg" || Path.GetExtension(file).ToLower() == ".png" || Path.GetExtension(file).ToLower() == ".jpeg")
                                            .OrderBy(file => int.Parse(Path.GetFileNameWithoutExtension(file))) // 按数字排序
                                            .ToList();

            // 创建一个图像项的列表
            var imageList = new List<ImageItem>();

            foreach (var imagePath in imageFiles)
            {
                // 获取文件名
                string fileName = Path.GetFileName(imagePath);

                // 仅加载缩略图降低内存用量
                BitmapImage bitmapImage = LoadThumbnail(imagePath);

                (int, int) imageSize = GetImageSize(Path.Combine(BackgroundDirectory, fileName));

                // 创建 ImageItem 对象
                var imageItem = new ImageItem
                {
                    ImageName = fileName,
                    ImageObj = bitmapImage,
                    ImageResolution = $"{imageSize.Item1} x {imageSize.Item2}",
                    ImageSize = $"{ConvertBetweenUnits(GetFileSize(imagePath), SizeUnit.B, SizeUnit.MB):0.00} MB"
                };

                // 添加到列表中
                imageList.Add(imageItem);
            }

            // 设置 ImageListBox 的 ItemSource
            ImageListBox.ItemsSource = imageList;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SyncControlsFromConfig();
            FadeIn();
        }

        private void RemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ImageListBox.SelectedItem is not ImageItem selectedItem) return;

            var allFiles = Directory.GetFiles(BackgroundDirectory)
                .Where(file => Path.GetExtension(file).ToLower() == ".jpg" || Path.GetExtension(file).ToLower() == ".png" || Path.GetExtension(file).ToLower() == ".jpeg")
                .Select(f => new {
                    Path = f,
                    Index = int.Parse(Path.GetFileNameWithoutExtension(f))
                })
                .OrderBy(x => x.Index)
                .ToList();

            int deleteIndex = allFiles.FindIndex(x => x.Path == Path.Combine(BackgroundDirectory, selectedItem.ImageName));
            if (deleteIndex == -1) return;

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                int newIndex = 1;
                foreach (var file in allFiles)
                {
                    if (file.Index == deleteIndex + 1) continue;

                    string newName = $"{newIndex}{Path.GetExtension(file.Path)}";
                    File.Copy(file.Path, Path.Combine(tempDir, newName));
                    newIndex++;
                }

                allFiles.ForEach(f => TryDelete(f.Path));

                foreach (var tempFile in Directory.GetFiles(tempDir))
                {
                    string dest = Path.Combine(BackgroundDirectory, Path.GetFileName(tempFile));
                    File.Move(tempFile, dest);
                }

                ImageCropperControl.Source = null;
                ImageCropperControl.ResetDrawThumb();
                ImageListBox.SelectedItem = null;

                BackgroundService.ReloadByPath(currentImagePath);

                LoadImagesToList();
            }
            catch (Exception ex)
            {
                int newIndex = 1;
                foreach (var tempFile in Directory.GetFiles(tempDir))
                {
                    string originalName = allFiles[newIndex - 1].Path;
                    File.Copy(tempFile, originalName);
                }
                WriteLog("遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"操作失败，已恢复文件。\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new()
            {
                Title = "选择图片",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Filter = "图片 (*.jpg; *.jpeg; *.png)|*.jpg;*.jpeg;*.png",
                Multiselect = true
            };
            if (openFileDialog.ShowDialog() == true)
            {
                int existingFileCount = Directory.GetFiles(BackgroundDirectory, "*.*", SearchOption.TopDirectoryOnly).Count(file => new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(file).ToLower()));
                foreach(var file in openFileDialog.FileNames)
                {
                    try
                    {
                        File.Copy(file, Path.Combine(BackgroundDirectory, existingFileCount + 1 + Path.GetExtension(file)));
                        existingFileCount++;
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"遇到异常。", LogLevel.Error, ex);
                        MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                LoadImagesToList();
            }
        }

        private void UpBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ImageListBox.SelectedItem is not ImageItem selectedItem) return;

            // 获取当前选中索引
            int currentIndex = ImageListBox.SelectedIndex;
            if (currentIndex <= 0) return;

            // 释放UI资源
            ImageCropperControl.Source = null;
            GC.Collect();

            string prevName = ((ImageItem)ImageListBox.Items[currentIndex - 1]).ImageName;
            string currentName = selectedItem.ImageName;

            try
            {
                // 使用临时文件交换
                string temp1 = Path.Combine(BackgroundDirectory, Guid.NewGuid() + ".tmp");
                string temp2 = Path.Combine(BackgroundDirectory, Guid.NewGuid() + ".tmp");

                File.Move(Path.Combine(BackgroundDirectory, currentName), temp1);
                File.Move(Path.Combine(BackgroundDirectory, prevName), temp2);
                File.Move(temp1, Path.Combine(BackgroundDirectory, Path.GetFileNameWithoutExtension(prevName) + Path.GetExtension(currentName)));
                File.Move(temp2, Path.Combine(BackgroundDirectory, Path.GetFileNameWithoutExtension(currentName) + Path.GetExtension(prevName)));

                BackgroundService.ReloadByPath(Path.Combine(BackgroundDirectory, currentName));
                BackgroundService.ReloadByPath(Path.Combine(BackgroundDirectory, prevName));

                // 更新列表显示
                LoadImagesToList();
                ImageListBox.SelectedIndex = currentIndex - 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"上移失败！\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error );
                LoadImagesToList();
            }
        }

        private void DownBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ImageListBox.SelectedItem is not ImageItem selectedItem) return;

            // 获取当前选中索引
            int currentIndex = ImageListBox.SelectedIndex;
            if (currentIndex >= ImageListBox.Items.Count - 1) return;

            // 释放UI资源
            ImageCropperControl.Source = null;
            GC.Collect();

            string currentName = selectedItem.ImageName;
            string nextName = ((ImageItem)ImageListBox.Items[currentIndex + 1]).ImageName;

            try
            {
                // 使用临时文件交换
                string temp1 = Path.Combine(BackgroundDirectory, Guid.NewGuid() + ".tmp");
                string temp2 = Path.Combine(BackgroundDirectory, Guid.NewGuid() + ".tmp");

                File.Move(Path.Combine(BackgroundDirectory, currentName), temp1);
                File.Move(Path.Combine(BackgroundDirectory, nextName), temp2);
                File.Move(temp1, Path.Combine(BackgroundDirectory, Path.GetFileNameWithoutExtension(nextName) + Path.GetExtension(currentName)));
                File.Move(temp2, Path.Combine(BackgroundDirectory, Path.GetFileNameWithoutExtension(currentName) + Path.GetExtension(nextName)));

                BackgroundService.ReloadByPath(Path.Combine(BackgroundDirectory, currentName));
                BackgroundService.ReloadByPath(Path.Combine(BackgroundDirectory, nextName));

                // 更新列表显示
                LoadImagesToList();
                ImageListBox.SelectedIndex = currentIndex + 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下移失败！\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                LoadImagesToList();
            }
        }

        private void SetTimeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TimeTb.Text)) MessageBox.Show("请输入一个有效的值！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            else if (TimeTb.Text.Contains(".")) MessageBox.Show("请输入一个整数！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            else if (ConvertUtils.StringToInt(TimeTb.Text) < 1) MessageBox.Show("时间间隔不能小于一秒！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
            {
                INIWrite("背景设置", "ChangeInterval", TimeTb.Text, INIPath);
                MessageBox.Show("设置成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                SyncControlsFromConfig();

                // 通知服务重载
                MainWindow.BackgroundService.ReloadConfig();
            }
        }

        private void ToggleModeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (INIRead("背景设置", "ChangeMode", INIPath) == "Sequential") INIWrite("背景设置", "ChangeMode", "Random", INIPath);
            else INIWrite("背景设置", "ChangeMode", "Sequential", INIPath);
            SyncControlsFromConfig();

            // 通知服务重载
            MainWindow.BackgroundService.ReloadConfig();
        }

        private void CutBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentImagePath))
            {
                // 定义裁剪区域             
                Rectangle cropArea = new((int)ImageCropperControl.CroppedRegion.X, (int)ImageCropperControl.CroppedRegion.Y, (int)ImageCropperControl.CroppedRegion.Width, (int)ImageCropperControl.CroppedRegion.Height);

                try
                {
                    // 使用 GetImage 获取 BitmapImage
                    BitmapImage bitmapImage = GetImage(currentImagePath);

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

                    BackgroundService.ReloadByPath(currentImagePath);

                    ImageCropperControl.Source = null;
                    ImageCropperControl.ResetDrawThumb();
                    ImageListBox.SelectedItem = null;

                    MessageBox.Show("图像裁剪完成！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

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
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        private void ResetBtn_Click(object sender, RoutedEventArgs e) => ImageCropperControl.ResetDrawThumb();

        private async void DoneBtn_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.BackgroundService.ReloadConfig();

            ImageListBox.ItemsSource = null;
            ImageCropperControl.Source = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();

            await FadeOut(true);
        }

        private void ImageListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ImageListBox.SelectedItem is not ImageItem imageItem) {
                currentImagePath = null;
                return;
            }
            currentImagePath = Path.Combine(BackgroundDirectory, imageItem.ImageName);

            // 将文件内容复制到内存流
            using (var fileStream = new FileStream(currentImagePath, FileMode.Open, FileAccess.Read))
            {
                using var memoryStream = new MemoryStream();
                fileStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                // 加载图片
                ImageCropperControl.LoadImageFromStream(memoryStream);
            }
            ImageCropperControl.ResetDrawThumb();
        }

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

        private void SyncControlsFromConfig()
        {
            TimeTb.Text = INIRead("背景设置", "ChangeInterval", INIPath);
            if (INIRead("背景设置", "ChangeMode", INIPath) == "Random") ToggleModeBtn.Content = "随机模式";
            else ToggleModeBtn.Content = "顺序模式";
        }

        private BitmapImage LoadThumbnail(string path, int maxThumbnailSize = 100)
        {
            var image = new BitmapImage();
            try
            {
                image.BeginInit();
                image.UriSource = new Uri(path);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                // 动态设置解码尺寸
                var (w, h) = GetImageSize(path);
                if (w > maxThumbnailSize || h > maxThumbnailSize)
                {
                    var ratio = Math.Min((double)maxThumbnailSize / w, (double)maxThumbnailSize / h);
                    image.DecodePixelWidth = (int)(w * ratio);
                }

                image.EndInit();
                image.Freeze();
            }
            catch { /*...*/ }
            return image;
        }

        private (int, int) GetImageSize(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                return (frame.PixelWidth, frame.PixelHeight);
            }
            catch { return (0, 0); }
        }

        private void OnBackgroundChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ImageSwitcherService.CurrentImage)) return;

            Dispatcher.BeginInvoke(() =>
            {
                if (_isFirstImage)
                {
                    CurrentImage.Source = BackgroundService.CurrentImage;
                    CurrentImage.Opacity = 1;
                    NextImage.Opacity = 0;
                    _isFirstImage = false;
                    return;
                }

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
