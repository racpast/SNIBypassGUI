using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static SNIBypassGUI.Utils.ConvertUtils;
using static SNIBypassGUI.Utils.IniFileUtils;
using static SNIBypassGUI.Consts.PathConsts;

public class ImageSwitcherService : INotifyPropertyChanged
{
    public int _currentIndex = -1;
    private const int MaxDecodeSize = 1400;
    public event PropertyChangedEventHandler PropertyChanged;
    private readonly Random _random = new();
    private DispatcherTimer _timer;
    private FileSystemWatcher _fileWatcher;
    private List<string> _imagePaths = [];
    private Dictionary<string, BitmapImage> _imageCache = [];
    public ImageSource CurrentImage { get; private set; }
    public string ChangeMode { get; private set; }
    public int ChangeInterval { get; private set; }

    /// <summary>
    /// 服务构造函数
    /// </summary>
    public ImageSwitcherService()
    {
        InitializeService();
        StartFileWatcher();
    }

    /// <summary>
    /// 初始化服务核心逻辑
    /// </summary>
    private void InitializeService()
    {
        ReloadConfig();
        LoadImagePaths();

        if (_imagePaths.Count > 1)
        {
            _timer = new DispatcherTimer();
            UpdateTimerInterval();
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        if (_imagePaths.Any()) CurrentImage = LoadImage(_imagePaths.First());
    }

    /// <summary>
    /// 重新加载配置
    /// </summary>
    public void ReloadConfig()
    {
        ChangeMode = INIRead("背景设置", "ChangeMode", INIPath);      
        ChangeInterval = Math.Max(1, StringToInt(INIRead("背景设置", "ChangeInterval", INIPath)));
        LoadImagePaths();
        UpdateTimerInterval();
    }

    /// <summary>
    /// 更新定时器间隔
    /// </summary>
    private void UpdateTimerInterval()
    {
        if (_timer != null) _timer.Interval = TimeSpan.FromSeconds(ChangeInterval);
    }

    /// <summary>
    /// 加载图片路径（按数字排序）
    /// </summary>
    private void LoadImagePaths()
    {
        try
        {
            var allowedExtensions = new[] { ".jpg", ".png", ".jpeg" };
            _imagePaths = [.. Directory.GetFiles(BackgroundDirectory)
                .Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLower()))
                .OrderBy(f =>
                {
                    // 按文件名的数字排序
                    var fileName = Path.GetFileNameWithoutExtension(f);
                    return int.TryParse(fileName, out int num) ? num : int.MaxValue;
                })];

            // 重置索引到安全位置
            _currentIndex = Math.Min(_currentIndex, _imagePaths.Count - 1);
        }
        catch { /*...*/ }
    }

    /// <summary>
    /// 文件变化监控
    /// </summary>
    private void StartFileWatcher()
    {
        _fileWatcher = new FileSystemWatcher
        {
            Path = BackgroundDirectory,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            Filter = "*.*"
        };

        _fileWatcher.Created += OnFilesChanged;
        _fileWatcher.Deleted += OnFilesChanged;
        _fileWatcher.Renamed += OnFilesChanged;
        _fileWatcher.Changed += OnFilesChanged;
        _fileWatcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// 文件变化处理
    /// </summary>
    private void OnFilesChanged(object sender, FileSystemEventArgs e)
    {
        _timer?.Stop();

        // 立即清除被修改文件的缓存
        if (e.ChangeType != WatcherChangeTypes.Deleted) ReloadByPath(e.FullPath);

        Task.Delay(100).ContinueWith(_ => // 缩短延迟确保文件完全写入
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LoadImagePaths();

                // 如果路径仍然存在，强制重新加载当前图片
                if (_currentIndex >= 0 && _currentIndex < _imagePaths.Count)
                {
                    var currentPath = _imagePaths[_currentIndex];
                    _imageCache.Remove(currentPath);
                    CurrentImage = LoadImage(currentPath);
                    OnPropertyChanged(nameof(CurrentImage));
                }

                if (_imagePaths.Count > 1) _timer?.Start();
            });
        });
    }

    /// <summary>
    /// 计时器触发事件
    /// </summary>
    private void Timer_Tick(object sender, EventArgs e)
    {
        if (!_imagePaths.Any()) return;

        var nextIndex = CalculateNextIndex();
        var nextImage = LoadImage(_imagePaths[nextIndex]);

        Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentImage = nextImage;
            OnPropertyChanged(nameof(CurrentImage));
            _currentIndex = nextIndex;
        });
    }

    /// <summary>
    /// 计算下一个索引
    /// </summary>
    private int CalculateNextIndex()
    {
        return ChangeMode == "Sequential"
            ? (_currentIndex + 1) % _imagePaths.Count
            : _random.Next(_imagePaths.Count);
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Cleanup()
    {
        _timer?.Stop();
        _fileWatcher?.Dispose();

        // 强制释放所有图片资源
        CurrentImage = null;
        _imagePaths.ForEach(p =>
        {
            var img = (BitmapImage)LoadImage(p);
            img.StreamSource?.Dispose();
            img.UriSource = null;
        });
        _imagePaths.Clear();
    }

    /// <summary>
    /// 重载指定路径的图片
    /// </summary>
    public void ReloadByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        // 强制移除缓存
        var key = _imageCache.Keys.FirstOrDefault(k =>
            string.Equals(k, path, StringComparison.OrdinalIgnoreCase));
        if (key != null) _imageCache.Remove(key);

        // 如果当前显示的是该路径
        if (_imagePaths.Count > 0 &&
            _currentIndex >= 0 &&
            _currentIndex < _imagePaths.Count &&
            string.Equals(_imagePaths[_currentIndex], path, StringComparison.OrdinalIgnoreCase))
        {
            // 创建新实例
            var newImage = LoadImage(path);
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentImage = newImage;
                OnPropertyChanged(nameof(CurrentImage));
            });
        }
    }

    /// <summary>
    /// 加载图片
    /// </summary>
    private BitmapImage LoadImage(string path)
    {
        if (_imageCache.TryGetValue(path, out var cached)) return cached;

        var image = new BitmapImage();

        try
        {
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

            // 动态设置解码尺寸
            var (w, h) = GetImageSize(path);
            if (w > MaxDecodeSize || h > MaxDecodeSize)
            {
                var ratio = Math.Min((double)MaxDecodeSize / w, (double)MaxDecodeSize / h);
                image.DecodePixelWidth = (int)(w * ratio);
            }

            image.EndInit();
            image.Freeze();
            _imageCache[path] = image;
        }
        catch { /*...*/ }

        return image;
    }

    /// <summary>
    /// 获取图片尺寸
    /// </summary>
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

    /// <summary>
    /// 属性变更通知
    /// </summary>
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}