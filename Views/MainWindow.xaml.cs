using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SNIBypassGUI.Models;
using SNIBypassGUI.Utils;
using static SNIBypassGUI.Consts.AppConsts;
using static SNIBypassGUI.Consts.ConfigConsts;
using static SNIBypassGUI.Consts.CollectionConsts;
using static SNIBypassGUI.Consts.LinksConsts;
using static SNIBypassGUI.Consts.PathConsts;
using static SNIBypassGUI.Utils.AcrylicServiceUtils;
using static SNIBypassGUI.Utils.CertificateUtils;
using static SNIBypassGUI.Utils.ConvertUtils;
using static SNIBypassGUI.Utils.ConvertUtils.FileSizeConverter;
using static SNIBypassGUI.Utils.FileUtils;
using static SNIBypassGUI.Utils.GitHubUtils;
using static SNIBypassGUI.Utils.IniFileUtils;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.NetworkAdapterUtils;
using static SNIBypassGUI.Utils.NetworkUtils;
using static SNIBypassGUI.Utils.ProcessUtils;
using static SNIBypassGUI.Utils.ServiceUtils;
using static SNIBypassGUI.Utils.WinApiUtils;
using Action = System.Action;
using MessageBox = HandyControl.Controls.MessageBox;
using Task = System.Threading.Tasks.Task;

