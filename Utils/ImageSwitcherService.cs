using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Consts.ConfigConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.ConvertUtils;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Utils.IniFileUtils;

namespace SNIBypassGUI.Utils
{
    public class ImageSwitcherService : INotifyPropertyChanged
    {
        // 存储文件哈希到图片路径的映射
        private readonly Dictionary<string, string> hashToImagePath = [];

        // 存储图片路径到已加载图片的缓存
        private readonly Dictionary<string, BitmapImage> pathToImageCache = [];

        // 用于同步重载操作的锁对象
        private readonly object _reloadLock = new();

        // 随机数生成器，用于随机选择图片
        private readonly Random _random = new();

        // 定时器，用于定期更换图片
        private DispatcherTimer _timer;

        // 预加载图片的索引
        private int preloadIndex = -1;

        // 图片顺序列表
        private List<string> imageOrder = [];

        // 当前显示的图片索引
        public int _currentIndex = -1;

        // 属性变更事件
        public event PropertyChangedEventHandler PropertyChanged;

        // 当前显示的图片
        public ImageSource CurrentImage { get; private set; }

        // 更换图片的模式（顺序或随机）
        public string changeMode { get; private set; }

        // 更换图片的时间间隔
        public int changeInterval { get; private set; }

        /// <summary>
        /// 服务构造函数
        /// </summary>
        public ImageSwitcherService() => InitializeService();

        /// <summary>
        /// 初始化服务核心逻辑
        /// </summary>
        private void InitializeService()
        {
            ReloadConfig();

            // 如果有多张图片，启动定时器
            if (imageOrder.Count > 1)
            {
                _timer = new DispatcherTimer();
                UpdateTimer();
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }

            // 如果有图片，设置当前显示的图片
            if (imageOrder.Any())
            {
                if (changeMode == SequentialMode)
                {
                    if (hashToImagePath.TryGetValue(imageOrder.First(), out string firstPath))
                    {
                        CurrentImage = LoadImage(firstPath, pathToImageCache, MaxDecodeSize);
                        _currentIndex = 0;
                    }
                    else
                    {
                        var first = hashToImagePath.First();
                        CurrentImage = LoadImage(first.Value, pathToImageCache, MaxDecodeSize);
                        _currentIndex = imageOrder.IndexOf(first.Key);
                    }
                }
                else
                {
                    preloadIndex = _random.Next(imageOrder.Count);
                    if (hashToImagePath.TryGetValue(imageOrder[preloadIndex], out string preloadPath))
                    {
                        CurrentImage = LoadImage(preloadPath, pathToImageCache, MaxDecodeSize);
                        _currentIndex = preloadIndex;
                    }
                    else
                    {
                        var first = hashToImagePath.First();
                        CurrentImage = LoadImage(first.Value, pathToImageCache, MaxDecodeSize);
                        _currentIndex = imageOrder.IndexOf(first.Key);
                    }
                }

                // 触发属性变更事件，确保窗口能接收到初始图像
                OnPropertyChanged(nameof(CurrentImage));
            }
        }

        /// <summary>
        /// 验证当前图片是否存在
        /// </summary>
        public void ValidateCurrentImage()
        {
            if (CurrentImage == null) return;

            string currentPath = ((BitmapImage)CurrentImage).UriSource.AbsolutePath;
            if (!hashToImagePath.Values.Contains(currentPath))
            {
                if (imageOrder.Any())
                {
                    string firstPath = hashToImagePath[imageOrder[0]];
                    CurrentImage = LoadImage(firstPath, pathToImageCache, MaxDecodeSize);
                    OnPropertyChanged(nameof(CurrentImage));
                }
                else
                {
                    CurrentImage = null;
                    OnPropertyChanged(nameof(CurrentImage));
                }
            }
        }

        /// <summary>
        /// 重新加载所有配置
        /// </summary>
        public void ReloadConfig()
        {
            lock (_reloadLock)
            {
                // 从配置文件读取更换模式和间隔
                changeMode = INIRead(BackgroundSettings, ChangeMode, INIPath);
                changeInterval = Math.Max(1, StringToInt(INIRead(BackgroundSettings, ChangeInterval, INIPath)));

                // 从配置文件读取图片顺序
                imageOrder = [.. INIRead(BackgroundSettings, ImageOrder, INIPath).Split([','], StringSplitOptions.RemoveEmptyEntries)];
                preloadIndex = _random.Next(imageOrder.Count);
                hashToImagePath.Clear();

                // 遍历背景目录，获取所有图片文件的哈希值和路径
                foreach (var filePath in Directory.EnumerateFiles(BackgroundDirectory, "*.*", SearchOption.AllDirectories).Where(file => ImageExtensions.Any(ext => file.EndsWith(ext, StringComparison.OrdinalIgnoreCase))))
                {
                    string hash = CalculateFileHash(filePath);
                    hashToImagePath[hash] = filePath;
                }

                // 重置索引到安全位置
                _currentIndex = Math.Min(_currentIndex, imageOrder.Count - 1);

                // 更新定时器间隔
                UpdateTimer();
            }
        }

        /// <summary>
        /// 更新定时器间隔
        /// </summary>
        private void UpdateTimer()
        {
            if (_timer != null) _timer.Interval = TimeSpan.FromSeconds(changeInterval);
            if (imageOrder.Count > 1) _timer?.Start();
            else _timer?.Stop();
        }

        /// <summary>
        /// 重置图片顺序
        /// </summary>
        private void ResetImageOrder()
        {
            var files = ImageExtensions.SelectMany(ext => Directory.GetFiles(BackgroundDirectory, $"*{ext}"));
            var hashes = files.Select(CalculateFileHash);
            string imageOrder = string.Join(",", hashes);
            INIWrite(BackgroundSettings, ImageOrder, imageOrder, INIPath);
        }

        /// <summary>
        /// 计时器触发事件
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!imageOrder.Any()) return;
            var nextIndex = CalculateNextIndex();
            if (hashToImagePath.TryGetValue(imageOrder[nextIndex], out string nextImagePath))
            {
                var nextImage = LoadImage(nextImagePath, pathToImageCache, MaxDecodeSize);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentImage = nextImage;
                    OnPropertyChanged(nameof(CurrentImage));
                    _currentIndex = nextIndex;
                });
            }
            else
            {
                ResetImageOrder();
                ReloadConfig();
            }
        }

        /// <summary>
        /// 计算下一个索引
        /// </summary>
        private int CalculateNextIndex()
        {
            const int MAX_ATTEMPTS = 100;

            // 顺序模式下，直接返回下一个索引
            if (changeMode == SequentialMode) return (_currentIndex + 1) % imageOrder.Count;

            if (imageOrder.Count <= 1) return 0;

            int newIndex;
            int attempts = 0;

            // 随机模式下，避免重复
            do
            {
                newIndex = _random.Next(imageOrder.Count);
                attempts++;
            } while (newIndex == _currentIndex && attempts < MAX_ATTEMPTS);

            return newIndex == _currentIndex ? (newIndex + 1) % imageOrder.Count : newIndex;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            _timer?.Stop();
            CurrentImage = null;
            pathToImageCache.Clear();
        }

        /// <summary>
        /// 清空所有图片的缓存
        /// </summary>
        public void CleanAllCache() => pathToImageCache.Clear();

        /// <summary>
        /// 清理指定路径图片的缓存
        /// </summary>
        public void CleanCacheByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var keysToRemove = pathToImageCache.Keys
                .Where(k => string.Equals(k, path, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var key in keysToRemove) pathToImageCache.Remove(key);
        }

        /// <summary>
        /// 重载指定路径的图片
        /// </summary>
        public void ReloadByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // 强制移除缓存
            CleanCacheByPath(path);

            if (imageOrder.Count > 0 && _currentIndex >= 0 && _currentIndex < imageOrder.Count)
            {
                // 如果当前显示的是该路径
                if (string.Equals(hashToImagePath[imageOrder[_currentIndex]], path, StringComparison.OrdinalIgnoreCase))
                {
                    // 创建新实例
                    var newImage = LoadImage(path, pathToImageCache, MaxDecodeSize);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentImage = newImage;
                        OnPropertyChanged(nameof(CurrentImage));
                    });
                }
                else pathToImageCache[path] = LoadImage(path, pathToImageCache, MaxDecodeSize);
            }
        }

        /// <summary>
        /// 属性变更通知
        /// </summary>
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}