namespace SNIBypassGUI.Views
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer controlsStatusUpdateTimer = new() { Interval = TimeSpan.FromSeconds(5) };
        private readonly DispatcherTimer serviceStatusUpdateTimer = new() { Interval = TimeSpan.FromSeconds(3) };
        private readonly DispatcherTimer tempFilesSizeUpdateTimer = new() { Interval = TimeSpan.FromSeconds(5) };
        public ICommand TaskbarIconLeftClickCommand { get; }
        public static ImageSwitcherService BackgroundService { get; private set; }

        /// <summary>
        /// 窗口构造函数
        /// </summary>
        public MainWindow()
        {
            if (StringToBool(INIRead(AdvancedSettings, GUIDebug, INIPath))) EnableLog();
            WriteLog("进入 MainWindow。", LogLevel.Debug);

            InitializeComponent();

            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 2 && args[1] == "/waitForParent")
            {
                if (int.TryParse(args[2], out int parentPid))
                {
                    try
                    {
                        using var parentProcess = Process.GetProcessById(parentPid);
                        bool exited = parentProcess.WaitForExit(5000);
                        if (exited) WriteLog("旧进程已安全退出。", LogLevel.Debug);
                        else WriteLog("等待旧进程超时，强制继续启动。", LogLevel.Warning);
                    }
                    catch (ArgumentException ex)
                    {
                        WriteLog($"旧进程不存在。", LogLevel.Error, ex);
                    }
                }
            }

            if (GetProcessCount(Process.GetCurrentProcess().MainModule.ModuleName) > 1)
            {
                WriteLog("检测到程序已经在运行，将退出程序。", LogLevel.Warning);
                MessageBox.Show("SNIBypassGUI 已经在运行！\r\n请检查是否有托盘图标！(((ﾟДﾟ;)))", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                Environment.Exit(1);
                return;
            }

            // 初始化目录与文件
            InitializeDirectoriesAndFiles();

            // 将 MainWindow 作为 DataContext 设置
            DataContext = this;

            BackgroundService = new ImageSwitcherService();
            BackgroundService.PropertyChanged += OnBackgroundChanged;

            // 手动设置初始图像
            CurrentImage.Source = BackgroundService.CurrentImage;

            TaskbarIconLeftClickCommand = new RelayCommand(() => TaskbarIcon_LeftClick());

            // 窗口可拖动
            TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };

            // 刷新托盘图标，避免有多个托盘图标出现
            RefreshNotification();

            WriteLog("完成 MainWindow。", LogLevel.Debug);
        }

        /// <summary>
        /// 将代理开关项逐个添加到列表
        /// </summary>
        private async Task AddSwitchesToList()
        {
            WriteLog("进入 AddSwitchesToList。", LogLevel.Debug);

            string json = File.ReadAllText(SwitchData);

            // 反序列化 JSON
            var switchList = JsonConvert.DeserializeObject<SwitchList>(json);
            if (switchList?.switchs == null || switchList.switchs.Length == 0)
            {
                WriteLog("未找到任何代理开关数据！", LogLevel.Warning);
                return;
            }

            // 复制数据，避免 UI 线程和 Task.Run() 访问相同对象
            var items = switchList.switchs.ToList();

            // 批量创建 SwitchItem 并添加到 ObservableCollection
            foreach (var item in switchList.switchs)
            {
                Switchs.Add(new SwitchItem
                {
                    FaviconImageSource = Path.Combine(FaviconsDirectory, item.switchtitle + ".png"),
                    SwitchTitle = item.switchtitle,
                    LinksText = item.linkstext,
                    ToggleButtonName = item.togglebuttonname,
                    SectionName = item.sectionname,
                    SystemHostsRecord = GetSection(SystemHostsAll, item.sectionname),
                    AcrylicHostsRecord = GetSection(AcrylicHostsAll, item.sectionname),
                });
            }

            await Task.Run(() =>
            {
                foreach (var item in items)
                {
                    // 先保存 Base64 转换的图片
                    SaveBase64AsImage(item.favicon, item.switchtitle, FaviconsDirectory);

                    // 立刻更新 UI
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 找到对应的 SwitchItem
                        var switchItem = Switchs.FirstOrDefault(s => s.SwitchTitle == item.switchtitle);
                        if (switchItem != null) AddSwitchToUI(switchItem);
                    });
                }
            });

            WriteLog("完成 AddSwitchesToList。", LogLevel.Debug);
        }

        /// <summary>
        /// 将指定开关添加到用户界面
        /// </summary>
        private void AddSwitchToUI(SwitchItem item)
        {
            WriteLog("进入 AddSwitchToUI。", LogLevel.Debug);

            // 索引，用于确定每个代理开关项的位置
            int itemIndex = Switchlist.RowDefinitions.Count;

            // 创建站点图标
            Image favicon = new()
            {
                Source = GetImage(item.FaviconImageSource),
                Height = 32,
                Width = 32,
                Margin = new Thickness(10, 10, 10, 5)
            };

            // 设置站点图标的位置
            Grid.SetColumn(favicon, 0);
            Grid.SetRow(favicon, itemIndex);

            // 将站点图标添加到列表中
            Switchlist.Children.Add(favicon);

            // 创建站点名称及链接
            TextBlock textBlock = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 3, 10, 3)
            };

            // 添加站点名称
            textBlock.Inlines.Add(new Run { Text = item.SwitchTitle, FontSize = 16 });

            // 添加换行，使链接换行显示
            textBlock.Inlines.Add(new LineBreak());

            // 添加站点链接
            foreach (var linksTextParts in item.LinksText)
            {
                if (linksTextParts == "、" || linksTextParts == "等") textBlock.Inlines.Add(new Run { Text = linksTextParts, FontSize = 15, FontWeight = FontWeights.Bold });
                else
                {
                    Run run = new() { Text = linksTextParts, Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xF9, 0xFF)), FontSize = 15, Cursor = Cursors.Hand };
                    run.PreviewMouseDown += LinkText_PreviewMouseDown;
                    textBlock.Inlines.Add(run);
                }
            }

            // 设置站点名称及链接的位置
            Grid.SetColumn(textBlock, 1);
            Grid.SetRow(textBlock, itemIndex);

            // 将站点名称及链接添加到列表中
            Switchlist.Children.Add(textBlock);

            // 创建代理开关按钮
            ToggleButton toggleButton = new()
            {
                Width = 40,
                Margin = new Thickness(5, 0, 5, 0),
                IsChecked = true,
                Style = (Style)FindResource("ToggleButtonSwitch")
            };

            // 设置代理开关按钮的点击事件
            toggleButton.Click += ToggleButtonsClick;

            // 设置代理开关按钮的位置
            Grid.SetColumn(toggleButton, 2);
            Grid.SetRow(toggleButton, itemIndex);

            // 将代理开关按钮添加到列表中
            Switchlist.Children.Add(toggleButton);

            /*
                * https://learn.microsoft.com/zh-cn/dotnet/api/system.windows.frameworkelement.registername
                * 
                * FrameworkElement.RegisterName(String, Object) 方法
                * 
                * 定义: 
                * 命名空间: System.Windows
                * 程序集: PresentationFramework.dll
                * 
                * 提供一个可简化对 NameScope(https://learn.microsoft.com/zh-cn/dotnet/api/system.windows.namescope) 注册方法访问的访问器。
                * public void RegisterName (string name, object scopedElement);
                * 
                * 参数: 
                * name: 要在指定的名称-对象映射中使用的名称。
                * scopedElement: 映射的对象。
                * 
                * 注解: 
                * 此方法是调用的 RegisterName(https://learn.microsoft.com/zh-cn/dotnet/api/system.windows.namescope.registername)便利方法。 
                * 实现将检查连续的父元素，直到找到适用的 NameScope 实现，通过查找实现的 INameScope(https://learn.microsoft.com/zh-cn/dotnet/api/system.windows.markup.inamescope)元素来找到该实现。 
                * 有关名称范围的详细信息，请参阅 WPF XAML 名称范围。调用 RegisterName 是必需的，以便在代码中创建时为应用程序正确挂钩动画情节提要。 
                * 这是因为其中一个关键情节提要属性 TargetName使用运行时名称查找，而不是能够引用目标元素。 即使该元素可通过代码引用访问，也是如此。
                * 有关为何需要注册情节提要目标名称的详细信息，请参阅 情节提要概述。
                * 
            */
            // 注册代理开关按钮的名称
            Switchlist.RegisterName(item.ToggleButtonName, toggleButton);

            // 为列表添加行
            Switchlist.RowDefinitions.Add(new RowDefinition());

            // 拓展首列与尾列的背景
            Grid.SetRowSpan(FirstColumnBorder, itemIndex + 1);
            Grid.SetRowSpan(LastColumnBorder, itemIndex + 1);

            WriteLog("完成 AddSwitchToUI。", LogLevel.Debug);
        }

        /// <summary>
        /// 更新临时文件大小
        /// </summary>
        private void UpdateTempFilesSize() => CleanBtn.Content = $"清理临时文件 ({ConvertBetweenUnits(GetTotalSize(TempFilesPathsIncludingGUILogs), SizeUnit.B, SizeUnit.MB).ToString("0.00")}MB)";

        /// <summary>
        /// 更新适配器列表
        /// </summary>
        private void UpdateAdaptersCombo()
        {
            WriteLog("进入 UpdateAdaptersCombo。", LogLevel.Debug);

            // 从配置文件中读取上次选中的适配器
            string PreviousSelectedAdapter = INIRead(ProgramSettings, SpecifiedAdapter, INIPath);

            // 获取友好名称不为空的适配器
            List<NetworkAdapter> adapters = GetNetworkAdapters(ScopeNeeded.FriendlyNameNotNullOnly);

            // 清空下拉框避免重复添加
            AdaptersCombo.Items.Clear();

            // 将名字不为空的适配器逐个添加到下拉框
            foreach (var adapter in adapters)
            {
                if (!string.IsNullOrEmpty(adapter.FriendlyName))
                {
                    WriteLog($"向适配器列表添加 {adapter.FriendlyName}。", LogLevel.Info);
                    AdaptersCombo.Items.Add(adapter.FriendlyName);
                }
            }

            // 如果更新后的列表包含之前选中的适配器，那么重新选中
            // Cast<string>() 假定 AdaptersCombo.Items 中的所有项都是 string 类型。如果不是，可能会遇到运行时错误。
            // 使用 OfType<string>() 来安全地处理不同类型的项。OfType<string>() 会自动跳过非字符串类型的项。
            if (AdaptersCombo.Items.OfType<string>().Any(item => item == PreviousSelectedAdapter))
            {
                // SelectedItem 会确保 AdaptersCombo 正确选中与 PreviousSelectedAdapter 匹配的项。
                // 使用 Text 设置文本会导致 AdaptersCombo 显示 PreviousSelectedAdapter，但它并不意味着该项被选中了（特别是当该项不在 Items 中时）。
                AdaptersCombo.SelectedItem = PreviousSelectedAdapter;
            }
            else
            {
                WriteLog($"适配器列表中丢失 {PreviousSelectedAdapter}，取消选中。", LogLevel.Warning);

                // 如果没有匹配的项，取消选中
                AdaptersCombo.SelectedItem = null;
            }
            WriteLog("完成 UpdateAdaptersCombo。", LogLevel.Debug);
        }

        /// <summary>
        /// 更新服务状态
        /// </summary>
        public void UpdateServiceStatus()
        {
            WriteLog("进入 UpdateServiceStatus。", LogLevel.Debug);

            // 检查主服务是否在运行
            bool IsNginxRunning = IsProcessRunning(NginxProcessName);

            // 检查DNS服务是否在运行
            bool IsDnsRunning = IsAcrylicServiceRunning();

            WriteLog($"主服务运行中： {BoolToYesNo(IsNginxRunning)}", LogLevel.Info);
            WriteLog($"DNS服务运行中： {BoolToYesNo(IsDnsRunning)}", LogLevel.Info);

            // 根据不同情况显示不同的服务状态文本
            if (IsNginxRunning && IsDnsRunning)
            {
                // 主服务和DNS服务都在运行的情况
                ServiceStatusText.Text = TaskbarIconServiceST.Text = "主服务和DNS服务运行中";
                ServiceStatusText.Foreground = TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.ForestGreen);
                AdaptersCombo.IsEnabled = GetActiveAdapterBtn.IsEnabled = false;
            }
            else if (IsNginxRunning)
            {
                // 仅主服务在运行的情况
                ServiceStatusText.Text = TaskbarIconServiceST.Text = "仅主服务运行中";
                ServiceStatusText.Foreground = TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);
                AdaptersCombo.IsEnabled = GetActiveAdapterBtn.IsEnabled = false;
            }
            else if (IsDnsRunning)
            {
                // 仅DNS服务在运行的情况
                ServiceStatusText.Text = TaskbarIconServiceST.Text = "仅DNS服务运行中";
                ServiceStatusText.Foreground = TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);
                AdaptersCombo.IsEnabled = GetActiveAdapterBtn.IsEnabled = false;
            }
            else
            {
                // 服务都不在运行的情况
                ServiceStatusText.Text = TaskbarIconServiceST.Text = "主服务与DNS服务未运行";
                ServiceStatusText.Foreground = TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.Red);
                AdaptersCombo.IsEnabled = GetActiveAdapterBtn.IsEnabled = true;
            }
            WriteLog("完成 UpdateServiceStatus。", LogLevel.Debug);
        }

        /// <summary>
        /// 检查必要目录、文件的存在性，并在缺失时创建或释放
        /// </summary>
        public void InitializeDirectoriesAndFiles()
        {
            WriteLog("进入 InitializeDirectoriesAndFiles。", LogLevel.Debug);

            // 删除旧版本主程序
            TryDelete(OldVersionExe);
            TryDelete(NewVersionExe);

            // 确保必要目录存在
            foreach (string directory in NeccesaryDirectories) EnsureDirectoryExists(directory);

            // 释放相关文件
            foreach (var pair in PathToResourceDic)
            {
                if (!File.Exists(pair.Key))
                {
                    WriteLog($"文件 {pair.Key} 不存在，释放。", LogLevel.Info);
                    ExtractResourceToFile(pair.Value, pair.Key);
                }
            }

            // 如果背景文件夹为空，则释放默认背景
            if (!Directory.EnumerateFiles(BackgroundDirectory).GetEnumerator().MoveNext())
            {
                foreach (var pair in DefaultBackgrounds) ExtractResourceToFile(pair.Value, Path.Combine(BackgroundDirectory, pair.Key));
            }

            // 如果配置文件不存在，则直接创建
            if (!File.Exists(INIPath)) File.WriteAllText(INIPath, "");

            Dictionary<string, List<string>> sectionKeys = [];

            foreach (var config in InitialConfigurations)
            {
                var sections = config.Key.Split(':');
                if (sections.Length != 2) continue;
                string section = sections[0];
                if (!sectionKeys.ContainsKey(section)) sectionKeys[section] = GetKeys(section, INIPath);
            }

            // 遍历配置项，检查是否需要写入
            foreach (var config in InitialConfigurations)
            {
                var sections = config.Key.Split(':');
                if (sections.Length != 2) continue;
                string section = sections[0];
                string key = sections[1];
                if (!sectionKeys[section].Contains(key))
                {
                    WriteLog($"配置键 {key} 不存在，写入默认值。", LogLevel.Info);
                    INIWrite(section, key, config.Value, INIPath);
                }
            }

            // 背景顺序配置
            if (!sectionKeys[BackgroundSettings].Contains(ImageOrder) || string.IsNullOrEmpty(INIRead(BackgroundSettings, ImageOrder, INIPath)))
            {
                var files = ImageExtensions.SelectMany(ext => Directory.GetFiles(BackgroundDirectory, $"*{ext}"));
                var hashes = files.Select(CalculateFileHash);
                string imageOrder = string.Join(",", hashes);
                INIWrite(BackgroundSettings, ImageOrder, imageOrder, INIPath);
            }

            WriteLog("完成 InitializeDirectoriesAndFiles。", LogLevel.Debug);
        }

        /// <summary>
        /// 更新一言
        /// </summary>
        public async Task UpdateYiyan()
        {
            WriteLog("进入 UpdateYiyan。", LogLevel.Debug);
            try
            {
                string YiyanJson = await GetAsync("https://v1.hitokoto.cn/?c=d");
                WriteLog($"获取到一言的数据为 {YiyanJson}。", LogLevel.Debug);

                // 提取一言文本、来源、作者
                JObject repodata = JObject.Parse(YiyanJson);
                string Hitokoto = repodata["hitokoto"].ToString();
                string From = repodata["from"].ToString();
                string FromWho = repodata["from_who"].ToString();

                WriteLog($"解析到一言文本为 {Hitokoto}，来源为 {From}，作者为 {FromWho}。", LogLevel.Info);

                // 将一言与相关信息显示在托盘图标悬浮文本
                TaskbarIconYiyan.Text = Hitokoto;
                TaskbarIconYiyanFrom.Text = $"—— {FromWho}「{From}」";
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常，将设置为默认一言。", LogLevel.Error, ex);

                // 设置为默认一言
                TaskbarIconYiyan.Text = DefaultYiyan;
                TaskbarIconYiyanFrom.Text = DefaultYiyanFrom;
            }
            WriteLog("完成 UpdateYiyan。", LogLevel.Debug);
        }

        /// <summary>
        /// 从配置文件向 Hosts 文件更新
        /// </summary>
        public void UpdateHostsFromConfig()
        {
            WriteLog("进入 UpdateHostsFromConfig。", LogLevel.Debug);

            // 根据域名解析模式判断要更新的文件
            bool IsDnsService = INIRead(AdvancedSettings, DomainNameResolutionMethod, INIPath) == DnsServiceMode;
            string FileShouldUpdate = IsDnsService ? AcrylicHostsPath : SystemHosts;

            // 根据域名解析模式获取应该添加的条目数据
            string CorrespondingHosts = IsDnsService ? "AcrylicHostsRecord" : "SystemHostsRecord";

            WriteLog($"当前域名解析方法是否为DNS服务： {BoolToYesNo(IsDnsService)}，将更新的文件为 {FileShouldUpdate}。", LogLevel.Info);
            try
            {
                // 移除所有条目部分，防止重复添加
                RemoveHostsRecords();

                // 遍历条目部分名称
                foreach (SwitchItem pair in Switchs)
                {
                    if (StringToBool(INIRead(ProxySettings, pair.SectionName, INIPath)) == true)
                    {
                        // 条目部分名称对应的开关是打开的情况
                        WriteLog($"{pair.SectionName} 的代理开关为开启，将添加记录。", LogLevel.Info);

                        // 添加该条目部分
                        AppendToFile(FileShouldUpdate, (string[])pair.GetType().GetProperty(CorrespondingHosts).GetValue(pair));
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteLog($"对系统hosts的访问被拒绝！", LogLevel.Error, ex);
                if (MessageBox.Show($"对系统hosts的访问被拒绝。\r\n{ex}\r\n点击“是”将为您展示有关帮助。", "错误", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes) Process.Start(new ProcessStartInfo(当您遇到对系统hosts的访问被拒绝的提示时) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"更新 Hosts 文件时遇到异常。\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            WriteLog("完成UpdateHostsFromConfig。", LogLevel.Debug);
        }

        /// <summary>
        /// 移除 Hosts 中全部有关记录
        /// </summary>
        public void RemoveHostsRecords()
        {
            WriteLog("进入RemoveHosts。", LogLevel.Debug);

            // 根据域名解析模式判断要更新的文件
            bool IsDnsService = INIRead(AdvancedSettings, DomainNameResolutionMethod, INIPath) == DnsServiceMode;
            string FileShouldUpdate = IsDnsService ? AcrylicHostsPath : SystemHosts;

            WriteLog($"当前域名解析方法是否为 DNS 服务： {BoolToYesNo(IsDnsService)}，将更新的文件为 {FileShouldUpdate}。", LogLevel.Info);
            try
            {
                foreach (SwitchItem pair in Switchs)
                {
                    WriteLog($"移除 {pair.SectionName} 的记录部分。", LogLevel.Info);
                    RemoveSection(FileShouldUpdate, pair.SectionName);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteLog($"对系统 Hosts 的访问被拒绝！", LogLevel.Error, ex);
                if (MessageBox.Show($"对系统 Hosts 的访问被拒绝。\r\n{ex}\r\n点击“是”将为您展示有关帮助。", "错误", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes) StartProcess(当您遇到对系统hosts的访问被拒绝的提示时, useShellExecute: true);
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"更新 Hosts 文件时遇到异常。\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            WriteLog("完成 RemoveHosts。", LogLevel.Debug);
        }

        /// <summary>
        /// 从配置文件同步有关控件
        /// </summary>
        private void SyncControlsFromConfig()
        {
            WriteLog("进入 SyncControlsFromConfig。", LogLevel.Debug);

            string themeMode = INIRead(ProgramSettings, ThemeMode, INIPath);
            if (themeMode == LightMode)
            {
                SwitchTheme(false);
                ThemeSwitchTB.IsChecked = false;
            }
            else
            {
                SwitchTheme(true);
                ThemeSwitchTB.IsChecked = true;
            }

            // 遍历所有代理开关项
            foreach (SwitchItem pair in Switchs)
            {
                ToggleButton toggleButtonInstance = (ToggleButton)FindName(pair.ToggleButtonName);
                bool isEnabled = StringToBool(INIRead(ProxySettings, pair.SectionName, INIPath));
                if (toggleButtonInstance != null)
                {
                    // 更新代理开关状态
                    toggleButtonInstance.IsChecked = isEnabled;

                    WriteLog($"开关 {toggleButtonInstance.Name} 从配置键 {pair.SectionName} 同步状态： {BoolToYesNo(isEnabled)}。", LogLevel.Debug);
                }
            }

            // 判断调试模式是否开启
            bool isDebugModeOn = StringToBool(INIRead(AdvancedSettings, DebugMode, INIPath));

            // 更新调试有关按钮文本
            DebugModeBtn.Content = isDebugModeOn ? "调试模式：\n开" : "调试模式：\n关";
            GUIDebugBtn.Content = StringToBool(INIRead(AdvancedSettings, GUIDebug, INIPath)) ? "GUI调试：\n开" : "GUI调试：\n关";
            SwitchDomainNameResolutionMethodBtn.Content = INIRead(AdvancedSettings, DomainNameResolutionMethod, INIPath) == DnsServiceMode ? "域名解析：\nDNS服务" : "域名解析：\n系统hosts";
            AcrylicDebugBtn.Content = StringToBool(INIRead(AdvancedSettings, AcrylicDebug, INIPath)) ? "DNS服务调试：\n开" : "DNS服务调试：\n关";
            PixivIPPreferenceBtn.Content = StringToBool(INIRead(ProgramSettings, PixivIPPreference, INIPath)) ? "Pixiv IP优选：开" : "Pixiv IP优选：关";

            // 根据调试模式是否开启决定有关按钮是否启用
            SwitchDomainNameResolutionMethodBtn.IsEnabled = AcrylicDebugBtn.IsEnabled = GUIDebugBtn.IsEnabled = isDebugModeOn;

            // 更新适配器列表及选中的适配器
            UpdateAdaptersCombo();

            WriteLog("完成 SyncControlsFromConfig。", LogLevel.Debug);
        }

        /// <summary>
        /// 从代理开关列表向配置文件同步
        /// </summary>
        public void UpdateConfigFromToggleButtons()
        {
            WriteLog("进入 UpdateConfigFromToggleButtons。", LogLevel.Debug);

            // 遍历所有代理开关
            foreach (var pair in Switchs)
            {
                ToggleButton toggleButtonInstance = (ToggleButton)FindName(pair.ToggleButtonName);
                if (toggleButtonInstance != null)
                {
                    if (toggleButtonInstance.IsChecked == true) INIWrite(ProxySettings, pair.SectionName, "true", INIPath);
                    else INIWrite(ProxySettings, pair.SectionName, "false", INIPath);
                    WriteLog($"配置键 {pair.SectionName} 从开关 {toggleButtonInstance.Name} 同步状态： {BoolToYesNo(toggleButtonInstance.IsChecked)}。", LogLevel.Debug);
                }
            }

            WriteLog("完成 UpdateConfigFromToggleButtons。", LogLevel.Debug);
        }

        /// <summary>
        /// 设置指定网络适配器首选DNS为环回地址并记录先前的DNS服务器地址
        /// </summary>
        /// <param name="Adapter">指定的网络适配器</param>
        public async Task SetLoopbackDNS(NetworkAdapter Adapter)
        {
            WriteLog("进入 SetLoopbackDNS。", LogLevel.Debug);
            try
            {
                if (Adapter.IPv4DNSServer.Length == 0 || Adapter.IPv4DNSServer[0] != "127.0.0.1")
                {
                    // 指定适配器的首选DNS不是127.0.0.1的情况
                    WriteLog($"开始配置网络适配器： {Adapter.FriendlyName}。", LogLevel.Info);

                    // 在设置DNS之前记录DNS服务器是否为自动获取
                    bool? isIPv4DNSAuto = Adapter.IsIPv4DNSAuto;

                    // 用于暂存DNS服务器地址
                    string PreviousDNS1 = string.Empty, PreviousDNS2 = string.Empty;

                    if (isIPv4DNSAuto != true)
                    {
                        // 遍历 DNS 地址并获取有效的 DNS
                        int validDnsCount = 0;
                        foreach (var dns in Adapter.IPv4DNSServer)
                        {
                            if (IsValidIPv4(dns) && dns != "127.0.0.1")
                            {
                                if (validDnsCount == 0) PreviousDNS1 = dns;
                                else if (validDnsCount == 1) PreviousDNS2 = dns;
                                validDnsCount++;
                                if (validDnsCount == 2) break;
                            }
                        }
                    }

                    // 将指定适配器的IPv4 DNS服务器设置为首选127.0.0.1
                    SetIPv4DNS(Adapter, ["127.0.0.1"]);

                    // 将指定适配器的IPv6 DNS服务器设置为首选::1
                    await SetIPv6DNS(Adapter, ["::1"]);

                    // 刷新适配器信息
                    var refreshedAdapter = Refresh(Adapter);
                    if (refreshedAdapter == null)
                    {
                        WriteLog("刷新适配器信息失败！", LogLevel.Error);
                        return;
                    }
                    Adapter = refreshedAdapter;

                    string ipv4Dns = Adapter.IPv4DNSServer.Length > 0 ? Adapter.IPv4DNSServer[0] : "未设置";
                    string ipv6Dns = Adapter.IPv6DNSServer.Length > 0 ? Adapter.IPv6DNSServer[0] : "未设置";
                    WriteLog($"指定网络适配器是否为自动获取 DNS： {BoolToYesNo(isIPv4DNSAuto)}", LogLevel.Info);
                    WriteLog($"成功设置指定网络适配器的 IPv4 首选 DNS 为 {ipv4Dns}，IPv6 首选 DNS 为 {ipv6Dns}", LogLevel.Info);
                    WriteLog($"将暂存的DNS服务器为： {PreviousDNS1}， {PreviousDNS2}", LogLevel.Debug);

                    // 将停止服务时恢复适配器所需要的信息写入配置文件备用
                    INIWrite(TemporaryData, PreviousPrimaryDNS, PreviousDNS1, INIPath);
                    INIWrite(TemporaryData, PreviousAlternativeDNS, PreviousDNS2, INIPath);
                    INIWrite(TemporaryData, IsPreviousDnsAutomatic, isIPv4DNSAuto.ToString(), INIPath);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"无法设置指定的网络适配器！", LogLevel.Error, ex);
                if (MessageBox.Show($"无法设置指定的网络适配器！请手动设置！\r\n点击“是”将为您展示有关帮助。", "错误", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) StartProcess(当您找不到当前正在使用的适配器或启动时遇到适配器设置失败时, useShellExecute: true);
            }
            WriteLog("完成 SetLoopbackDNS。", LogLevel.Debug);
        }

        /// <summary>
        /// 从配置文件还原适配器
        /// </summary>
        /// <param name="Adapter">指定的网络适配器</param>
        public async Task RestoreAdapterDNS(NetworkAdapter Adapter)
        {
            WriteLog("进入 RestoreAdapterDNS。", LogLevel.Debug);
            try
            {
                if (Adapter.IPv4DNSServer.Length > 0 && Adapter.IPv4DNSServer[0] == "127.0.0.1")
                {
                    // 指定适配器的首选DNS为环回地址的情况，需要从配置文件还原回去
                    if (StringToBool(INIRead(TemporaryData, IsPreviousDnsAutomatic, INIPath)))
                    {
                        // 指定适配器DNS服务器之前是自动获取的情况，设置指定适配器DNS服务器为自动获取
                        SetIPv4DNS(Adapter, []);

                        WriteLog($"活动网络适配器的 IPv4 DNS 成功设置为自动获取。", LogLevel.Info);
                    }
                    else
                    {
                        string PreviousDNS1 = INIRead(TemporaryData, PreviousPrimaryDNS, INIPath);
                        string PreviousDNS2 = INIRead(TemporaryData, PreviousAlternativeDNS, INIPath);
                        if (string.IsNullOrEmpty(PreviousDNS1) || !IsValidIPv4(PreviousDNS1))
                        {
                            SetIPv4DNS(Adapter, []);
                            WriteLog($"指定网络适配器的 IPv4 DNS 成功设置为自动获取。", LogLevel.Info);
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(PreviousDNS2) || !IsValidIPv4(PreviousDNS2)) SetIPv4DNS(Adapter, [PreviousDNS1]);
                            else SetIPv4DNS(Adapter, [PreviousDNS1, PreviousDNS2]);
                            Adapter = Refresh(Adapter);
                            string IPv4DNS1 = Adapter.IPv4DNSServer.Length > 0 ? Adapter.IPv4DNSServer[0] : "空";
                            string IPv4DNS2 = Adapter.IPv4DNSServer.Length > 1 ? Adapter.IPv4DNSServer[1] : "空";
                            WriteLog($"指定网络适配器的 DNS 成功设置为首选 {IPv4DNS1}，备用 {IPv4DNS2}。", LogLevel.Info);
                        }
                    }
                }
                if (Adapter.IPv6DNSServer.Length > 0 && Adapter.IPv6DNSServer[0] == "::1")
                {
                    await SetIPv6DNS(Adapter, []);
                    WriteLog($"活动网络适配器的 IPv6 DNS 成功设置为自动获取。", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"无法还原指定的网络适配器！", LogLevel.Error, ex);
                if (MessageBox.Show($"无法还原指定的网络适配器！请手动还原！\r\n点击“是”将为您展示有关帮助。", "错误", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) StartProcess(当您在停止时遇到适配器设置失败或不确定该软件是否对适配器造成影响时, useShellExecute: true);
            }
            WriteLog("完成 RestoreAdapterDNS。", LogLevel.Debug);
        }

        /// <summary>
        /// 启动主服务
        /// </summary>
        public void StartNginx()
        {
            WriteLog("进入 StartNginx。", LogLevel.Debug);
            try
            {
                ServiceStatusText.Text = "主服务启动中";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);
                if (IsPortInUse(80) || IsPortInUse(443))
                {
                    WriteLog($"检测到系统 80 或 443 端口被占用。", LogLevel.Warning);
                    if (MessageBox.Show($"检测到系统 80 或 443 端口被占用，主服务可能无法正常运行，但仍然会尝试继续启动。\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) StartProcess(当您的主服务运行后自动停止或遇到80端口被占用的提示时, useShellExecute: true);
                }
                StartProcess(nginxPath, workingDirectory: NginxDirectory);
            }
            catch (Exception ex)
            {
                WriteLog($"尝试启动主服务时遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"启动主服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            WriteLog("完成 StartNginx。", LogLevel.Debug);
        }

        /// <summary>
        /// 按需启动服务
        /// </summary>
        public async Task StartService()
        {
            WriteLog("进入 StartService。", LogLevel.Debug);

            if (INIRead(AdvancedSettings, DomainNameResolutionMethod, INIPath) == DnsServiceMode)
            {
                // 域名解析模式是 DNS 服务的情况，需要启动主服务、 DNS 服务与设置适配器

                if (!IsProcessRunning(NginxProcessName))
                {
                    WriteLog($"主服务未运行，将启动主服务。", LogLevel.Info);
                    StartNginx();
                }

                if (!IsAcrylicServiceRunning())
                {
                    WriteLog($"DNS 服务未运行，将启动 DNS 服务。", LogLevel.Info);
                    ServiceStatusText.Text = "DNS服务启动中";
                    ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);
                    try
                    {
                        StartAcrylicService();
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"尝试启动 DNS 服务时遇到异常。", LogLevel.Error, ex);
                        MessageBox.Show($"启动 DNS 服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // 遍历所有适配器
                NetworkAdapter activeAdapter = null;
                foreach (var adapter in GetNetworkAdapters(ScopeNeeded.FriendlyNameNotNullOnly))
                {
                    if (adapter.FriendlyName == AdaptersCombo.SelectedItem?.ToString())
                    {
                        // 如果适配器的名称和下拉框选中的适配器相同，就记录下来备用并退出循环
                        activeAdapter = adapter;
                        break;
                    }
                }

                if (activeAdapter != null)
                {
                    WriteLog($"指定网络适配器为： {activeAdapter.FriendlyName}", LogLevel.Info);
                    await SetLoopbackDNS(activeAdapter);
                    FlushDNSCache();
                }
                else
                {
                    WriteLog($"没有找到指定的网络适配器！", LogLevel.Warning);
                    if (MessageBox.Show($"没有找到指定的网络适配器！您可能需要手动设置。\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) StartProcess(当您找不到当前正在使用的适配器或启动时遇到适配器设置失败时, useShellExecute: true);
                }
            }
            else
            {
                // 域名解析模式不是 DNS 服务的情况，仅需要启动主服务
                if (!IsProcessRunning(NginxProcessName))
                {
                    WriteLog($"主服务未运行，将启动主服务。", LogLevel.Info);
                    StartNginx();
                }
            }

            // 更新服务的状态信息
            UpdateServiceStatus();

            WriteLog("完成 StartService。", LogLevel.Debug);
        }

        /// <summary>
        /// 停止所有服务
        /// </summary>
        public async Task StopService()
        {
            WriteLog("进入 StopService。", LogLevel.Debug);

            if (IsProcessRunning("SNIBypass"))
            {
                WriteLog($"主服务运行中，将停止主服务。", LogLevel.Info);
                ServiceStatusText.Text = "主服务停止中";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);
                try
                {
                    KillProcess("SNIBypass");
                }
                catch (Exception ex)
                {
                    WriteLog($"尝试停止主服务时遇到异常。", LogLevel.Error, ex);
                    MessageBox.Show($"停止主服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (IsAcrylicServiceRunning())
            {
                WriteLog($"DNS 服务运行中，将停止 DNS 服务。", LogLevel.Info);
                ServiceStatusText.Text = "DNS服务停止中";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);
                try
                {
                    StopAcrylicService();
                }
                catch (Exception ex)
                {
                    WriteLog($"尝试停止 DNS 服务时遇到异常。", LogLevel.Error);
                    MessageBox.Show($"停止 DNS 服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // 更新服务的状态信息
            UpdateServiceStatus();

            // 获取所有网络适配器
            List<NetworkAdapter> adapters = GetNetworkAdapters(ScopeNeeded.FriendlyNameNotNullOnly);
            NetworkAdapter activeAdapter = null;

            // 遍历所有适配器
            foreach (var adapter in adapters)
            {
                if (adapter.FriendlyName == AdaptersCombo.SelectedItem?.ToString())
                {
                    activeAdapter = adapter;
                    break;
                }
            }
            if (activeAdapter != null) await RestoreAdapterDNS(activeAdapter);
            WriteLog("完成 StopService。", LogLevel.Debug);
        }

        /// <summary>
        /// 刷新状态按钮点击事件
        /// </summary>
        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 RefreshBtn_Click。", LogLevel.Debug);
            UpdateServiceStatus();
            UpdateAdaptersCombo();
            WriteLog("完成 RefreshBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 启动按钮点击事件
        /// </summary>
        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 StartBtn_Click。", LogLevel.Debug);

            // 禁用按钮，防手贱重复启动，此时指定适配器也不可以更改
            StartBtn.IsEnabled = StopBtn.IsEnabled = AdaptersCombo.IsEnabled = GetActiveAdapterBtn.IsEnabled = false;

            if (AdaptersCombo.SelectedItem != null) INIWrite(ProgramSettings, SpecifiedAdapter, AdaptersCombo.SelectedItem.ToString(), INIPath);

            if (INIRead(AdvancedSettings, DomainNameResolutionMethod, INIPath) == DnsServiceMode && string.IsNullOrEmpty(AdaptersCombo.SelectedItem?.ToString()))
            {
                // 域名解析为DNS模式但没有选择网络适配器的情况
                MessageBox.Show("请先在下拉框中选择当前正在使用的适配器！您可以尝试点击“自动获取”按钮。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                GetActiveAdapterBtn.IsEnabled = AdaptersCombo.IsEnabled = true;
            }
            else
            {
                // 从配置文件更新 Hosts
                UpdateHostsFromConfig();

                // 启动服务
                await StartService();

                // 实验性功能：Pixiv IP优选
                if (StringToBool(INIRead(ProgramSettings, PixivIPPreference, INIPath))) PixivIPPreferenceEx();

                FlushDNSCache();
            }

            // 重新启用按钮
            StartBtn.IsEnabled = StopBtn.IsEnabled = true;

            WriteLog("完成 StartBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 停止按钮点击事件
        /// </summary>
        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 StopBtn_Click。", LogLevel.Debug);

            // 禁用按钮，防手重复停止
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = false;

            // 移除所有条目以消除对系统的影响
            RemoveHostsRecords();

            // 停止服务
            await StopService();

            // 实验性功能：Pixiv IP优选
            if (StringToBool(INIRead(ProgramSettings, PixivIPPreference, INIPath)))
            {
                try
                {
                    RemoveSection(SystemHosts, "s.pximg.net");
                }
                catch (UnauthorizedAccessException ex)
                {
                    WriteLog($"对系统 Hosts 的访问被拒绝！", LogLevel.Error, ex);
                    if (MessageBox.Show($"对系统 Hosts 的访问被拒绝。\r\n{ex}\r\n点击“是”将为您展示有关帮助。", "错误", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes) StartProcess(当您遇到对系统hosts的访问被拒绝的提示时, useShellExecute: true);
                }
                catch (Exception ex)
                {
                    WriteLog($"遇到异常。", LogLevel.Error, ex);
                    MessageBox.Show($"更新 Hosts 文件时遇到异常。\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // 刷新DNS缓存
            FlushDNSCache();

            // 重新启用按钮，此时适配器也可以更改
            StartBtn.IsEnabled = StopBtn.IsEnabled = AdaptersCombo.IsEnabled = GetActiveAdapterBtn.IsEnabled = true;

            WriteLog("完成 StopBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 设置开机启动按钮点击事件
        /// </summary>
        private void SetStartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 SetStartBtn_Click。", LogLevel.Debug);
            try
            {
                // 使用 TaskService 来访问和操作任务计划程序
                using (TaskService ts = new())
                {
                    // 尝试获取已存在的同名任务
                    Microsoft.Win32.TaskScheduler.Task existingTask = ts.GetTask(TaskName);

                    // 如果任务已存在，则删除它，以便创建新的任务
                    if (existingTask != null)
                    {
                        WriteLog($"计划任务 {TaskName} 已经存在，进行移除。", LogLevel.Warning);
                        ts.RootFolder.DeleteTask(TaskName);
                    }

                    // 创建一个新的任务定义
                    TaskDefinition td = ts.NewTask();

                    // 设置任务的描述信息和作者
                    td.RegistrationInfo.Description = "开机启动 SNIBypassGUI 并自动启动服务";
                    td.RegistrationInfo.Author = "SNIBypassGUI";

                    // 将登录触发器添加到任务定义中
                    td.Triggers.Add(new LogonTrigger());

                    // 创建一个执行操作，指定要执行的 Nginx 路径、参数和工作目录
                    ExecAction execAction = new(SNIBypassGUIExeFilePath, null, null);

                    // 将执行操作添加到任务定义中
                    td.Actions.Add(execAction);

                    // 设置为管理员组
                    td.Principal.GroupId = @"BUILTIN\Administrators";

                    // 设置任务以最高权限运行
                    td.Principal.RunLevel = TaskRunLevel.Highest;

                    // 设置任务所需的安全登录方法为“组”
                    td.Principal.LogonType = TaskLogonType.Group;

                    // 在根文件夹中注册新的任务定义
                    ts.RootFolder.RegisterTaskDefinition(TaskName, td);
                }
                WriteLog("成功设置 SNIBypassGUI 为开机启动。", LogLevel.Info);

                // 显示提示信息，表示已成功设置为开机启动
                MessageBox.Show("成功设置 SNIBypassGUI 为开机启动！\r\n当开机自动启动时，将会自动在托盘图标运行并启动服务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteLog($"尝试设置 SNIBypassGUI 为开机启动时遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"设置开机启动时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            WriteLog("完成 SetStartBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 停止开机启动按钮点击事件
        /// </summary>
        private void StopStartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 StopStartBtn_Click。", LogLevel.Debug);
            try
            {
                // 使用 TaskService 来访问和操作任务计划程序
                using (TaskService ts = new())
                {
                    // 尝试获取已存在的同名任务
                    Microsoft.Win32.TaskScheduler.Task existingTask = ts.GetTask(TaskName);

                    // 如果任务已存在，则删除它
                    if (existingTask != null)
                    {
                        WriteLog($"计划任务 {TaskName} 存在，进行移除。", LogLevel.Info);
                        ts.RootFolder.DeleteTask(TaskName);
                    }
                }
                WriteLog("成功停止 SNIBypassGUI 的开机启动。", LogLevel.Info);
                MessageBox.Show("成功停止 StartSNIBypassGUI 的开机启动！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteLog($"尝试停止 SNIBypassGUI 的开机启动时遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"停止开机启动时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            WriteLog("完成 StopStartBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 退出工具按钮点击事件
        /// </summary>
        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 ExitBtn_Click。", LogLevel.Debug);

            AnimateWindow(1, 0, async () =>
            {
                // 先隐藏窗体，在后台退出程序
                Hide();

                // 隐藏托盘图标
                TaskbarIcon.Visibility = Visibility.Collapsed;

                // 刷新托盘图标
                RefreshNotification();

                // 移除所有条目以消除对系统的影响
                RemoveHostsRecords();

                // 停止服务
                await StopService();

                // 实验性功能： Pixiv IP 优选
                if (StringToBool(INIRead(ProgramSettings, PixivIPPreference, INIPath)))
                {
                    try
                    {
                        RemoveSection(SystemHosts, "s.pximg.net");
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        WriteLog($"对系统 Hosts 的访问被拒绝！", LogLevel.Error, ex);
                        if (MessageBox.Show($"对系统 Hosts 的访问被拒绝。\r\n{ex}\r\n点击“是”将为您展示有关帮助。", "错误", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes) StartProcess(当您遇到对系统hosts的访问被拒绝的提示时, useShellExecute: true);
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"遇到异常。", LogLevel.Error, ex);
                        MessageBox.Show($"更新 Hosts 文件时遇到异常。\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // 刷新DNS缓存
                FlushDNSCache();

                // 清空暂存数据
                INIWrite(TemporaryData, PreviousPrimaryDNS, "", INIPath);
                INIWrite(TemporaryData, PreviousAlternativeDNS, "", INIPath);
                INIWrite(TemporaryData, IsPreviousDnsAutomatic, "true", INIPath);

                // 退出程序
                Environment.Exit(0);
            });

            // 不必要的日志记录
            WriteLog("完成 ExitBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 更新数据按钮点击事件
        /// </summary>
        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 UpdateBtn_Click。", LogLevel.Debug);

            // 禁用更新数据按钮防止点来点去
            UpdateBtn.IsEnabled = false;

            // 修改按钮内容以提示用户正在进行更新
            UpdateBtn.Content = "获取数据…";

            try
            {
                string jsonResponse;
                string fastestProxy = DefaultProxy;
                bool needproxy = false;
                try
                {
                    jsonResponse = await GetAsync(MainLatest);
                }
                catch(Exception ex)
                {
                    WriteLog("从主服务获取信息失败，将使用备用服务器。", LogLevel.Error, ex);
                    fastestProxy = await FindFastestProxy(BackupLatest);
                    jsonResponse = await GetAsync($"https://{fastestProxy}/{BackupLatest}");
                    needproxy = true;
                }

                // 解析返回的 JSON 字符串
                JObject updateData = JObject.Parse(jsonResponse);
                JObject files = (JObject)updateData["files"];

                // 从解析后的 JSON 中获取最后一次发布的信息
                string version = updateData["version"].ToString();
                string exehash = updateData["sha256"].ToString();

                string acrylichostsUrl = files["AcrylicHosts_All.dat"].ToString();
                string systemhostsUrl = files["SystemHosts_All.dat"].ToString();
                string switchdataUrl = files["SwitchData.json"].ToString();
                string crtUrl = files["SNIBypassCrt.crt"].ToString();
                string nginxconfUrl = files["nginx.conf"].ToString();
                string exeUrl = files["SNIBypassGUI.exe"].ToString();

                if (needproxy)
                {
                    acrylichostsUrl = $"https://{fastestProxy}/{acrylichostsUrl}";
                    systemhostsUrl = $"https://{fastestProxy}/{systemhostsUrl}";
                    switchdataUrl = $"https://{fastestProxy}/{switchdataUrl}";
                    crtUrl = $"https://{fastestProxy}/{crtUrl}";
                    nginxconfUrl = $"https://{fastestProxy}/{nginxconfUrl}";
                    exeUrl = $"https://{fastestProxy}/{exeUrl}";
                }

                // 下载文件并覆盖
                await DownloadFileWithProgress(acrylichostsUrl, AcrylicHostsAll, UpdateProgress, 10);
                await DownloadFileWithProgress(systemhostsUrl, SystemHostsAll, UpdateProgress, 10);
                await DownloadFileWithProgress(switchdataUrl, SwitchData, UpdateProgress, 10);
                await DownloadFileWithProgress(crtUrl, CRTFile, UpdateProgress, 10);
                await DownloadFileWithProgress(nginxconfUrl, nginxConfigFile, UpdateProgress, 10);

                if (version != CurrentVersion)
                {
                    UpdateBtn.Content = "更新主程序…";
                    await DownloadFileWithProgress(exeUrl, NewVersionExe, UpdateProgress, 120);
                    UpdateBtn.Content = "校验哈希值…";
                    string realhash = CalculateFileHash(NewVersionExe);
                    if (realhash == exehash)
                    {
                        TryDelete(OldVersionExe);
                        File.Move(CurrentExe, OldVersionExe);
                        File.SetAttributes(OldVersionExe, File.GetAttributes(OldVersionExe) | FileAttributes.Hidden);
                        File.Copy(NewVersionExe, CurrentExe);
                        UpdateBtn.Content = "更新完成";
                    }
                    else
                    {
                        TryDelete(NewVersionExe);
                        WriteLog($"主程序更新失败，哈希值校验未通过！预期：{exehash}，现有：{realhash}", LogLevel.Error);
                        MessageBox.Show($"主程序更新失败，哈希值校验未通过！\r\n预期：{exehash}\r\n现有：{realhash}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                MessageBox.Show($"数据更新完成，将会为您重启主程序。", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                StartProcess(CurrentExe, $"/waitForParent {Process.GetCurrentProcess().Id}");
                ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (HttpRequestException ex)
            {
                WriteLog("尝试更新数据时遇到异常。", LogLevel.Error, ex);
                MessageBox.Show("获取信息或下载文件请求失败，请稍后重试！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TaskCanceledException ex)
            {
                WriteLog("尝试更新数据时遇到异常。", LogLevel.Error, ex);
                MessageBox.Show("获取信息或下载文件超时，请稍后重试！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                WriteLog("尝试更新数据时遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"更新数据时遇到异常： {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UpdateBtn.IsEnabled = true;
                UpdateBtn.Content = "更新数据";
            }
            WriteLog("完成 UpdateBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 用于更新下载进度
        /// </summary>
        private void UpdateProgress(double progress)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateBtn.Content = $"下载中… ({progress:F1}%)";
            });
        }

        /// <summary>
        /// 清理按钮点击事件
        /// </summary>
        private async void CleanBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 CleanBtn_Click。", LogLevel.Debug);

            // 禁用按钮并显示提示信息
            CleanBtn.IsEnabled = false;

            // 停止定期更新大小的计时器
            tempFilesSizeUpdateTimer.Stop();

            CleanBtn.Content = "服务停止中…";
            await StopService();
            CleanBtn.Content = "服务运行日志及缓存清理中…";
            try
            {
                TryDelete(TempFilesPaths);
                if (IsLogEnabled)
                {
                    WriteLog($"GUI 调试开启，将不会删除调试日志。", LogLevel.Info);
                    MessageBox.Show("GUI 调试开启时将不会删除调试日志，请尝试关闭 GUI 调试。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else DeleteLogs();
            }
            catch (Exception ex)
            {
                WriteLog($"尝试清理临时文件时遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"清理临时文件时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog($"服务运行日志及缓存清理完成。", LogLevel.Info);

            // 重新启用定期更新大小的计时器
            tempFilesSizeUpdateTimer.Start();

            MessageBox.Show("服务运行日志及缓存清理完成，请自行重启服务！", "清理", MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateTempFilesSize();

            // 重新启用按钮
            CleanBtn.IsEnabled = true;

            WriteLog("完成 CleanBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 选项卡发生改变事件
        /// </summary>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != sender) return;
            WriteLog("进入 TabControl_SelectionChanged。", LogLevel.Debug);
            if (sender is TabControl tabControl)
            {
                // 可以在这里安全地使用 tabControl，因为它已经被确认为非空的 TabControl 实例
                // 尝试将 TabControl 的选中项转换为 TabItem
                var selectedItem = tabControl.SelectedItem as TabItem;

                // 根据选中项的标题来决定执行哪个操作
                switch (selectedItem?.Header.ToString())
                {
                    case "主页":
                        // 不 UpdateAdaptersCombo() 是因为会引发绑定异常
                        UpdateServiceStatus();
                        serviceStatusUpdateTimer?.Start();
                        controlsStatusUpdateTimer?.Stop();
                        tempFilesSizeUpdateTimer?.Stop();
                        break;
                    case "开关列表":
                        SyncControlsFromConfig();
                        controlsStatusUpdateTimer?.Stop();
                        serviceStatusUpdateTimer?.Stop();
                        tempFilesSizeUpdateTimer?.Stop();
                        break;
                    case "设置":
                        UpdateTempFilesSize();
                        SyncControlsFromConfig();
                        controlsStatusUpdateTimer?.Start();
                        tempFilesSizeUpdateTimer?.Start();
                        serviceStatusUpdateTimer?.Stop();
                        break;
                }
            }
            WriteLog("完成 TabControl_SelectionChanged。", LogLevel.Debug);
        }

        /// <summary>
        /// 全部开启按钮点击事件
        /// </summary>
        private void AllOnBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 AllOnBtn_Click。", LogLevel.Debug);
            bool IsChanged = false;

            // 遍历所有开关
            foreach (var pair in Switchs)
            {
                ToggleButton toggleButtonInstance = (ToggleButton)FindName(pair.ToggleButtonName);
                if (toggleButtonInstance != null && toggleButtonInstance.IsChecked != true) toggleButtonInstance.IsChecked = IsChanged = true;
            }

            // 有开关发生改变时才启用更改有关按钮
            if (IsChanged) ApplyBtn.IsEnabled = UnchangeBtn.IsEnabled = true;

            WriteLog("完成 AllOnBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 全部关闭按钮点击事件
        /// </summary>
        private void AllOffBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 AllOffBtn_Click。", LogLevel.Debug);
            bool IsChanged = false;
            foreach (var pair in Switchs)
            {
                ToggleButton toggleButtonInstance = (ToggleButton)FindName(pair.ToggleButtonName);
                if (toggleButtonInstance != null && toggleButtonInstance.IsChecked != false)
                {
                    toggleButtonInstance.IsChecked = false;
                    IsChanged = true;
                }
            }
            if (IsChanged) ApplyBtn.IsEnabled = UnchangeBtn.IsEnabled = true;
            WriteLog("完成 AllOffBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 安装证书按钮点击事件
        /// </summary>
        private void InstallCertBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 InstallCertBtn_Click。", LogLevel.Debug);
            try
            {
                if (IsCertificateInstalled(CertificateThumbprint)) UninstallCertificate(CertificateThumbprint);
                InstallCertificate(CERFile);
                MessageBox.Show($"成功安装证书！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteLog($"尝试安装证书时遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"安装证书时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            WriteLog("完成 InstallCertBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 自定义背景按钮点击事件
        /// </summary>
        private void CustomBkgBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 CustomBkgBtn_Click。", LogLevel.Debug);
            CustomBkgBtn.IsEnabled = false;
            AnimateWindow(1, 0, () =>
            {
                Hide();
                CustomBackgroundWindow customBackgroundWindow = new();
                customBackgroundWindow.ShowDialog();
                Show();
                AnimateWindow(0, 1);
                CustomBkgBtn.IsEnabled = true;
            });
            WriteLog("完成 CustomBkgBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 恢复默认背景按钮点击事件
        /// </summary>
        private void DefaultBkgBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 DefaultBkgBtn_Click。", LogLevel.Debug);

            // 清空背景文件夹
            ClearFolder(BackgroundDirectory);

            // 释放默认背景图片
            foreach (var pair in DefaultBackgrounds) ExtractResourceToFile(pair.Value, Path.Combine(BackgroundDirectory, pair.Key));

            var files = ImageExtensions.SelectMany(ext => Directory.GetFiles(BackgroundDirectory, $"*{ext}"));
            var hashes = files.Select(CalculateFileHash);
            string imageOrder = string.Join(",", hashes);

            // 写入配置文件
            INIWrite(BackgroundSettings, ImageOrder, imageOrder, INIPath);
            INIWrite(BackgroundSettings, ChangeInterval, "15", INIPath);
            INIWrite(BackgroundSettings, ChangeMode, SequentialMode, INIPath);

            // 通知背景服务更新
            BackgroundService.CleanAllCache();
            BackgroundService.ReloadConfig();
            BackgroundService.ValidateCurrentImage();

            WriteLog("完成 DefaultBkgBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 代理开关点击事件
        /// </summary>
        private void ToggleButtonsClick(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 ToggleButtonsClick。", LogLevel.Debug);
            ApplyBtn.IsEnabled = UnchangeBtn.IsEnabled = true;
            WriteLog("完成 ToggleButtonsClick。", LogLevel.Debug);
        }

        /// <summary>
        /// 链接文本点击事件
        /// </summary>
        private void LinkText_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            WriteLog("进入 LinkText_PreviewMouseDown。", LogLevel.Debug);
            string url = string.Empty;
            if (sender is TextBlock textblock) url = textblock.Text;
            else if (sender is Run run) url = run.Text;
            if (!string.IsNullOrEmpty(url))
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "https://" + url;
                WriteLog($"用户点击的链接被识别为 {url}。", LogLevel.Info);
                StartProcess(url, useShellExecute: true);
            }
            WriteLog("完成 LinkText_PreviewMouseDown。", LogLevel.Debug);
        }

        /// <summary>
        /// 应用更改按钮点击事件
        /// </summary>
        private void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 ApplyBtn_Click。", LogLevel.Debug);

            // 禁用有关按钮
            ApplyBtn.IsEnabled = UnchangeBtn.IsEnabled = false;

            ApplyBtn.Content = "应用更改中";

            // 如果DNS服务在运行，则稍后需要重启
            bool WasDnsServiceRunning = IsAcrylicServiceRunning();

            try
            {
                StopAcrylicService();
            }
            catch (Exception ex)
            {
                WriteLog($"尝试停止 DNS 服务时遇到异常。", LogLevel.Error);
                MessageBox.Show($"停止 DNS 服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // 根据开关状态更新配置文件
            UpdateConfigFromToggleButtons();

            // 从配置文件同步到 Hosts
            UpdateHostsFromConfig();

            if (WasDnsServiceRunning) StartAcrylicService();
            RemoveAcrylicCacheFile();

            // 刷新DNS缓存
            FlushDNSCache();

            ApplyBtn.Content = "应用更改";
            WriteLog("完成 ApplyBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 取消更改按钮点击事件
        /// </summary>
        private void UnchangeBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 UnchangeBtn_Click。", LogLevel.Debug);
            ApplyBtn.IsEnabled = UnchangeBtn.IsEnabled = false;
            SyncControlsFromConfig();
            WriteLog("完成 UnchangeBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 菜单：显示主窗口点击事件
        /// </summary>
        private void MenuItem_ShowMainWin_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 MenuItem_ShowMainWin_Click。", LogLevel.Debug);
            if ((Math.Abs(Opacity) < 1e-6 || IsVisible == false || IsActive == false) && !IsAnyDialogOpen())
            {
                Hide();
                AnimateWindow(0, 1);
            }
            Show();
            Activate();
            WriteLog("完成 MenuItem_ShowMainWin_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 菜单：启动服务点击事件
        /// </summary>
        private void MenuItem_StartService_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 MenuItem_StartService_Click。", LogLevel.Debug);
            StartBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            TaskbarIcon.ShowBalloonTip("服务启动完成", "您现在可以尝试访问列表中的网站。", BalloonIcon.Info);
            WriteLog("完成 MenuItem_StartService_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 菜单：停止服务点击事件
        /// </summary>
        private void MenuItem_StopService_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 MenuItem_StopService_Click。", LogLevel.Debug);
            StopBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            TaskbarIcon.ShowBalloonTip("服务停止完成", "感谢您的使用 ~\\(≥▽≤)/~", BalloonIcon.Info);
            WriteLog("完成 MenuItem_StopService_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 菜单：退出工具点击事件
        /// </summary>
        private void MenuItem_ExitTool_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 MenuItem_ExitTool_Click。", LogLevel.Debug);
            ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WriteLog("进入 MenuItem_ExitTool_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 托盘图标点击事件
        /// </summary>
        public async void TaskbarIcon_LeftClick()
        {
            WriteLog("进入 TaskbarIcon_LeftClicsk。", LogLevel.Debug);
            MenuItem_ShowMainWin.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await UpdateYiyan();
            WriteLog("完成 TaskbarIcon_LeftClick。", LogLevel.Debug);
        }

        /// <summary>
        /// 最小化到托盘图标运行按钮点击事件
        /// </summary>
        private void TaskbarIconRunBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 TaskbarIconRunBtn_Click。", LogLevel.Debug);
            TaskbarIconRunBtn.IsEnabled = false;
            AnimateWindow(1, 0, () =>
            {
                Hide();
                TaskbarIcon.ShowBalloonTip("已最小化运行", "点击图标显示主窗体或右键显示菜单", BalloonIcon.Info);
                TaskbarIconRunBtn.IsEnabled = true;
            });
            WriteLog("完成 TaskbarIconRunBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 用于 JSON 解析的结构
        /// </summary>
        public class SwitchList
        {
            public SwitchJsonItem[] switchs { get; set; }
        }
        public class SwitchJsonItem
        {
            public string favicon { get; set; }
            public string switchtitle { get; set; }
            public string[] linkstext { get; set; }
            public string togglebuttonname { get; set; }
            public string sectionname { get; set; }
        }

        /// <summary>
        /// 窗口加载完成事件
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 Window_Loaded。", LogLevel.Debug);

            // 隐藏窗口避免视觉卡顿
            Hide();

            // 将版本号显示在标题
            WindowTitle.Text = "SNIBypassGUI " + CurrentVersion;

            // 绑定事件
            serviceStatusUpdateTimer.Tick += ServiceStatusUpdateTimer_Tick;
            tempFilesSizeUpdateTimer.Tick += TempFilesSizeUpdateTimer_Tick;
            controlsStatusUpdateTimer.Tick += ControlsStatusUpdateTimer_Tick;
            MainTabControl.SelectionChanged += TabControl_SelectionChanged;

            // 将开关添加到列表
            await AddSwitchesToList();

            // 如果有配置中不存在的项，则开启
            List<String> ExistingKeys = GetKeys(ProxySettings, INIPath);
            foreach (SwitchItem item in Switchs)
            {
                if (!ExistingKeys.Contains(item.SectionName)) INIWrite(ProxySettings, item.SectionName, "true", INIPath);
            }

            // 更新信息
            SyncControlsFromConfig();

            // 基本加载完成后显示窗口
            Show();
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(1),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);

            // 更新适配器列表和服务状态
            UpdateAdaptersCombo();
            UpdateServiceStatus();

            // 判断证书是否安装
            if (!IsCertificateInstalled(CertificateThumbprint))
            {
                WriteLog($"未找到指纹为 {CertificateThumbprint} 的证书，安装。", LogLevel.Info);
                InstallCertificate(CERFile);
            }

            try
            {
                if (!IsAcrylicServiceInstalled()) await InstallAcrylicService();
                else if (GetServiceBinaryPath(DnsServiceName) != AcrylicServiceExeFilePath)
                {
                    // 如果此 DNS 服务非彼 DNS 服务，则直接重装
                    await UninstallAcrylicService();
                    await InstallAcrylicService();
                }
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // 启用有关计时器
            serviceStatusUpdateTimer.Start();

            // 区别由服务启动与用户启动，如果是由服务启动，则托盘运行并自动启动服务
            if (Environment.CurrentDirectory != Path.GetDirectoryName(currentDirectory))
            {
                WriteLog("程序应为计划任务启动，将托盘运行并自动启动服务。", LogLevel.Info);
                TaskbarIconRunBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                MenuItem_StartService.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }

            // 更新一言
            await UpdateYiyan();

            WriteLog("完成 Window_Loaded。", LogLevel.Debug);
        }

        /// <summary>
        ///控件状态更新计时器触发事件
        /// </summary>
        private void ControlsStatusUpdateTimer_Tick(object sender, EventArgs e) => SyncControlsFromConfig();

        /// <summary>
        /// 临时文件大小更新计时器触发事件
        /// </summary>
        private void TempFilesSizeUpdateTimer_Tick(object sender, EventArgs e) => UpdateTempFilesSize();

        /// <summary>
        /// 服务状态更新计时器触发事件
        /// </summary>
        private void ServiceStatusUpdateTimer_Tick(object sender, EventArgs e) => UpdateServiceStatus();

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            WriteLog("进入 Window_Closing。", LogLevel.Debug);
            e.Cancel = true;
            TaskbarIconRunBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            WriteLog("完成 Window_Closing。", LogLevel.Debug);
        }

        /// <summary>
        /// 菜单选项鼠标进入事件
        /// </summary>
        private void MenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            WriteLog("进入 MenuItem_MouseEnter。", LogLevel.Debug);
            if (sender is MenuItem menuitem)
            {
                string color = menuitem.Header.ToString() switch
                {
                    "显示主窗口" => "#00A2FF",
                    "启动服务" => "#FF2BFF00",
                    "停止服务" => "#FFFF0000",
                    "退出工具" => "#FFFF00C7",
                    _ => null
                };
                menuitem.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                menuitem.Header = $"『{menuitem.Header}』";
                menuitem.FontSize += 2;
            }
            WriteLog("完成 MenuItem_MouseEnter。", LogLevel.Debug);
        }

        /// <summary>
        /// 菜单选项鼠标离开事件
        /// </summary>
        private void MenuItem_MouseLeave(object sender, MouseEventArgs e)
        {
            WriteLog("进入 MenuItem_MouseLeave。", LogLevel.Debug);
            if (sender is MenuItem menuitem)
            {
                menuitem.Header = $"{menuitem.Header.ToString().Substring(1, menuitem.Header.ToString().Length - 2)}";
                menuitem.Foreground = new SolidColorBrush(Colors.White);
                menuitem.FontSize -= 2;
            }
            WriteLog("完成 MenuItem_MouseLeave。", LogLevel.Debug);
        }

        /// <summary>
        /// 调试模式按钮点击事件
        /// </summary>
        private void DebugModeBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 DebugModeBtn_Click。", LogLevel.Debug);
            if (!StringToBool(INIRead(AdvancedSettings, DebugMode, INIPath)) && MessageBox.Show("调试模式仅供测试和开发使用，强烈建议您在没有开发者明确指示的情况下不要随意打开。\r\n是否打开调试模式？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) INIWrite(AdvancedSettings, DebugMode, "true", INIPath);
            else
            {
                INIWrite(AdvancedSettings, DebugMode, "false", INIPath);
                INIWrite(AdvancedSettings, GUIDebug, "false", INIPath);
                INIWrite(AdvancedSettings, DomainNameResolutionMethod, DnsServiceMode, INIPath);
                INIWrite(AdvancedSettings, AcrylicDebug, "false", INIPath);
            }
            SyncControlsFromConfig();
            WriteLog("完成 DebugModeBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 域名解析模式按钮点击事件
        /// </summary>
        private void SwitchDomainNameResolutionMethodBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 SwitchDomainNameResolutionMethodBtn_Click。", LogLevel.Debug);
            if (INIRead(AdvancedSettings, DomainNameResolutionMethod, INIPath) != SystemHostsMode && MessageBox.Show("在 DNS 服务无法正常启动的情况下，系统 Hosts 可以作为备选方案使用，\r\n但具有一定局限性（例如 pixivFANBOX 的作者页面需要手动向系统 Hosts 添加记录）。\r\n是否切换域名解析模式为系统 Hosts？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) INIWrite(AdvancedSettings, DomainNameResolutionMethod, SystemHostsMode, INIPath);
            else INIWrite(AdvancedSettings, DomainNameResolutionMethod, DnsServiceMode, INIPath);
            SyncControlsFromConfig();
            WriteLog("完成 SwitchDomainNameResolutionMethodBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// DNS 服务调试按钮点击事件
        /// </summary>
        private void AcrylicDebugBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 AcrylicDebugBtn_Click。", LogLevel.Debug);
            if (!StringToBool(INIRead(AdvancedSettings, AcrylicDebug, INIPath)) && MessageBox.Show("开启 DNS 服务调试可以诊断某些问题，重启服务后生效。\r\n请在重启直到程序出现问题后，将 \\data\\dns 目录下的 AcrylicDebug.txt 提交给开发者。\r\n是否打开 DNS 服务调试？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) INIWrite(AdvancedSettings, AcrylicDebug, "true", INIPath);
            else INIWrite(AdvancedSettings, AcrylicDebug, "false", INIPath);
            SyncControlsFromConfig();
            WriteLog("完成 AcrylicDebugBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// GUI 调试按钮点击事件
        /// </summary>
        private void GUIDebugBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 GUIDebugBtn_Click。", LogLevel.Debug);
            if (!StringToBool(INIRead(AdvancedSettings, GUIDebug, INIPath)) && MessageBox.Show("开启 GUI 调试模式可以更准确地诊断问题，但生成日志会产生额外的性能开销，请在不需要时关闭。\r\n开启后将自动关闭程序，重启程序后生效。\r\n请在重启直到程序出现问题后，将 \\data\\logs 目录下的 GUI.log 提交给开发者。\r\n是否打开 GUI 调试模式并重启？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                INIWrite(AdvancedSettings, GUIDebug, "true", INIPath);
                StartProcess(CurrentExe, $"/waitForParent {Process.GetCurrentProcess().Id}");
                ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            else INIWrite(AdvancedSettings, GUIDebug, "false", INIPath);
            SyncControlsFromConfig();
            WriteLog("完成 GUIDebugBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 编辑系统 Hosts 按钮点击事件
        /// </summary>
        private void EditHostsBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 EditHostsBtn_Click。", LogLevel.Debug);
            if (File.Exists(SystemHosts)) StartProcess("notepad.exe", SystemHosts);
            else
            {
                WriteLog("未在指定路径找到系统 Hosts ！", LogLevel.Warning);
                MessageBox.Show("未在指定路径找到系统 Hosts ！\r\n请尝试手动创建该文件。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            WriteLog("完成 EditHostsBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 一键卸载按钮点击事件
        /// </summary>
       private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 UninstallBtn_Click。", LogLevel.Debug);
            if (MessageBox.Show("该功能将立即从系统上移除本程序并消除本程序对系统设置所作的有关修改。\r\n是否继续卸载？", "卸载", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                UninstallBtn.Content = "卸载中…";
                UninstallBtn.IsEnabled = false;
                try
                {
                    UninstallCertificate(CertificateThumbprint);
                    StopBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    foreach (SwitchItem pair in Switchs)
                    {
                        WriteLog($"移除 {pair.SectionName} 的记录部分。", LogLevel.Info);
                        RemoveSection(SystemHosts, pair.SectionName);
                    }
                    // 获取所有网络适配器
                    List<NetworkAdapter> adapters = GetNetworkAdapters(ScopeNeeded.FriendlyNameNotNullOnly);
                    NetworkAdapter activeAdapter = null;
                    // 遍历所有适配器
                    foreach (var adapter in adapters)
                    {
                        if (adapter.FriendlyName == INIRead(ProgramSettings, SpecifiedAdapter, INIPath))
                        {
                            activeAdapter = adapter;
                            break;
                        }
                    }
                    if (activeAdapter != null) await RestoreAdapterDNS(activeAdapter);
                    FlushDNSCache();
                    await UninstallAcrylicService();
                    Directory.Delete(dataDirectory, true);
                    string bat = $"@echo off\r\n"
                                         + "timeout /t 1 /nobreak >nul\r\n"
                                         + $"taskkill /f /pid {Process.GetCurrentProcess().Id} >nul 2>&1\r\n"
                                         + "timeout /t 1 /nobreak >nul\r\n"
                                         + $"del /f /q \"{CurrentExe}\" >nul 2>&1\r\n"
                                         + $"if exist \"{CurrentExe}\" (\r\n"
                                         + "echo MsgBox \"卸载失败，请手动删除文件！\", 48, \"警告\" > \"%temp%\\temp.vbs\"\r\n"
                                         + "cscript /nologo \"%temp%\\temp.vbs\" >nul\r\n"
                                         + "del \"%temp%\\temp.vbs\"\r\n"
                                         + ") else (\r\n"
                                         + "echo MsgBox \"卸载成功！\", 64, \"提示\" > \"%temp%\\temp.vbs\"\r\n"
                                         + "cscript /nologo \"%temp%\\temp.vbs\" >nul\r\n"
                                         + "del \"%temp%\\temp.vbs\"\r\n"
                                         + ")\r\n"
                                         + "start /b cmd /c del \"%~f0\"";
                    string batPath = Path.Combine(Path.GetTempPath(), "uninstall.bat");
                    File.WriteAllText(batPath, bat, Encoding.Default);
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = batPath,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    });
                    ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (UnauthorizedAccessException ex)
                {
                    WriteLog($"对系统 Hosts 的访问被拒绝！", LogLevel.Error, ex);
                    if (MessageBox.Show($"对系统 Hosts 的访问被拒绝。\r\n{ex}\r\n点击“是”将为您展示有关帮助。", "错误", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes) StartProcess(当您遇到对系统hosts的访问被拒绝的提示时, useShellExecute: true);
                }
                catch (Exception ex)
                {
                    WriteLog($"遇到异常。", LogLevel.Error, ex);
                    MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            WriteLog("完成 UninstallBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 适配器下拉框选择项改变事件
        /// </summary>
        private void AdaptersCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WriteLog("进入 AdaptersCombo_SelectionChanged。", LogLevel.Debug);
            if (AdaptersCombo.SelectedItem != null) INIWrite(ProgramSettings, SpecifiedAdapter, AdaptersCombo.SelectedItem.ToString(), INIPath);
            e.Handled = true;
            WriteLog("完成 AdaptersCombo_SelectionChanged。", LogLevel.Debug);
        }

        /// <summary>
        /// 自动获取活动适配器按钮点击事件
        /// </summary>
        private void GetActiveAdapterBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 GetActiveAdapterBtn_Click。", LogLevel.Debug);
            UpdateAdaptersCombo();
            uint? interfaceIndex = GetDefaultRouteInterfaceIndex();
            NetworkAdapter activeAdapter = null;
            if (interfaceIndex.HasValue)
            {
                List<NetworkAdapter> adapters = GetNetworkAdapters(ScopeNeeded.FriendlyNameNotNullOnly);
                activeAdapter = adapters.FirstOrDefault(a => a.InterfaceIndex == interfaceIndex.Value);
            }
            if (activeAdapter != null && AdaptersCombo.Items.OfType<string>().Contains(activeAdapter.FriendlyName))
            {
                AdaptersCombo.SelectedItem = activeAdapter.FriendlyName;
                INIWrite(ProgramSettings, SpecifiedAdapter, activeAdapter.FriendlyName, INIPath);
            }
            else
            {
                WriteLog($"没有找到活动且可设置的网络适配器！", LogLevel.Warning);
                if (MessageBox.Show($"没有找到活动且可设置的网络适配器！您可能需要手动设置。\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) StartProcess(当您找不到当前正在使用的适配器或启动时遇到适配器设置失败时, useShellExecute: true);
            }
            WriteLog("完成 GetActiveAdapterBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 如何选择适配器按钮点击事件
        /// </summary>
        private void HelpBtn_HowToFindActiveAdapter_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 HelpBtn_HowToFindActiveAdapter_Click。", LogLevel.Debug);
            StartProcess(当您无法确定当前正在使用的适配器时, useShellExecute: true);
            WriteLog("完成 HelpBtn_HowToFindActiveAdapter_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 实验性功能：Pixiv IP 优选
        /// </summary>
        private void PixivIPPreferenceEx()
        {
            WriteLog("进入 PixivIPPreferenceEx。", LogLevel.Debug);
            try
            {
                RemoveSection(SystemHosts, "s.pximg.net");
                string ip = FindFastestIP(pximgIP);
                if (ip != null)
                {
                    string[] NewAPIRecord =
                    [
                    "#\ts.pximg.net Start",
                    $"{ip}       s.pximg.net",
                    "#\ts.pximg.net End",
                    ];
                    PrependToFile(SystemHosts, NewAPIRecord);
                    FlushDNSCache();
                    PixivIPPreferenceBtn.Content = "Pixiv IP 优选：开";
                }
                else WriteLog("Pixiv IP 优选失败，没有找到最优 IP。", LogLevel.Warning);
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteLog($"对系统 Hosts 的访问被拒绝！", LogLevel.Error, ex);
                if (MessageBox.Show($"对系统 Hosts 的访问被拒绝。\r\n{ex}\r\n点击“是”将为您展示有关帮助。", "错误", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes) StartProcess(当您遇到对系统hosts的访问被拒绝的提示时, useShellExecute: true);
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            WriteLog("完成 PixivIPPreferenceEx。", LogLevel.Debug);
        }

        /// <summary>
        /// 反馈按钮点击事件
        /// </summary>
        private void FeedbackBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 FeedbackBtn_Click。", LogLevel.Debug);
            FeedbackBtn.IsEnabled = false;
            AnimateWindow(1, 0, () =>
            {
                Hide();
                var feedbackWindow = new FeedbackWindow();
                feedbackWindow.ShowDialog();
                Show();
                AnimateWindow(0, 1);
                FeedbackBtn.IsEnabled = true;
            });
            WriteLog("完成 FeedbackBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// Pixiv IP 优选按钮点击事件
        /// </summary>
        private void PixivIPPreferenceBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 PixivIPPreferenceBtn_Click。", LogLevel.Debug);
            PixivIPPreferenceBtn.IsEnabled = false;
            if (!StringToBool(INIRead(ProgramSettings, PixivIPPreference, INIPath)) && MessageBox.Show("Pixiv IP 优选是实验性功能。\r\n当您遇到服务正常运行，但打开 Pixiv 白屏时可以尝试使用此功能。\r\n您要打开该功能吗？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                INIWrite(ProgramSettings, PixivIPPreference, "true", INIPath);
                PixivIPPreferenceEx();
            }
            else
            {
                INIWrite(ProgramSettings, PixivIPPreference, "false", INIPath);
                RemoveSection(SystemHosts, "s.pximg.net");
            }
            PixivIPPreferenceBtn.IsEnabled = true;
            WriteLog("完成 PixivIPPreferenceBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 还原数据按钮点击事件
        /// </summary>
        private async void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 ResetBtn_Click。", LogLevel.Debug);
            if (MessageBox.Show("还原数据功能用于将本程序关联的数据文件恢复为初始状态。\r\n当您认为本程序更新造成了关联的数据文件损坏，或您对有关规则做出了修改时可以使用此功能。\r\n是否还原数据？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ResetBtn.IsEnabled = false;
                ResetBtn.Content = "还原中…";
                try
                {
                    await StopService();
                    TryDelete(nginxConfigFile);
                    TryDelete(CRTFile);
                    TryDelete(AcrylicHostsAll);
                    TryDelete(SystemHostsAll);
                    TryDelete(SwitchData);
                    InitializeDirectoriesAndFiles();
                }
                catch (Exception ex)
                {
                    WriteLog($"遇到异常。", LogLevel.Error, ex);
                    MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    ResetBtn.IsEnabled = true;
                    ResetBtn.Content = "还原数据";
                }
            }
            WriteLog("完成 ResetBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 主题切换按钮点击事件
        /// </summary>
        private void ThemeSwitchTB_Checked(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 ThemeSwitchTB_Checked。", LogLevel.Debug);
            SwitchTheme(true);
            INIWrite(ProgramSettings, ThemeMode, DarkMode, INIPath);
            WriteLog("完成 ThemeSwitchTB_Checked。", LogLevel.Debug);
        }
        private void ThemeSwitchTB_Unchecked(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 ThemeSwitchTB_Unchecked。", LogLevel.Debug);
            SwitchTheme(false);
            INIWrite(ProgramSettings, ThemeMode, LightMode, INIPath);
            WriteLog("完成 ThemeSwitchTB_Unchecked。", LogLevel.Debug);
        }

        /// <summary>
        /// 切换主题
        /// </summary>
        public void SwitchTheme(bool isNightMode)
        {
            Color currentBackgroundColor = (Color)Application.Current.Resources["BackgroundColor"];
            Color currentBorderColor = (Color)Application.Current.Resources["BorderColor"];
            Color currentSwitchTextColor = ((SolidColorBrush)ThemeSwitchTBText.Foreground).Color;

            Color targetBackground = isNightMode ? Color.FromArgb(0x70, 0x61, 0x61, 0x61) : Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF);
            Color targetBorder = isNightMode ? Colors.Black : Colors.White;
            Color targetSwitchText = isNightMode ? (Color)ColorConverter.ConvertFromString("#C4C9D4") : (Color)ColorConverter.ConvertFromString("#F3C62B");

            var newBackgroundBrush = new SolidColorBrush(currentBackgroundColor);
            var newBorderBrush = new SolidColorBrush(currentBorderColor);
            var newSwitchTextBrush = new SolidColorBrush(currentSwitchTextColor);

            Application.Current.Resources["BackgroundColor"] = targetBackground;
            Application.Current.Resources["BorderColor"] = targetBorder;

            var switchTextAnimation = new ColorAnimation
            {
                To = targetSwitchText,
                Duration = TimeSpan.FromSeconds(0.8),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var backgroundAnimation = new ColorAnimation
            {
                To = targetBackground,
                Duration = TimeSpan.FromSeconds(0.8),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var borderAnimation = new ColorAnimation
            {
                To = targetBorder,
                Duration = TimeSpan.FromSeconds(0.8),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            newBackgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, backgroundAnimation);
            newBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, borderAnimation);
            newSwitchTextBrush.BeginAnimation(SolidColorBrush.ColorProperty, switchTextAnimation);

            Application.Current.Resources["BackgroundBrush"] = newBackgroundBrush;
            Application.Current.Resources["BorderBrush"] = newBorderBrush;
            ThemeSwitchTBText.Foreground = newSwitchTextBrush;
        }

        /// <summary>
        /// 窗口动画逻辑
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

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            BackgroundService.Cleanup();
            BackgroundService.PropertyChanged -= OnBackgroundChanged;
            base.OnClosed(e);
        }

        /// <summary>
        /// 指示是否有任何对话框打开
        /// </summary>
        /// <returns></returns>
        private static bool IsAnyDialogOpen()
        {
            foreach (Window window in Application.Current.Windows) if (window is FeedbackWindow || window is CustomBackgroundWindow) return true;
            return false;
        }

        /// <summary>
        /// 播放窗口动画
        /// </summary>
        private void AnimateWindow(double from, double to, Action onCompleted = null)
        {
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(0.8),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            if (onCompleted != null)
            {
                animation.Completed += (s, e) => onCompleted();
            }

            BeginAnimation(OpacityProperty, animation);
        }

        public class RelayCommand(Action execute, Func<bool> canExecute = null) : ICommand
        {
            private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            private readonly Func<bool> _canExecute = canExecute;

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

            public void Execute(object parameter) => _execute();
        }
    }

    public static class ToggleButtonAttach
    {
        #region IsAutoFold
        [AttachedPropertyBrowsableForType(typeof(ToggleButton))]
        public static bool GetIsAutoFold(ToggleButton control) => (bool)control.GetValue(IsAutoFoldProperty);

        public static void SetIsAutoFold(ToggleButton control, bool value)
        {
            control.SetValue(IsAutoFoldProperty, value);
        }

        public static readonly DependencyProperty IsAutoFoldProperty =
            DependencyProperty.RegisterAttached("IsAutoFold", typeof(bool), typeof(ToggleButtonAttach),
                new PropertyMetadata(false, ToggleButtonChanged));

        private static void ToggleButtonChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            if (o is not ToggleButton control)
                return;

            if ((bool)e.NewValue)
            {
                control.Loaded += Control_Loaded;
                control.MouseLeave += Control_MouseLeave;
                control.Checked += Control_Checked;
                control.Unchecked += Control_Checked;
            }
            else
            {
                control.Loaded -= Control_Loaded;
                control.Checked -= Control_Checked;
                control.Unchecked -= Control_Checked;
                VisualStateManager.GoToState(control, "Normal", false);
            }
        }

        private static void Control_Loaded(object sender, RoutedEventArgs e)
        {
            var control = (ToggleButton)sender;
            UpdateVisualState(control);
        }

        private static void Control_Checked(object sender, RoutedEventArgs e)
        {
            var control = (ToggleButton)sender;
            if (control.IsMouseOver) return;
            UpdateVisualState(control);
        }

        private static void Control_MouseLeave(object sender, MouseEventArgs e)
        {
            var control = (ToggleButton)sender;
            UpdateVisualState(control);
        }

        private static void UpdateVisualState(ToggleButton control)
        {
            var state = control.IsChecked == true ? "MouseLeaveChecked" : "MouseLeaveUnChecked";
            if (control.IsMouseOver) state = "MouseOver";
            VisualStateManager.GoToState(control, state, true);
        }
        #endregion
    }
}