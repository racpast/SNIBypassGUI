using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json.Linq;
using static SNIBypassGUI.PublicHelper;
using static SNIBypassGUI.LogHelper;
using RpNet.FileHelper;
using RpNet.NetworkHelper;
using RpNet.AcrylicServiceHelper;
using RpNet.TaskBarHelper;
using Task = System.Threading.Tasks.Task;
using System.Windows.Controls.Primitives;
using System.Security.Cryptography.X509Certificates;

namespace SNIBypassGUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 定义用于更新信息的计时器
        private readonly DispatcherTimer _TabAUpdateTimer = new DispatcherTimer
        {
            // 设置 timer 的时间间隔为每5秒触发一次
            Interval = TimeSpan.FromSeconds(5)
        };
        private readonly DispatcherTimer _TabCUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };

        // 创建 TaskbarIconLeftClickCommand 实例，并将它作为 DataContext 的一部分传递。
        public TaskbarIconLeftClickCommand _TaskbarIconLeftClickCommand { get; }

        // 窗体构造函数
        public MainWindow()
        {
            OutputLog = StringBoolConverter.StringToBool(ConfigINI.INIRead("高级设置", "GUIDebug", PathsSet.INIPath));

            foreach (var headerline in GUILogHead)
            {
                /** 日志信息 **/ WriteLog(headerline, LogLevel.None);
            }

            /** 日志信息 **/ WriteLog("进入MainWindow。", LogLevel.Debug);

            // 初始化
            InitializeComponent();

            // 检查是否已经开启程序，如果已开启则退出
            string MName = Process.GetCurrentProcess().MainModule.ModuleName;
            string PName = Path.GetFileNameWithoutExtension(MName);
            Process[] GUIProcess = Process.GetProcessesByName(PName);
            if (GUIProcess.Length > 1)
            {
                /** 日志信息 **/ WriteLog("检测到程序已经在运行，将退出程序。", LogLevel.Warning);

                HandyControl.Controls.MessageBox.Show("SNIBypassGUI 已经在运行！\r\n请检查是否有托盘图标！(((ﾟДﾟ;)))", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                Environment.Exit(1);
                return;
            }

            // 将 MainWindow 作为 DataContext 设置
            this.DataContext = this;
            _TaskbarIconLeftClickCommand = new TaskbarIconLeftClickCommand(this);

            // 窗口可拖动
            this.TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };

            // 刷新托盘图标，避免有多个托盘图标出现
            TaskBarIconHelper.RefreshNotification();

            /** 日志信息 **/ WriteLog("完成MainWindow。", LogLevel.Debug);
        }

        // 将代理开关添加到列表的方法
        private void AddSwitchItems()
        {
            /** 日志信息 **/ WriteLog("进入AddSwitchItems。", LogLevel.Debug);

            int ItemIndex = 0;
            foreach(SwitchItem item in Switchs)
            {
                Image favicon = new Image
                {
                    Source = new BitmapImage(new Uri(item.FaviconImageSource, UriKind.RelativeOrAbsolute)),
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(10, 10, 10, 5),
                };
                Grid.SetColumn(favicon, 0);
                Grid.SetRow(favicon, ItemIndex);
                // 添加站点图标
                Switchlist.Children.Add(favicon);

                TextBlock textBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 3, 10, 3)
                };
                textBlock.Inlines.Add(new Run { Text = item.SwitchTitle, FontWeight = FontWeights.Bold, FontSize = 18 });
                textBlock.Inlines.Add(new LineBreak());
                if (item.LinksText.Contains('|'))
                {
                    string[] parts = item.LinksText.Split('|');

                    foreach (var part in parts)
                    {
                        if (part == "、" || part == "等")
                        {
                            textBlock.Inlines.Add(new Run { Text = part, FontSize = 15, FontWeight = FontWeights.Bold});
                        }
                        else
                        {
                            Run run = new Run { Text = part, Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xF9, 0xFF)), FontSize = 15, Cursor = Cursors.Hand, FontFamily = new FontFamily("Microsoft Tai Le") };
                            run.PreviewMouseDown += LinkText_PreviewMouseDown;
                            textBlock.Inlines.Add(run);
                        }
                    }
                }
                else
                {
                    Run run = new Run { Text = item.LinksText, Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0xF9, 0xFF)), FontSize = 15, Cursor = Cursors.Hand, FontFamily = new FontFamily("Microsoft Tai Le") };
                    run.PreviewMouseDown += LinkText_PreviewMouseDown;
                    textBlock.Inlines.Add(run);
                }
                Grid.SetColumn(textBlock, 1);
                Grid.SetRow(textBlock, ItemIndex);
                // 添加站点标题及链接
                Switchlist.Children.Add(textBlock);

                ToggleButton toggleButton = new ToggleButton
                {
                    Width = 40,
                    Margin = new Thickness(5, 0, 5, 0),
                    IsChecked = true,
                    Style = (Style)FindResource("ToggleButtonSwitch")
                };
                toggleButton.Click += ToggleButtonsClick;
                Grid.SetColumn(toggleButton, 2);
                Grid.SetRow(toggleButton, ItemIndex);
                // 添加切换开关
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
                Switchlist.RegisterName(item.ToggleButtonName, toggleButton);

                Switchlist.RowDefinitions.Add(new RowDefinition());

                ItemIndex++;
            }
            // 设置首列与尾列背景
            Grid.SetRowSpan(FirstColumnBorder, ItemIndex);
            Grid.SetRowSpan(LastColumnBorder, ItemIndex);

            /** 日志信息 **/ WriteLog("完成AddSwitchItems。", LogLevel.Debug);
        }

        // 主页更新计时器触发事件
        private void TabAUpdateTimer_Tick(object sender, EventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入TabAUpdateTimer_Tick。", LogLevel.Debug);

            // 更新服务状态
            UpdateServiceStatus();

            // 更新适配器列表
            UpdateAdaptersCombo();

            /** 日志信息 **/ WriteLog("完成TabAUpdateTimer_Tick。", LogLevel.Debug);
        }

        // 设置页面更新计时器触发事件
        private void TabCUpdateTimer_Tick(object sender, EventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入TabCUpdateTimer_Tick。", LogLevel.Debug);

            // 更新清理日志按钮的内容，显示所有日志文件的总大小（以MB为单位）
            CleanBtn.Content = $"清理服务运行日志及缓存 ({FileHelper.GetTotalFileSizeInMB(PathsSet.TempFilesPathsIncludingGUILog)}MB)";

            // 从配置文件同步调试按钮状态
            SyncControlsFromConfig();

            /** 日志信息 **/ WriteLog("完成TabCUpdateTimer_Tick。", LogLevel.Debug);
        }

        // 更新适配器列表的方法
        private void UpdateAdaptersCombo()
        {
            /** 日志信息 **/ WriteLog("进入UpdateAdaptersCombo。", LogLevel.Debug);

            // 记录先前选中的适配器，以便在更新下拉框之后重新选中
            string PreviousSelectedAdapter = AdaptersCombo.SelectedItem?.ToString();

            // 获取已经连接的适配器
            List<NetworkAdapter> adapters = NetworkAdapter.GetNetworkAdapters(NetworkAdapter.ScopeNeeded.ConnectedOnly);

            // 清空下拉框避免重复添加
            AdaptersCombo.Items.Clear();

            // 将名字不为空的适配器逐个添加到下拉框
            foreach (var adapter in adapters)
            {
                if (!string.IsNullOrEmpty(adapter.Caption))
                {
                    /** 日志信息 **/ WriteLog($"向适配器列表添加{adapter.Caption}。", LogLevel.Info);

                    AdaptersCombo.Items.Add(adapter.Caption);
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
                /** 日志信息 **/ WriteLog($"适配器列表中丢失{PreviousSelectedAdapter}，取消选中。", LogLevel.Warning);

                // 如果没有匹配的项，取消选中
                AdaptersCombo.SelectedItem = null;
            }

            /** 日志信息 **/ WriteLog("完成UpdateAdaptersCombo。", LogLevel.Debug);
        }

        // 更新服务状态的方法
        public void UpdateServiceStatus()
        {
            /** 日志信息 **/ WriteLog("进入UpdateServiceStatus。", LogLevel.Debug);

            // 使用 Process.GetProcessesByName 方法获取所有名为 "SNIBypass" 的进程，这将返回一个包含所有匹配进程的 Process 数组
            Process[] ps1 = Process.GetProcessesByName("SNIBypass");

            // 检查获取到的进程数组长度是否大于 0，如果大于 0，说明主服务正在运行
            bool IsNginxRunning = ps1.Length > 0;

            // 检查DNS服务是否在运行
            bool IsDnsRunning = AcrylicService.AcrylicServiceIsRunning();

            /** 日志信息 **/ WriteLog($"主服务运行中：{StringBoolConverter.BoolToYesNo(IsNginxRunning)}",LogLevel.Info);
            /** 日志信息 **/ WriteLog($"DNS服务运行中：{StringBoolConverter.BoolToYesNo(IsDnsRunning)}",LogLevel.Info);

            // 根据不同情况显示不同的服务状态文本
            if (IsNginxRunning && IsDnsRunning)
            {
                // 主服务和DNS服务都在运行的情况
                ServiceStatusText.Text = "当前服务状态：\r\n主服务和DNS服务运行中";
                TaskbarIconServiceST.Text = "主服务和DNS服务运行中";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.ForestGreen);
                TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.ForestGreen);
                AdaptersCombo.IsEnabled = false;
                GetActiveAdapterBtn.IsEnabled = false;
            }
            else if (IsNginxRunning)
            {
                // 仅主服务在运行的情况
                ServiceStatusText.Text = "当前服务状态：\r\n主服务运行中，但DNS服务未运行";
                TaskbarIconServiceST.Text = "仅主服务运行中";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);
                TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);
                AdaptersCombo.IsEnabled = false;
                GetActiveAdapterBtn.IsEnabled = false;
            }
            else if (IsDnsRunning)
            {
                // 仅DNS服务在运行的情况
                ServiceStatusText.Text = "当前服务状态：\r\n主服务未运行，但DNS服务运行中";
                TaskbarIconServiceST.Text = "仅DNS服务运行中";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);
                TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);
                AdaptersCombo.IsEnabled = false;
                GetActiveAdapterBtn.IsEnabled = false;
            }
            else
            {
                // 服务都不在运行的情况
                ServiceStatusText.Text = "当前服务状态：\r\n主服务与DNS服务未运行";
                TaskbarIconServiceST.Text = "主服务与DNS服务未运行";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.Red);
                TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.Red);
                AdaptersCombo.IsEnabled = true;
                GetActiveAdapterBtn.IsEnabled = true;
            }

            /** 日志信息 **/ WriteLog("完成UpdateServiceStatus。", LogLevel.Debug);
        }

        // 更新背景图片的方法
        public void UpdateBackground()
        {
            /** 日志信息 **/ WriteLog("进入UpdateBackground。", LogLevel.Debug);

            // 设置图片源为默认背景图片并设置图片的拉伸模式为均匀填充，以适应背景区域
            ImageBrush bg = new ImageBrush();
            bg.ImageSource = new BitmapImage(new Uri("pack://application:,,,/SNIBypassGUI;component/Resources/DefaultBkg.png"));
            bg.Stretch = Stretch.UniformToFill;

            if (ConfigINI.INIRead("程序设置", "Background", PathsSet.INIPath) == "Custom")
            {
                // 程序设置中背景为自定义的情况
                if (File.Exists(PathsSet.CustomBackground))
                {
                    // 如果找到了背景图片
                    // 用资源释放型的读取来获取背景图片
                    bg.ImageSource = FileHelper.GetImage(PathsSet.CustomBackground);

                    /** 日志信息 **/ WriteLog($"背景图片将设置为自定义：{PathsSet.CustomBackground}。", LogLevel.Info);
                }
                else
                {
                    // 如果没有找到背景图片的路径

                    /** 日志信息 **/ WriteLog("背景图片设置为自定义但未在指定位置找到文件，或被删除？将恢复为默认。", LogLevel.Warning);

                    // 将配置设置回默认背景
                    ConfigINI.INIWrite("程序设置", "Background", "Preset", PathsSet.INIPath);
                }
            }

            // 设置背景图片
            MainPage.Background = bg;

            /** 日志信息 **/ WriteLog("完成UpdateBackground。", LogLevel.Debug);
        }

        // 检查必要目录、文件的存在性，并在必要时创建或释放的方法
        public void InitializeDirectoriesAndFiles()
        {
            /** 日志信息 **/ WriteLog("进入InitializeDirectoriesAndFiles。", LogLevel.Debug);

            // 确保必要目录存在
            foreach (string directory in PathsSet.NeccesaryDirectories)
            {
                FileHelper.EnsureDirectoryExists(directory);
            }

            // 释放相关文件
            foreach (var pair in PathToResourceDic)
            {
                if (!File.Exists(pair.Key))
                {
                    /** 日志信息 **/ WriteLog($"文件{pair.Key}不存在，释放。", LogLevel.Info);

                    FileHelper.ExtractNormalFileInResx(pair.Value, pair.Key);
                }
            }

            if (!File.Exists(PathsSet.INIPath))
            {
                // 如果配置文件不存在，则创建配置文件

                /** 日志信息 **/ WriteLog($"配置文件{PathsSet.INIPath}不存在，创建。", LogLevel.Info);

                // 写入初始配置
                foreach (var config in InitialConfigurations)
                {
                    var sections = config.Key.Split(':');
                    if (sections.Length == 2)
                    {
                        ConfigINI.INIWrite(sections[0], sections[1], config.Value, PathsSet.INIPath);
                    }
                }

                // 写入初始代理开关配置
                foreach (SwitchItem pair in Switchs)
                {
                    ConfigINI.INIWrite("代理开关", pair.SectionName, "true", PathsSet.INIPath);
                }
            }

            /** 日志信息 **/ WriteLog("完成InitializeDirectoriesAndFiles。", LogLevel.Debug);
        }

        // 更新一言的方法
        public async Task UpdateYiyan()
        {
            /** 日志信息 **/ WriteLog("进入UpdateYiyan。", LogLevel.Debug);

            try
            {
                string YiyanJson = await HTTPHelper.GetAsync("https://v1.hitokoto.cn/?c=d");

                /** 日志信息 **/ WriteLog($"获取到一言的数据为{YiyanJson}。", LogLevel.Debug);

                // 将返回的JSON字符串解析为JObject
                JObject repodata = JObject.Parse(YiyanJson);

                // 提取一言文本、来源于作者数据
                string Hitokoto = repodata["hitokoto"].ToString();
                string From = repodata["from"].ToString();
                string FromWho = repodata["from_who"].ToString();

                /** 日志信息 **/ WriteLog($"解析到一言文本为{Hitokoto}，来源为{From}，作者为{FromWho}。", LogLevel.Info);

                // 将一言与相关信息显示在托盘图标悬浮文本
                TaskbarIconYiyan.Text = Hitokoto;
                TaskbarIconYiyanFrom.Text = $"—— {FromWho}「{From}」";
            }
            catch (Exception ex)
            {
                /** 日志信息 **/ WriteLog($"遇到异常，将设置为默认一言。", LogLevel.Error, ex);

                TaskbarIconYiyan.Text = PresetYiyan;
                TaskbarIconYiyanFrom.Text = PresetYiyanForm;
            }

            /** 日志信息 **/ WriteLog("完成UpdateYiyan。", LogLevel.Debug);
        }

        // 从配置文件向 Hosts 文件更新的方法
        public void UpdateHostsFromConfig()
        {
            /** 日志信息 **/ WriteLog("进入UpdateHostsFromConfig。", LogLevel.Debug);

            // 根据域名解析模式判断要更新的文件
            bool IsDnsService = ConfigINI.INIRead("高级设置", "DomainNameResolutionMethod", PathsSet.INIPath) == "DnsService";
            string FileShouldUpdate = IsDnsService ? PathsSet.AcrylicHostsPath : PathsSet.SystemHosts;

            /** 日志信息 **/ WriteLog($"当前域名解析方法是否为DNS服务：{StringBoolConverter.BoolToYesNo(IsDnsService)}，将更新的文件为{FileShouldUpdate}。", LogLevel.Info);

            // 根据域名解析模式获取应该添加的条目数据
            string CorrespondingHosts = IsDnsService ? "HostsRecord" : "OldHostsRecord";

            // 移除所有条目部分，防止重复添加
            RemoveHosts();

            // 遍历条目部分名称
            foreach (SwitchItem pair in Switchs)
            {
                if (StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", pair.SectionName, PathsSet.INIPath)) == true)
                {
                    // 条目部分名称对应的开关是打开的情况

                    /** 日志信息 **/ WriteLog($"{pair.SectionName}的代理开关为开启，将添加记录。", LogLevel.Info);

                    // 添加该条目部分
                    FileHelper.WriteLinesToFileEnd((string[])pair.GetType().GetProperty(CorrespondingHosts).GetValue(pair), FileShouldUpdate);
                }
            }

            /** 日志信息 **/ WriteLog("完成UpdateHostsFromConfig。", LogLevel.Debug);
        }

        // 移除 Hosts 中全部有关记录的方法
        public void RemoveHosts()
        {
            /** 日志信息 **/ WriteLog("进入RemoveHosts。", LogLevel.Debug);

            // 根据域名解析模式判断要更新的文件
            bool IsDnsService = ConfigINI.INIRead("高级设置", "DomainNameResolutionMethod", PathsSet.INIPath) == "DnsService";
            string FileShouldUpdate = IsDnsService ? PathsSet.AcrylicHostsPath : PathsSet.SystemHosts;

            /** 日志信息 **/ WriteLog($"当前域名解析方法是否为DNS服务：{StringBoolConverter.BoolToYesNo(IsDnsService)}，将更新的文件为{FileShouldUpdate}。", LogLevel.Info);

            foreach (SwitchItem pair in Switchs)
            {
                /** 日志信息 **/ WriteLog($"移除{pair.SectionName}的记录部分。", LogLevel.Info);

                FileHelper.RemoveSection(FileShouldUpdate, pair.SectionName);
            }

            /** 日志信息 **/ WriteLog("完成RemoveHosts。", LogLevel.Debug);
        }

        // 从配置文件同步有关控件的方法
        public void SyncControlsFromConfig()
        {
            /** 日志信息 **/ WriteLog("进入SyncControlsFromConfig。", LogLevel.Debug);

            // 更新代理开关状态
            foreach (SwitchItem pair in Switchs)
            {
                bool ShouldChecked = StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", pair.SectionName, PathsSet.INIPath));

                ToggleButton toggleButtonInstance = (ToggleButton)this.FindName(pair.ToggleButtonName);
                if (toggleButtonInstance != null)
                {
                    toggleButtonInstance.IsChecked = ShouldChecked;
                    /** 日志信息 **/ WriteLog($"开关{toggleButtonInstance.Name}从配置键{pair.SectionName}同步状态：{StringBoolConverter.BoolToYesNo(ShouldChecked)}。", LogLevel.Debug);
                }
            }

            // 更新调试有关按钮
            if (StringBoolConverter.StringToBool(ConfigINI.INIRead("高级设置", "DebugMode", PathsSet.INIPath)))
            {
                DebugModeBtn.Content = "调试模式：\n开";
                SwitchDomainNameResolutionMethodBtn.IsEnabled = true;
                AcrylicDebugBtn.IsEnabled = true;
                GUIDebugBtn.IsEnabled = true;
            }
            else
            {
                DebugModeBtn.Content = "调试模式：\n关";
                SwitchDomainNameResolutionMethodBtn.IsEnabled = false;
                AcrylicDebugBtn.IsEnabled = false;
                GUIDebugBtn.IsEnabled = false;
            }

            if (StringBoolConverter.StringToBool(ConfigINI.INIRead("高级设置", "GUIDebug", PathsSet.INIPath)))
            {
                GUIDebugBtn.Content = "GUI调试：\n开";
                OutputLog = true;
            }
            else
            {
                GUIDebugBtn.Content = "GUI调试：\n关";
                OutputLog = false;
            }

            if (ConfigINI.INIRead("高级设置", "DomainNameResolutionMethod", PathsSet.INIPath) == "DnsService")
            {
                SwitchDomainNameResolutionMethodBtn.Content = "域名解析：\nDNS服务";
            }
            else
            {
                SwitchDomainNameResolutionMethodBtn.Content = "域名解析：\n系统hosts";
            }

            if (StringBoolConverter.StringToBool(ConfigINI.INIRead("高级设置", "AcrylicDebug", PathsSet.INIPath)))
            {
                AcrylicDebugBtn.Content = "DNS服务调试：\n开";
                AcrylicService.CreateAcrylicServiceDebugLog();
            }
            else
            {
                AcrylicDebugBtn.Content = "DNS服务调试：\n关";
                AcrylicService.RemoveAcrylicServiceDebugLog();
            }

            /// <summary>
            /// 实验性功能：Pixiv IP优选
            /// </summary>
            if (StringBoolConverter.StringToBool(ConfigINI.INIRead("程序设置", "PixivIPPreference", PathsSet.INIPath)))
            {
                PixivIPPreferenceBtn.Content = "Pixiv IP优选：开";
            }
            else
            {
                PixivIPPreferenceBtn.Content = "Pixiv IP优选：关";
            }

            // 获取配置中记录的适配器
            string activeAdapter = ConfigINI.INIRead("程序设置", "ActiveAdapter", PathsSet.INIPath);

            // 尝试在适配器列表重新选中
            // Cast<string>() 假定 AdaptersCombo.Items 中的所有项都是 string 类型。如果不是，可能会遇到运行时错误。
            // 使用 OfType<string>() 来安全地处理不同类型的项。OfType<string>() 会自动跳过非字符串类型的项。
            if (AdaptersCombo.Items.OfType<string>().Any(item => item == activeAdapter))
            {
                // SelectedItem 会确保 AdaptersCombo 正确选中与 PreviousSelectedAdapter 匹配的项。
                // 使用 Text 设置文本会导致 AdaptersCombo 显示 PreviousSelectedAdapter，但它并不意味着该项被选中了（特别是当该项不在 Items 中时）。
                AdaptersCombo.SelectedItem = activeAdapter;
            }
            else
            {
                /** 日志信息 **/ WriteLog($"适配器列表中丢失{activeAdapter}，取消选中。", LogLevel.Warning);

                // 如果没有匹配的项，取消选中

                AdaptersCombo.SelectedItem = null;
            }

            /** 日志信息 **/ WriteLog("完成SyncControlsFromConfig。", LogLevel.Debug);
        }

        // 从代理开关列表向配置文件同步的方法
        public void UpdateConfigFromToggleButtons()
        {
            /** 日志信息 **/ WriteLog("进入UpdateConfigFromToggleButtons。", LogLevel.Debug);

            // 遍历所有代理开关
            foreach (var pair in Switchs)
            {
                ToggleButton toggleButtonInstance = (ToggleButton)this.FindName(pair.ToggleButtonName);
                if (toggleButtonInstance != null)
                {
                    if (toggleButtonInstance.IsChecked == true)
                    {
                        // 代理开关是开启的情况
                        ConfigINI.INIWrite("代理开关", pair.SectionName, "true", PathsSet.INIPath);
                    }
                    else
                    {
                        // 代理开关是关闭的情况
                        ConfigINI.INIWrite("代理开关", pair.SectionName, "false", PathsSet.INIPath);
                    }

                    /** 日志信息 **/ WriteLog($"配置键{pair.SectionName}从开关{toggleButtonInstance.Name}同步状态：{StringBoolConverter.BoolToYesNo(toggleButtonInstance.IsChecked)}。", LogLevel.Debug);
                }
            }

            /** 日志信息 **/ WriteLog("完成UpdateConfigFromToggleButtons。", LogLevel.Debug);
        }

        // 将指定网络适配器首选DNS设置为127.0.0.1，并记录先前的是有效IPv4地址且非127.0.0.1的DNS服务器地址到配置文件中并优先记录到 PreviousDNS1 的方法
        public bool SetLocalDNS(NetworkAdapter Adapter)
        {
            /** 日志信息 **/ WriteLog("进入SetLocalDNS。", LogLevel.Debug);

            try
            {
                /** 日志信息 **/ WriteLog($"开始配置网络适配器：{Adapter.Caption}。", LogLevel.Info);

                // 应该在设置DNS之前记录DNS服务器是否为自动获取
                bool? IsDnsAutomatic = Adapter.IsDnsAutomatic;

                string PreviousDNS1 = null;
                string PreviousDNS2 = null;

                if (Adapter.DNS.Length > 0 ? Adapter.DNS[0].ToString() != "127.0.0.1" : true)
                {
                    // 指定适配器的DNS服务器设置为空，或者首选DNS服务器不为127.0.0.1的情况
                    if (Adapter.DNS.Length == 0)
                    {
                        // DNS服务器设置为空的情况
                        PreviousDNS1 = "";
                        PreviousDNS2 = "";
                    }
                    else if (Adapter.DNS.Length == 1)
                    {
                        // DNS服务器设置中只有一个DNS服务器的情况
                        if (IPValidator.IsValidIPv4(Adapter.DNS[0]))
                        {
                            // 首选DNS服务器是有效IPv4地址的情况
                            PreviousDNS1 = Adapter.DNS[0];
                            PreviousDNS2 = "";
                        }
                        else
                        {
                            // 首选DNS服务器不是有效IPv4地址的情况
                            PreviousDNS1 = "";
                            PreviousDNS2 = "";
                        }
                    }
                    else if (Adapter.DNS.Length == 2)
                    {
                        // DNS服务器设置中有两个DNS服务器的情况
                        if (IPValidator.IsValidIPv4(Adapter.DNS[0]))
                        {
                            // 首选DNS服务器是有效IPv4地址的情况
                            PreviousDNS1 = Adapter.DNS[0];
                            if (IPValidator.IsValidIPv4(Adapter.DNS[1]))
                            {
                                // 备用DNS服务器是有效IPv4地址的情况
                                if (Adapter.DNS[1] == "127.0.0.1")
                                {
                                    // 备用DNS服务器是有效IPv4地址但是是127.0.0.1的情况
                                    PreviousDNS2 = "";
                                }
                                else
                                {
                                    // 备用DNS服务器是有效IPv4地址且不为127.0.0.1的情况
                                    PreviousDNS2 = Adapter.DNS[1];
                                }
                            }
                            else
                            {
                                // 备用DNS服务器不是有效IPv4地址的情况
                                PreviousDNS2 = "";
                            }
                        }
                        else
                        {
                            // 首选DNS服务器不是有效IPv4地址的情况
                            PreviousDNS2 = "";
                            if (IPValidator.IsValidIPv4(Adapter.DNS[1]))
                            {
                                // 备用DNS服务器是有效IPv4地址的情况
                                if (Adapter.DNS[1] == "127.0.0.1")
                                {
                                    // 备用DNS服务器是有效IPv4地址但是是127.0.0.1的情况
                                    PreviousDNS1 = "";
                                }
                                else
                                {
                                    // 备用DNS服务器是有效IPv4地址且不为127.0.0.1的情况
                                    PreviousDNS1 = Adapter.DNS?[1];
                                }
                            }
                            else
                            {
                                // 备用DNS服务器也不是有效IPv4地址的情况
                                PreviousDNS1 = "";
                            }
                        }
                    }

                    // 将指定适配器的DNS服务器设置为首选127.0.0.1
                    Adapter.DNS = new string[] { "127.0.0.1" };

                    // 刷新指定适配器的信息
                    Adapter.Fresh();

                    /** 日志信息 **/ WriteLog($"指定网络适配器是否为自动获取DNS：{StringBoolConverter.BoolToYesNo(IsDnsAutomatic)}", LogLevel.Info);
                    /** 日志信息 **/ WriteLog($"指定网络适配器的DNS成功设置为首选{Adapter.DNS[0]}", LogLevel.Info);
                    /** 日志信息 **/ WriteLog($"将暂存的DNS服务器为：{PreviousDNS1}，{PreviousDNS2}", LogLevel.Debug);

                    // 将停止服务时恢复适配器所需要的信息写入配置文件备用
                    ConfigINI.INIWrite("暂存数据", "PreviousDNS1", PreviousDNS1, PathsSet.INIPath);
                    ConfigINI.INIWrite("暂存数据", "PreviousDNS2", PreviousDNS2, PathsSet.INIPath);
                    ConfigINI.INIWrite("暂存数据", "IsPreviousDnsAutomatic", IsDnsAutomatic.ToString(), PathsSet.INIPath);
                }

                /** 日志信息 **/ WriteLog("完成SetLocalDNS，返回true。", LogLevel.Debug);

                return true;
            }
            catch (NetworkAdapterSetException ex)
            {
                /** 日志信息 **/ WriteLog($"无法设置指定的网络适配器！", LogLevel.Error, ex);

                if (HandyControl.Controls.MessageBox.Show($"无法设置指定的网络适配器！请手动设置！\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(LinksSet.当您找不到当前正在使用的适配器或启动时遇到适配器设置失败时) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                /** 日志信息 **/ WriteLog($"遇到异常。", LogLevel.Error, ex);

                HandyControl.Controls.MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            /** 日志信息 **/ WriteLog("完成SetLocalDNS，返回false。", LogLevel.Debug);

            return false;
        }

        // 从配置文件还原适配器的方法
        public bool RestoreAdapterDNS(NetworkAdapter Adapter)
        {
            /** 日志信息 **/ WriteLog("进入RestoreAdapterDNS。", LogLevel.Debug);

            try
            {
                if (Adapter.DNS.Length > 0 ? Adapter.DNS[0] == "127.0.0.1" : false)
                {
                    // 指定适配器的首选DNS为127.0.0.1的情况，需要从配置文件还原回去
                    if (StringBoolConverter.StringToBool(ConfigINI.INIRead("暂存数据", "IsPreviousDnsAutomatic", PathsSet.INIPath)))
                    {
                        // 指定适配器DNS服务器之前是自动获取的情况
                        // 设置指定适配器DNS服务器为自动获取
                        Adapter.DNS = null;

                        /** 日志信息 **/ WriteLog($"活动网络适配器的DNS成功设置为自动获取。", LogLevel.Info);
                    }
                    else
                    {
                        // 指定适配器DNS服务器之前不是自动获取的情况，需要按照暂存数据设置回去
                        if (string.IsNullOrEmpty(ConfigINI.INIRead("暂存数据", "PreviousDNS1", PathsSet.INIPath)))
                        {
                            // 没有暂存地址存在的情况
                            // 设置指定适配器DNS服务器为自动获取
                            Adapter.DNS = null;

                            /** 日志信息 **/ WriteLog($"指定网络适配器的DNS成功设置为自动获取。", LogLevel.Info);
                        }
                        else
                        {
                            // 暂存地址一存在的情况
                            if (string.IsNullOrEmpty(ConfigINI.INIRead("暂存数据", "PreviousDNS2", PathsSet.INIPath)))
                            {
                                // 暂存地址二不存在的情况
                                Adapter.DNS = new string[] { ConfigINI.INIRead("暂存数据", "PreviousDNS1", PathsSet.INIPath) };

                                /** 日志信息 **/ WriteLog($"指定网络适配器的DNS成功设置为首选{ConfigINI.INIRead("暂存数据", "PreviousDNS1", PathsSet.INIPath)}。", LogLevel.Info);
                            }
                            else
                            {
                                // 暂存地址二存在的情况
                                Adapter.DNS = new string[] { ConfigINI.INIRead("暂存数据", "PreviousDNS1", PathsSet.INIPath), ConfigINI.INIRead("暂存数据", "PreviousDNS2", PathsSet.INIPath) };

                                /** 日志信息 **/ WriteLog($"指定网络适配器的DNS成功设置为首选{ConfigINI.INIRead("暂存数据", "PreviousDNS1", PathsSet.INIPath)}，备用{ConfigINI.INIRead("暂存数据", "PreviousDNS2", PathsSet.INIPath)}。", LogLevel.Info);
                            }
                        }
                    }
                }

                /** 日志信息 **/ WriteLog("完成RestoreAdapterDNS，返回true。", LogLevel.Debug);

                return true;
            }
            catch(NetworkAdapterSetException ex)
            {
                /** 日志信息 **/ WriteLog($"无法还原指定的网络适配器！", LogLevel.Error, ex);

                if (HandyControl.Controls.MessageBox.Show($"无法还原指定的网络适配器！请手动还原！\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(LinksSet.当您在停止时遇到适配器设置失败或不确定该软件是否对适配器造成影响时) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                /** 日志信息 **/ WriteLog($"遇到异常。", LogLevel.Error, ex);

                HandyControl.Controls.MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            /** 日志信息 **/ WriteLog("完成RestoreAdapterDNS，返回false", LogLevel.Debug);

            return false;
        }

        // 启动所有服务的方法
        public async Task StartService()
        {
            /** 日志信息 **/ WriteLog("进入StartService。", LogLevel.Debug);

            if (ConfigINI.INIRead("高级设置", "DomainNameResolutionMethod", PathsSet.INIPath) == "DnsService")
            {
                // 域名解析模式是DNS服务的情况，需要启动主服务、DNS服务与设置适配器
                // 使用 Process.GetProcessesByName 方法获取所有名为“SNIBypass”的进程，这将返回一个包含所有匹配进程的 Process 数组
                Process[] ps1 = Process.GetProcessesByName("SNIBypass");

                // 检查获取到的进程数组长度是否大于 0，如果大于 0，说明主服务正在运行
                bool IsNginxRunning = ps1.Length > 0;

                /** 日志信息 **/ WriteLog($"主服务运行中：{StringBoolConverter.BoolToYesNo(IsNginxRunning)}", LogLevel.Info);

                if (ps1.Length <= 0)
                {
                    /** 日志信息 **/ WriteLog($"主服务未运行，将启动主服务。", LogLevel.Info);

                    // 显示文本提示启动中
                    ServiceStatusText.Text = "当前服务状态：\r\n主服务启动中";
                    ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);

                    // 创建一个新的 Process 对象，用于启动外部进程
                    Process process = new Process();
                    // 设置要启动的进程的文件名
                    process.StartInfo.FileName = PathsSet.nginxPath;
                    // 设置进程的工作目录
                    process.StartInfo.WorkingDirectory = PathsSet.NginxDirectory;
                    // 设置是否使用操作系统外壳启动进程
                    process.StartInfo.UseShellExecute = false;
                    try
                    {
                        // 尝试启动进程
                        process.Start();
                    }
                    catch (Exception ex)
                    {
                        /** 日志信息 **/ WriteLog($"尝试启动主服务时遇到异常。", LogLevel.Error, ex);

                        // 如果启动进程时发生异常，则显示一个错误消息框
                        HandyControl.Controls.MessageBox.Show($"无法启动主服务: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // 更新服务的状态信息
                UpdateServiceStatus();
                
                // 检查DNS服务是否在运行
                bool IsDnsRunning = AcrylicService.AcrylicServiceIsRunning();

                /** 日志信息 **/ WriteLog($"DNS服务运行中：{StringBoolConverter.BoolToYesNo(IsDnsRunning)}", LogLevel.Info);

                if (!IsDnsRunning)
                {
                    // DNS服务未在运行的情况

                    /** 日志信息 **/ WriteLog($"DNS服务未运行，将启动DNS服务。", LogLevel.Info);

                    // 显示文本提示启动中
                    ServiceStatusText.Text = "当前服务状态：\r\nDNS服务启动中";
                    ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);

                    try
                    {
                        await AcrylicService.StartAcrylicService();
                    }
                    catch (Exception ex)
                    {
                        /** 日志信息 **/ WriteLog($"尝试启动DNS服务时遇到异常。", LogLevel.Error, ex);

                        HandyControl.Controls.MessageBox.Show($"无法启动DNS服务:{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // 更新服务的状态信息
                UpdateServiceStatus();

                // 获取所有适配器
                List<NetworkAdapter> adapters = NetworkAdapter.GetNetworkAdapters();
                NetworkAdapter activeAdapter = null;
                // 遍历所有适配器
                foreach (var adapter in adapters)
                {
                    if (adapter.Caption == AdaptersCombo.SelectedItem?.ToString())
                    {
                        // 如果适配器的名称和下拉框选中的适配器相同，就记录下来备用并退出循环
                        activeAdapter = adapter;
                        break;
                    }
                }
                if (activeAdapter != null)
                {
                    // 获取到选中的适配器的情况
                    /** 日志信息 **/ WriteLog($"指定网络适配器为：{activeAdapter.Caption}", LogLevel.Info);

                    // 获取到的适配器不为空的情况
                    if (SetLocalDNS(activeAdapter))
                    {
                        // 成功设置指定适配器DNS服务器的情况
                        // 如果设置成功，说明该适配器可用，则记录到配置文件中来为自动选中做准备
                        ConfigINI.INIWrite("程序设置", "ActiveAdapter", activeAdapter.Caption, PathsSet.INIPath);

                        // 刷新DNS缓存
                        DNS.FlushDNS();

                        // 根据给定域名解析的IP判断是否需要禁用指定适配器的IPv6
                        string IPForIPv6DisableDecision = DNS.GetIpAddressFromDomain(DomainForIPv6DisableDecision);

                        /** 日志信息 **/ WriteLog($"指定的首选DNS服务器设置完成，{DomainForIPv6DisableDecision}解析到{IPForIPv6DisableDecision}。", LogLevel.Info);

                        if (IPValidator.IsValidIPv6(IPForIPv6DisableDecision))
                        {
                            // 如果解析的IP是IPv6，说明当前系统IPv6 DNS优先，需要禁用指定适配器的IPv6

                            try
                            {
                                await activeAdapter.DisableIPv6();
                            }
                            catch(Exception ex)
                            {
                                // 未能禁用指定适配器IPv6的情况

                                /** 日志信息 **/ WriteLog($"指定网络适配器的Internet 协议版本 6(TCP/IPv6)禁用失败！", LogLevel.Error, ex);

                                if (HandyControl.Controls.MessageBox.Show($"指定网络适配器的Internet 协议版本 6(TCP/IPv6)禁用失败！请手动设置！\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                                {
                                    Process.Start(new ProcessStartInfo(LinksSet.当您找不到当前正在使用的适配器或启动时遇到适配器设置失败时) { UseShellExecute = true });
                                }
                            }
                        }
                    }
                }
                else
                {
                    // 未能获取到选中的适配器的情况

                    /** 日志信息 **/ WriteLog($"没有找到指定的网络适配器！", LogLevel.Warning);

                    if (HandyControl.Controls.MessageBox.Show($"没有找到指定的网络适配器！您可能需要手动设置。\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(LinksSet.当您找不到当前正在使用的适配器或启动时遇到适配器设置失败时) { UseShellExecute = true });
                    }
                }
            }
            else
            {
                // 域名解析模式不是DNS服务的情况，仅需要启动主服务
                // 使用 Process.GetProcessesByName 方法获取所有名为 "SNIBypass" 的进程，这将返回一个包含所有匹配进程的 Process 数组
                Process[] ps = Process.GetProcessesByName("SNIBypass");

                // 检查获取到的进程数组长度是否大于 0，如果大于 0，说明主服务正在运行
                bool IsNginxRunning = ps.Length > 0;

                /** 日志信息 **/ WriteLog($"主服务运行中：{StringBoolConverter.BoolToYesNo(IsNginxRunning)}", LogLevel.Info);

                if (ps.Length <= 0)
                {
                    /** 日志信息 **/ WriteLog($"主服务未运行，将启动主服务。", LogLevel.Info);

                    // 显示文本提示启动中
                    ServiceStatusText.Text = "当前服务状态：\r\n主服务启动中";
                    ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);

                    Process process = new Process();
                    process.StartInfo.FileName = PathsSet.nginxPath;
                    process.StartInfo.WorkingDirectory = PathsSet.NginxDirectory;
                    process.StartInfo.UseShellExecute = false;
                    try
                    {
                        process.Start();
                    }
                    catch (Exception ex)
                    {
                        /** 日志信息 **/ WriteLog($"尝试启动主服务时遇到异常。", LogLevel.Error, ex);

                        HandyControl.Controls.MessageBox.Show($"无法启动主服务: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                // 更新服务的状态信息
                UpdateServiceStatus();
            }

            /** 日志信息 **/ WriteLog("完成StartService。", LogLevel.Debug);
        }

        // 停止所有服务的方法
        public async Task StopService()
        {
            /** 日志信息 **/ WriteLog("进入StopService。", LogLevel.Debug);

            // 使用 Process.GetProcessesByName 方法获取所有名为“SNIBypass”的进程，这将返回一个包含所有匹配进程的 Process 数组
            Process[] ps1 = Process.GetProcessesByName("SNIBypass");

            // 检查获取到的进程数组长度是否大于 0，如果大于 0，说明服务正在运行
            bool IsNginxRunning = ps1.Length > 0;

            /** 日志信息 **/ WriteLog($"主服务运行中：{StringBoolConverter.BoolToYesNo(IsNginxRunning)}", LogLevel.Info);

            if (IsNginxRunning)
            {
                /** 日志信息 **/ WriteLog($"主服务运行中，将停止主服务。", LogLevel.Info);

                // 显示文本提示服务停止中
                ServiceStatusText.Text = "当前服务状态：\r\n主服务停止中";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);

                // 创建一个任务列表，用于存储每个杀死进程任务的任务对象
                List<Task> tasks = new List<Task>();
                // 遍历所有找到的“SNIBypass”进程
                foreach (Process process in ps1)
                {
                    // 为每个进程创建一个异步任务，该任务尝试结束进程并处理可能的异常
                    Task task = Task.Run(() =>
                    {
                        try
                        {
                            // 尝试结束当前遍历到的进程
                            process.Kill();
                            // 等待进程退出，最多等待5000毫秒（5秒）
                            bool exited = process.WaitForExit(5000);
                            // 如果进程在超时时间内没有退出，则显示警告消息框
                            if (!exited)
                            {
                                /** 日志信息 **/ WriteLog($"退出进程{process.ProcessName}超时。", LogLevel.Warning);

                                HandyControl.Controls.MessageBox.Show($"退出进程{process.ProcessName}超时。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            /** 日志信息 **/ WriteLog($"尝试停止主服务时遇到异常。", LogLevel.Error, ex);

                            // 如果在结束进程的过程中发生异常，则显示错误消息框
                            HandyControl.Controls.MessageBox.Show($"停止主服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                    // 将创建的任务添加到任务列表中
                    tasks.Add(task);
                }
                // 等待所有结束进程的任务完成
                await Task.WhenAll(tasks);
            }

            // 更新服务的状态信息
            UpdateServiceStatus();

            // 检查DNS服务是否在运行
            bool IsDnsRunning = AcrylicService.AcrylicServiceIsRunning();

            /** 日志信息 **/ WriteLog($"DNS服务运行中：{StringBoolConverter.BoolToYesNo(IsDnsRunning)}", LogLevel.Info);

            if (IsDnsRunning)
            {
                /** 日志信息 **/ WriteLog($"DNS服务运行中，将停止DNS服务。", LogLevel.Info);

                // 显示文本提示服务停止中
                ServiceStatusText.Text = "当前服务状态：\r\nDNS服务停止中";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);

                try
                {
                    await AcrylicService.StopAcrylicService();
                }
                catch (Exception ex)
                {
                    /** 日志信息 **/ WriteLog($"尝试停止DNS服务时遇到异常。", LogLevel.Error);

                    HandyControl.Controls.MessageBox.Show($"停止DNS服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // 更新服务的状态信息
            UpdateServiceStatus();

            // 获取所有网络适配器
            List<NetworkAdapter> adapters = NetworkAdapter.GetNetworkAdapters();
            NetworkAdapter activeAdapter = null;

            // 遍历所有适配器
            foreach (var adapter in adapters)
            {
                if (adapter.Caption == AdaptersCombo.SelectedItem?.ToString())
                {
                    // 如果适配器的名称和下拉框选中的适配器相同，就记录下来备用并退出循环
                    activeAdapter = adapter;
                    break;
                }
            }
            if (activeAdapter != null)
            {
                // 获取到选中的适配器的情况
                if (RestoreAdapterDNS(activeAdapter))
                {
                    try
                    {
                        await activeAdapter.EnableIPv6();
                    }
                    catch(Exception ex)
                    {
                        /** 日志信息 **/ WriteLog($"指定网络适配器的Internet 协议版本 6(TCP/IPv6)启用失败！", LogLevel.Error, ex);

                        if (HandyControl.Controls.MessageBox.Show($"指定网络适配器的Internet 协议版本 6(TCP/IPv6)启用失败！请手动还原！\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo(LinksSet.当您在停止时遇到适配器设置失败或不确定该软件是否对适配器造成影响时) { UseShellExecute = true });
                        }
                    }
                }
            }
            else
            {
                // 未能获取到选中的适配器的情况
                if (!string.IsNullOrEmpty(AdaptersCombo.SelectedItem?.ToString()))
                {
                    // 适配器下拉框选中项不为空的情况

                    /** 日志信息 **/ WriteLog($"没有找到指定的网络适配器！", LogLevel.Warning);

                    if (HandyControl.Controls.MessageBox.Show($"没有找到指定的网络适配器！您可能需要手动还原。\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(LinksSet.当您在停止时遇到适配器设置失败或不确定该软件是否对适配器造成影响时) { UseShellExecute = true });
                    }
                }
                // 如果适配器下拉框选中项为空就是没选择，不需要还原适配器
            }

            /** 日志信息 **/ WriteLog("完成StopService。", LogLevel.Debug);
        }

        // 刷新状态按钮的点击事件
        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入RefreshBtn_Click。", LogLevel.Debug);

            // 更新服务的状态信息
            UpdateServiceStatus();

            /** 日志信息 **/ WriteLog("完成RefreshBtn_Click。", LogLevel.Debug);
        }

        // 启动按钮的点击事件
        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入StartBtn_Click。", LogLevel.Debug);

            // 禁用按钮，防手贱不停地启动
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = false;

            // 此时指定适配器不可以更改
            AdaptersCombo.IsEnabled = false;
            GetActiveAdapterBtn.IsEnabled = false;

            if (string.IsNullOrEmpty(AdaptersCombo.SelectedItem?.ToString()))
            {
                // 没有选择网络适配器的情况

                HandyControl.Controls.MessageBox.Show("请先在下拉框中选择当前正在使用的适配器！您可以尝试点击“自动获取”按钮。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);

                AdaptersCombo.IsEnabled = true;
            }
            else
            {
                // 从配置文件更新 Hosts
                UpdateHostsFromConfig();

                // 启动服务
                await StartService();

                /// <summary>
                /// 实验性功能：Pixiv IP优选
                /// </summary>
                if (StringBoolConverter.StringToBool(ConfigINI.INIRead("程序设置", "PixivIPPreference", PathsSet.INIPath)))
                {
                    PixivIPPreferenceBtn.Content = "Pixiv IP优选：开";
                    PixivIPPreference();
                }
                else
                {
                    PixivIPPreferenceBtn.Content = "Pixiv IP优选：关";
                    FileHelper.RemoveSection(PathsSet.SystemHosts, "s.pximg.net");
                }

                // 刷新DNS缓存
                DNS.FlushDNS();
            }

            // 重新启用按钮
            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = true;

            /** 日志信息 **/ WriteLog("完成StartBtn_Click。", LogLevel.Debug);
        }

        // 停止按钮的点击事件
        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入StopBtn_Click。", LogLevel.Debug);

            // 禁用按钮，防手贱不停地停止
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = false;

            // 移除所有条目以消除对系统的影响
            RemoveHosts();

            // 停止服务
            await StopService();

            /// <summary>
            /// 实验性功能：Pixiv IP优选
            /// </summary>
            FileHelper.RemoveSection(PathsSet.SystemHosts, "s.pximg.net");

            // 刷新DNS缓存
            DNS.FlushDNS();

            // 重新启用按钮
            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = true;

            // 此时适配器可以重新指定
            AdaptersCombo.IsEnabled = true;
            GetActiveAdapterBtn.IsEnabled = true;

            /** 日志信息 **/ WriteLog("完成StopBtn_Click。", LogLevel.Debug);
        }

        // 设置开机启动按钮的点击事件
        private void SetStartBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入SetStartBtn_Click。", LogLevel.Debug);

            try
            {
                // 使用 TaskService 来访问和操作任务计划程序
                using (TaskService ts = new TaskService())
                {
                    // 定义要创建或更新的任务名称
                    string taskName = "StartSNIBypassGUI";
                    // 尝试获取已存在的同名任务
                    Microsoft.Win32.TaskScheduler.Task existingTask = ts.GetTask(taskName);
                    // 如果任务已存在，则删除它，以便创建新的任务
                    if (existingTask != null)
                    {
                        /** 日志信息 **/ WriteLog("计划任务StartSNIBypassGUI已经存在，进行移除。", LogLevel.Warning);

                        ts.RootFolder.DeleteTask(taskName);
                    }
                    // 创建一个新的任务定义
                    TaskDefinition td = ts.NewTask();
                    // 设置任务的描述信息和作者
                    td.RegistrationInfo.Description = "开机启动 SNIBypassGUI 并自动启动服务";
                    td.RegistrationInfo.Author = "SNIBypassGUI";
                    // 创建一个登录触发器，当用户登录时触发任务
                    LogonTrigger logonTrigger = new LogonTrigger();
                    // 将登录触发器添加到任务定义中
                    td.Triggers.Add(logonTrigger);
                    // 创建一个执行操作，指定要执行的 Nginx 路径、参数和工作目录
                    ExecAction execAction = new ExecAction(PathsSet.SNIBypassGUIExeFilePath, null, null);
                    // 将执行操作添加到任务定义中
                    td.Actions.Add(execAction);

                    // 设置为管理员组
                    td.Principal.GroupId = @"BUILTIN\Administrators";
                    // 设置任务以最高权限运行
                    td.Principal.RunLevel = TaskRunLevel.Highest;
                    // 设置任务所需的安全登录方法为“组”
                    td.Principal.LogonType = TaskLogonType.Group;

                    // 在根文件夹中注册新的任务定义
                    ts.RootFolder.RegisterTaskDefinition(taskName, td);
                }

                /** 日志信息 **/ WriteLog("成功设置SNIBypassGUI为开机启动。", LogLevel.Info);

                // 显示提示信息，表示已成功设置为开机启动
                HandyControl.Controls.MessageBox.Show("成功设置SNIBypassGUI为开机启动！\r\n当开机自动启动时，将会自动在托盘图标运行并启动服务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                /** 日志信息 **/ WriteLog($"尝试设置SNIBypassGUI为开机启动时遇到异常。", LogLevel.Error,ex);

                HandyControl.Controls.MessageBox.Show($"设置开机启动时遇到异常: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            /** 日志信息 **/ WriteLog("完成SetStartBtn_Click。", LogLevel.Debug);
        }

        // 停止开机启动按钮的点击事件
        private void StopStartBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入StopStartBtn_Click。", LogLevel.Debug);

            try
            {
                // 使用 TaskService 来访问和操作任务计划程序
                using (TaskService ts = new TaskService())
                {
                    // 定义要创建或更新的任务名称
                    string taskName = "StartSNIBypassGUI";
                    // 尝试获取已存在的同名任务
                    Microsoft.Win32.TaskScheduler.Task existingTask = ts.GetTask(taskName);
                    // 如果任务已存在，则删除它
                    if (existingTask != null)
                    {
                        /** 日志信息 **/ WriteLog("计划任务StartSNIBypassGUI存在，进行移除。", LogLevel.Info);

                        ts.RootFolder.DeleteTask(taskName);
                    }
                }

                /** 日志信息 **/ WriteLog("成功停止SNIBypassGUI的开机启动。", LogLevel.Info);

                // 显示提示信息，表示已成功停止开机启动
                HandyControl.Controls.MessageBox.Show("成功停止StartSNIBypassGUI的开机启动！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                /** 日志信息 **/ WriteLog($"尝试停止SNIBypassGUI的开机启动时遇到异常。", LogLevel.Error, ex);

                // 捕获异常并显示错误信息
                HandyControl.Controls.MessageBox.Show($"停止开机启动时遇到异常: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            /** 日志信息 **/ WriteLog("完成StopStartBtn_Click。", LogLevel.Debug);
        }

        // 退出工具按钮的点击事件
        private async void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入ExitBtn_Click。", LogLevel.Debug);

            // 先隐藏窗体，在后台退出程序
            this.Hide();

            // 隐藏托盘图标
            TaskbarIcon.Visibility = Visibility.Collapsed;

            // 刷新托盘图标
            TaskBarIconHelper.RefreshNotification();

            // 移除所有条目以消除对系统的影响
            RemoveHosts();

            // 停止服务
            await StopService();

            /// <summary>
            /// 实验性功能：Pixiv IP优选
            /// </summary>
            FileHelper.RemoveSection(PathsSet.SystemHosts, "s.pximg.net");

            // 刷新DNS缓存
            DNS.FlushDNS();

            // 清空暂存数据
            ConfigINI.INIWrite("暂存数据", "PreviousDNS1", "", PathsSet.INIPath);
            ConfigINI.INIWrite("暂存数据", "PreviousDNS2", "", PathsSet.INIPath);
            ConfigINI.INIWrite("暂存数据", "IsPreviousDnsAutomatic", "True", PathsSet.INIPath);

            // 退出程序
            Environment.Exit(0);

            // 不必要的日志记录
            /** 日志信息 **/ WriteLog("完成ExitBtn_Click。", LogLevel.Debug);
        }

        // 检查更新按钮的点击事件，用于检查 SNIBypassGUI 是否有新版本可用
        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入CheckUpdateBtn_Click。", LogLevel.Debug);

            // 禁用检查更新按钮防止点来点去
            CheckUpdateBtn.IsEnabled = false;

            // 修改按钮内容以提示用户正在进行检查
            CheckUpdateBtn.Content = "检查更新中...";

            try
            {
                // 先移除在系统 hosts 中存在的 api.github.com 部分
                FileHelper.RemoveSection(PathsSet.SystemHosts, "api.github.com");

                // 确保 api.github.com 可以正常访问
                Github.EnsureGithubAPI();

                // 刷新DNS缓存
                DNS.FlushDNS();

                // 异步获取 GitHub 的最新发布信息
                string LatestReleaseInfo = await HTTPHelper.GetAsync("https://api.github.com/repos/racpast/SNIBypassGUI/releases/latest");

                /** 日志信息 **/ WriteLog($"获取到GitHub的最新发布JSON信息为{LatestReleaseInfo}", LogLevel.Debug);

                // 解析返回的JSON字符串
                JObject repodata = JObject.Parse(LatestReleaseInfo);
                JArray assets = (JArray)repodata["assets"];

                // 从解析后的JSON中获取最后一次发布的信息
                string LatestReleaseTag = repodata["tag_name"].ToString();
                string LatestReleasePublishedDt = repodata["published_at"].ToString();

                // 从解析后的JSON中获取资产信息
                string LatestReleaseDownloadLink = assets[0]["browser_download_url"].ToString();

                /** 日志信息 **/ WriteLog($"解析到最后一次发布的标签为{LatestReleaseTag}，日期为{LatestReleasePublishedDt}，下载地址为{LatestReleaseDownloadLink}。", LogLevel.Info);

                // 比较当前安装的版本与最后一次发布的版本
                if (LatestReleaseTag.ToUpper() != PresetGUIVersion)
                {
                    /** 日志信息 **/ WriteLog("检测到SNIBypassGUI有新版本可以使用。", LogLevel.Info);

                    // 修改按钮内容以提示用户正在寻找最优代理
                    CheckUpdateBtn.Content = "寻找最优代理中...";

                    // 找到最优下载加速代理地址
                    string fastestProxy = await Github.FindFastestProxy(Github.proxies, LatestReleaseDownloadLink);

                    // 拼接下载地址
                    string proxiedLatestReleaseDownloadLink = $"https://{fastestProxy}/{LatestReleaseDownloadLink}";

                    /** 日志信息 **/ WriteLog($"获取到下载加速代理链接：{proxiedLatestReleaseDownloadLink}。", LogLevel.Info);

                    if (HandyControl.Controls.MessageBox.Show($"SNIBypassGUI有新版本可用，请及时获取最新版本！\r\n点击“确定”将直接打开下载链接。\r\n版本号：{LatestReleaseTag.ToUpper()}\r\n发布时间(GMT)：{LatestReleasePublishedDt}", "检查更新", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                    {
                        // 使用 Process.Start 来打开默认浏览器并导航到链接
                        Process.Start(new ProcessStartInfo(proxiedLatestReleaseDownloadLink) { UseShellExecute = true });
                    }
                }
                else
                {
                    /** 日志信息 **/ WriteLog($"检测到SNIBypassGUI已经是最新版本。", LogLevel.Info);

                    // 如果没有新版本，则弹出提示框告知用户已是最新版本
                    HandyControl.Controls.MessageBox.Show("SNIBypassGUI目前已是最新版本！", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            // 更准确的报错信息（https://github.com/racpast/Pixiv-Nginx-GUI/issues/2）
            catch (OperationCanceledException ex)
            {
                /** 日志信息 **/ WriteLog("尝试检查更新时遇到异常。", LogLevel.Error, ex);

                // 如果获取信息请求超时或被取消，显示提示信息
                HandyControl.Controls.MessageBox.Show("获取信息操作超时或被取消！\r\n请检查是否可以正常访问api.github.com，如果可以请反馈到开发者处！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                /** 日志信息 **/ WriteLog($"尝试检查更新时遇到异常。", LogLevel.Error, ex);

                // 捕获异常并弹出错误提示框
                HandyControl.Controls.MessageBox.Show($"检查更新时遇到异常: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);

                throw ex;
            }
            finally
            {
                CheckUpdateBtn.IsEnabled = true;
                // 检查完成后，将按钮内容改回原内容
                CheckUpdateBtn.Content = "检查是否有新版本可用";               
            }

            /** 日志信息 **/ WriteLog("完成CheckUpdateBtn_Click。", LogLevel.Debug);
        }

        // 清理按钮的点击事件
        private async void CleanBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入CleanBtn_Click。", LogLevel.Debug);

            // 禁用按钮并显示提示信息
            CleanBtn.IsEnabled = false;

            // 停止定期更新大小的计时器
            _TabCUpdateTimer.Stop();

            // 使用 Process.GetProcessesByName 方法获取所有名为“SNIBypass”的进程，这将返回一个包含所有匹配进程的 Process 数组
            Process[] ps = Process.GetProcessesByName("SNIBypass");

            // 检查获取到的进程数组长度是否大于 0，如果大于 0，说明服务正在运行
            bool IsNginxRunning = ps.Length > 0;

            /** 日志信息 **/ WriteLog($"主服务运行中：{StringBoolConverter.BoolToYesNo(IsNginxRunning)}", LogLevel.Info);

            if (IsNginxRunning)
            {
                /** 日志信息 **/ WriteLog($"主服务运行中，将停止主服务。", LogLevel.Info);

                // 显示文本提示服务停止中
                ServiceStatusText.Text = "当前服务状态：\r\n主服务停止中";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);
                CleanBtn.Content = "主服务停止中";

                // 创建一个任务列表，用于存储每个杀死进程任务的任务对象
                List<Task> tasks = new List<Task>();
                // 遍历所有找到的“SNIBypass”进程
                foreach (Process process in ps)
                {
                    // 为每个进程创建一个异步任务，该任务尝试结束进程并处理可能的异常
                    Task task = Task.Run(() =>
                    {
                        try
                        {
                            // 尝试结束当前遍历到的进程
                            process.Kill();
                            // 等待进程退出，最多等待5000毫秒（5秒）
                            bool exited = process.WaitForExit(5000);
                            // 如果进程在超时时间内没有退出，则显示警告消息框
                            if (!exited)
                            {
                                /** 日志信息 **/ WriteLog($"退出进程{process.ProcessName}超时。", LogLevel.Warning);

                                HandyControl.Controls.MessageBox.Show($"退出进程{process.ProcessName}超时。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            /** 日志信息 **/ WriteLog($"尝试停止主服务时遇到异常。", LogLevel.Error, ex);

                            // 如果在结束进程的过程中发生异常，则显示错误消息框
                            HandyControl.Controls.MessageBox.Show($"停止主服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });
                    // 将创建的任务添加到任务列表中
                    tasks.Add(task);
                }
                // 等待所有结束进程的任务完成
                await Task.WhenAll(tasks);
            }

            // 检查DNS服务是否在运行
            bool IsDnsRunning = AcrylicService.AcrylicServiceIsRunning();

            /** 日志信息 **/ WriteLog($"DNS服务运行中：{StringBoolConverter.BoolToYesNo(IsDnsRunning)}", LogLevel.Info);

            if (IsDnsRunning)
            {
                /** 日志信息 **/ WriteLog($"DNS服务运行中，将停止DNS服务。", LogLevel.Info);

                // 显示文本提示服务停止中
                ServiceStatusText.Text = "当前服务状态：\r\nDNS服务停止中";
                ServiceStatusText.Foreground = new SolidColorBrush(Colors.DarkOrange);
                CleanBtn.Content = "DNS服务停止中";

                try
                {
                    await AcrylicService.StopAcrylicService();
                }
                catch (Exception ex)
                {
                    /** 日志信息 **/ WriteLog($"尝试停止DNS服务时遇到异常。", LogLevel.Error);

                    HandyControl.Controls.MessageBox.Show($"停止DNS服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            CleanBtn.Content = "服务运行日志及缓存清理中";

            // 遍历所有临时文件
            foreach (string path in PathsSet.TempFilesPaths)
            {
                if (File.Exists(path))
                {
                    // 如果文件存在则删除

                    /** 日志信息 **/ WriteLog($"临时文件{path}存在，删除。", LogLevel.Info);

                    File.Delete(path);
                }
            }

            if (File.Exists(PathsSet.GUILogPath))
            {
                // GUI调试日志存在的情况
                if (OutputLog)
                {
                    // GUI调试开启的情况

                    /** 日志信息 **/ WriteLog($"GUI调试开启，将不会删除调试日志{PathsSet.GUILogPath}。", LogLevel.Info);

                    HandyControl.Controls.MessageBox.Show("GUI调试开启时将不会删除调试日志，请尝试关闭GUI调试。","提示",MessageBoxButton.OK,MessageBoxImage.Information);
                }
                else
                {
                    // GUI调试未开启的情况

                    File.Delete(PathsSet.GUILogPath);
                }
                /** 日志信息 **/ WriteLog($"临时文件{PathsSet.GUILogPath}存在，删除。", LogLevel.Info);
            }

            /** 日志信息 **/ WriteLog($"服务运行日志及缓存清理完成。", LogLevel.Info);

            // 重新启用定期更新大小的计时器
            _TabCUpdateTimer.Start();

            // 弹出窗口提示清理完成
            HandyControl.Controls.MessageBox.Show("服务运行日志及缓存清理完成，请自行重启服务！", "清理", MessageBoxButton.OK, MessageBoxImage.Information);

            // 更新清理按钮的内容，显示所有临时文件的总大小
            CleanBtn.Content = $"清理服务运行日志及缓存 ({FileHelper.GetTotalFileSizeInMB(PathsSet.TempFilesPathsIncludingGUILog)}MB)";

            // 重新启用按钮
            CleanBtn.IsEnabled = true;

            /** 日志信息 **/ WriteLog("完成CleanBtn_Click。", LogLevel.Debug);
        }

        // 选项卡发生改变时的事件
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入TabControl_SelectionChanged。", LogLevel.Debug);

            if (sender is TabControl tabControl)
            {
                // 可以在这里安全地使用 tabControl，因为它已经被确认为非空的 TabControl 实例
                // 尝试将 TabControl 的选中项转换为 TabItem
                var selectedItem = tabControl.SelectedItem as TabItem;

                /** 日志信息 **/ WriteLog($"获取到选项卡标题：{selectedItem?.Header.ToString()}。", LogLevel.Debug);

                // 根据选中项的标题来决定执行哪个操作
                switch (selectedItem?.Header.ToString())
                {
                    // 如果选中项的标题是"日志"
                    case "设置":
                        CleanBtn.Content = $"清理服务运行日志及缓存 ({FileHelper.GetTotalFileSizeInMB(PathsSet.TempFilesPathsIncludingGUILog)}MB)";
                        SyncControlsFromConfig();
                        // 启动日志更新定时器并停止服务状态信息更新定时器
                        _TabCUpdateTimer?.Start();
                        _TabAUpdateTimer?.Stop();
                        break;
                    // 如果选中项的标题是"主页"
                    case "主页":
                        // 不 UpdateAdaptersCombo() 是因为会引发绑定异常
                        UpdateServiceStatus();
                        // 停止日志更新定时器并启动服务状态信息更新定时器
                        _TabAUpdateTimer?.Start();
                        _TabCUpdateTimer?.Stop();
                        break;
                    // 如果选中项的标题不是上述两者之一
                    default:
                        // 停止所有定时器
                        _TabCUpdateTimer?.Stop();
                        _TabAUpdateTimer?.Stop();
                        break;
                }
            }

            /** 日志信息 **/ WriteLog("完成TabControl_SelectionChanged。", LogLevel.Debug);
        }

        // 全部开启按钮的点击事件
        private void AllOnBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入AllOnBtn_Click。", LogLevel.Debug);

            bool IsChanged = false;

            // 遍历所有开关
            foreach (var pair in Switchs)
            {
                ToggleButton toggleButtonInstance = (ToggleButton)this.FindName(pair.ToggleButtonName);
                if (toggleButtonInstance != null)
                {
                    if (toggleButtonInstance.IsChecked != true)
                    {
                        toggleButtonInstance.IsChecked = true;
                        IsChanged = true;
                    }
                }
            }

            if (IsChanged)
            {
                // 有开关发生改变时才启用更改有关按钮
                ApplyBtn.IsEnabled = true;
                UnchangeBtn.IsEnabled = true;
            }

            /** 日志信息 **/ WriteLog("完成AllOnBtn_Click。", LogLevel.Debug);
        }

        // 全部关闭按钮的点击事件
        private void AllOffBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入AllOffBtn_Click。", LogLevel.Debug);

            bool IsChanged = false;

            // 遍历所有开关
            foreach (var pair in Switchs)
            {
                ToggleButton toggleButtonInstance = (ToggleButton)this.FindName(pair.ToggleButtonName);
                if (toggleButtonInstance != null)
                {
                    if (toggleButtonInstance.IsChecked != false)
                    {
                        toggleButtonInstance.IsChecked = false;
                        IsChanged = true;
                    }
                }
            }


            if (IsChanged)
            {
                // 有开关发生改变时才启用更改有关按钮
                ApplyBtn.IsEnabled = true;
                UnchangeBtn.IsEnabled = true;
            }

            /** 日志信息 **/ WriteLog("完成AllOffBtn_Click。", LogLevel.Debug);
        }

        // 安装证书按钮的点击事件
        private void InstallCertBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入InstallCertBtn_Click。", LogLevel.Debug);

            // 安装证书
            InstallCertificate();

            /** 日志信息 **/ WriteLog("完成InstallCertBtn_Click。", LogLevel.Debug);
        }

        // 自定义背景按钮的点击事件
        private void CustomBkgBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入CustomBkgBtn_Click。", LogLevel.Debug);

            // 创建OpenFileDialog实例
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "选择图片",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Filter = "图片 (*.jpg; *.jpeg; *.png)|*.jpg;*.jpeg;*.png"
            };

            // 显示对话框并检查结果
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                // 获取选中的文件路径
                string sourceFile = openFileDialog.FileName;

                /** 日志信息 **/ WriteLog($"用户在对话框中选择了{sourceFile}。", LogLevel.Info);

                try
                {
                    // 确保目标目录存在
                    if (!Directory.Exists(PathsSet.dataDirectory))
                    {
                        Directory.CreateDirectory(PathsSet.dataDirectory);
                    }

                    // 进入图像裁剪窗口
                    bool? finalresult = new ImageClippingWindow(sourceFile).ShowDialog();

                    if (finalresult == true)
                    {
                        // 写入配置文件
                        ConfigINI.INIWrite("程序设置", "Background", "Custom", PathsSet.INIPath);

                        // 更新背景图片
                        UpdateBackground();
                    }
                }
                catch (Exception ex)
                {
                    /** 日志信息 **/ WriteLog($"遇到异常。", LogLevel.Error, ex);

                    HandyControl.Controls.MessageBox.Show($"遇到异常: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            /** 日志信息 **/ WriteLog("完成CustomBkgBtn_Click。", LogLevel.Debug);
        }

        // 恢复默认背景按钮的点击事件
        private void DefaultBkgBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入DefaultBkgBtn_Click。", LogLevel.Debug);

            // 设置配置为默认背景
            ConfigINI.INIWrite("程序设置", "Background", "Preset", PathsSet.INIPath);
            // 更新背景图片
            UpdateBackground();

            /** 日志信息 **/ WriteLog("完成DefaultBkgBtn_Click。", LogLevel.Debug);
        }

        // 代理开关点击事件
        private void ToggleButtonsClick(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入ToggleButtonsClick。", LogLevel.Debug);

            // 启用更改有关按钮
            ApplyBtn.IsEnabled = true;
            UnchangeBtn.IsEnabled = true;

            /** 日志信息 **/ WriteLog("完成ToggleButtonsClick。", LogLevel.Debug);
        }

        // 链接文本点击事件
        private void LinkText_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入LinkText_PreviewMouseDown。", LogLevel.Debug);

            string url;
            if (sender is TextBlock textblock)
            {
                // 要打开的链接
                url = textblock.Text;
            }
            else if (sender is Run run)
            {
                url = run.Text;
            }
            else
            {
                /** 日志信息 **/ WriteLog("完成LinkText_PreviewMouseDown。", LogLevel.Debug);

                return;
            }
            // 检查URL是否以http://或https://开头
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                // 如果不是，则添加https://
                url = "https://" + url;
            }

            /** 日志信息 **/ WriteLog($"用户点击的链接被识别为{url}。", LogLevel.Info);

            // 使用 Process.Start 来打开默认浏览器并导航到链接
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            /** 日志信息 **/ WriteLog("完成LinkText_PreviewMouseDown。", LogLevel.Debug);
        }

        // 应用更改按钮点击事件
        private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入ApplyBtn_Click。", LogLevel.Debug);

            // 禁用有关按钮
            ApplyBtn.IsEnabled = false;
            UnchangeBtn.IsEnabled = false;
            ApplyBtn.Content = "应用更改中";

            // 有更改时只需要对DNS服务进行重启与刷新DNS缓存

            // 如果DNS服务在运行，则稍后需要重启
            bool WasDnsServiceRunning = AcrylicService.AcrylicServiceIsRunning();

            /** 日志信息 **/ WriteLog($"DNS服务运行中：{StringBoolConverter.BoolToYesNo(WasDnsServiceRunning)}", LogLevel.Info);

            try
            {
                await AcrylicService.StopAcrylicService();
            }
            catch (Exception ex)
            {
                /** 日志信息 **/ WriteLog($"尝试停止DNS服务时遇到异常。", LogLevel.Error);

                HandyControl.Controls.MessageBox.Show($"停止DNS服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // 根据开关状态更新配置文件
            UpdateConfigFromToggleButtons();

            // 再从配置文件同步到 Hosts
            UpdateHostsFromConfig();

            // 刷新DNS缓存
            DNS.FlushDNS();

            if (WasDnsServiceRunning)
            {
                // 重启服务
                await AcrylicService.StartAcrylicService();
            }

            ApplyBtn.Content = "应用更改";

            /** 日志信息 **/ WriteLog("完成ApplyBtn_Click。", LogLevel.Debug);
        }

        // 取消更改按钮点击事件
        private void UnchangeBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入UnchangeBtn_Click。", LogLevel.Debug);

            // 禁用有关按钮
            ApplyBtn.IsEnabled = false;
            UnchangeBtn.IsEnabled = false;

            // 从配置文件同步开关
            SyncControlsFromConfig();

            /** 日志信息 **/ WriteLog("完成UnchangeBtn_Click。", LogLevel.Debug);
        }

        // 菜单：显示主窗口点击事件
        private void MenuItem_ShowMainWin_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入MenuItem_ShowMainWin_Click。", LogLevel.Debug);

            // 显示并激活窗体
            this.Show();
            this.Activate();

            /** 日志信息 **/ WriteLog("完成MenuItem_ShowMainWin_Click。", LogLevel.Debug);
        }

        // 菜单：启动服务点击事件
        private void MenuItem_StartService_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入MenuItem_StartService_Click。", LogLevel.Debug);

            // 模拟点击“启动”按钮
            StartBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            // 显示通知
            TaskbarIcon.ShowBalloonTip("服务启动完成", "您现在可以尝试访问列表中的网站。", BalloonIcon.Info);

            /** 日志信息 **/ WriteLog("完成MenuItem_StartService_Click。", LogLevel.Debug);
        }

        // 菜单：停止服务点击事件
        private void MenuItem_StopService_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入MenuItem_StopService_Click。", LogLevel.Debug);

            // 模拟点击“停止”按钮
            StopBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            // 显示通知
            TaskbarIcon.ShowBalloonTip("服务停止完成", "感谢您的使用 ~\\(≥▽≤)/~", BalloonIcon.Info);

            /** 日志信息 **/ WriteLog("完成MenuItem_StopService_Click。", LogLevel.Debug);
        }

        // 菜单：退出工具点击事件
        private void MenuItem_ExitTool_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入MenuItem_ExitTool_Click。", LogLevel.Debug);

            // 模拟点击“退出工具”按钮
            ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // 不必要的日志记录
            /** 日志信息 **/ WriteLog("进入MenuItem_ExitTool_Click。", LogLevel.Debug);
        }

        // 托盘图标点击事件
        public async void TaskbarIcon_LeftClick()
        {
            /** 日志信息 **/ WriteLog("进入TaskbarIcon_LeftClick。", LogLevel.Debug);

            // 显示并激活主窗体
            this.Show();
            this.Activate();
            // 更新一言
            await UpdateYiyan();

            /** 日志信息 **/ WriteLog("完成TaskbarIcon_LeftClick。", LogLevel.Debug);
        }

        // 最小化到托盘图标运行按钮点击事件
        private void TaskbarIconRunBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入TaskbarIconRunBtn_Click。", LogLevel.Debug);

            // 隐藏窗体
            this.Hide();
            // 显示通知
            TaskbarIcon.ShowBalloonTip("已最小化运行", "点击图标显示主窗体或右键显示菜单", BalloonIcon.Info);

            /** 日志信息 **/ WriteLog("完成TaskbarIconRunBtn_Click。", LogLevel.Debug);
        }

        // 窗口加载完成事件
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入Window_Loaded。", LogLevel.Debug);

            // 将代理开关添加到列表
            AddSwitchItems();

            // 在主窗口标题显示版本
            WindowTitle.Text = "SNIBypassGUI " + PresetGUIVersion;

            // 当 _TabAUpdateTimer 到达指定的时间间隔时，将调用 TabAUpdateTimer_Tick 方法
            _TabAUpdateTimer.Tick += TabAUpdateTimer_Tick;

            // 当 _TabCUpdateTimer 到达指定的时间间隔时，将调用 TabCUpdateTimer_Tick 方法
            _TabCUpdateTimer.Tick += TabCUpdateTimer_Tick;

            // 可以避免因 MainTabControl 或其中控件尚未完全加载时访问控件而导致的 null 错误。
            // 通过这种方式，确保了事件只在控件准备好时才会被触发，避免了早期调用导致的问题。
            MainTabControl.SelectionChanged += TabControl_SelectionChanged;

            // 检查文件，当配置文件不存在时可以创建
            InitializeDirectoriesAndFiles();

            // 更新背景图像
            UpdateBackground();

            // 更新服务状态信息
            UpdateServiceStatus();

            // 更新适配器列表
            UpdateAdaptersCombo();

            // 从配置信息更新开关状态
            SyncControlsFromConfig();

            // 创建一个指向当前用户根证书存储的X509Store对象
            // StoreName.Root表示根证书存储，StoreLocation.CurrentUser表示当前用户的证书存储
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            // 以最大权限打开证书存储，以便进行添加、删除等操作
            store.Open(OpenFlags.MaxAllowed);
            // 获取证书存储中的所有证书
            X509Certificate2Collection collection = store.Certificates;
            // 在证书存储中查找具有指定指纹的证书
            // X509FindType.FindByThumbprint 表示按指纹查找，false 表示不区分大小写（对于指纹查找无效，因为指纹是唯一的）
            X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
            // 检查是否找到了具有该指纹的证书
            if (fcollection != null)
            {
                // 如果找到了证书，则检查证书的数量
                if (fcollection.Count == 0)
                {
                    /** 日志信息 **/ WriteLog($"未找到指纹为{Thumbprint}的证书，提示用户安装证书。", LogLevel.Info);

                    MessageBoxResult UserConfirm = HandyControl.Controls.MessageBox.Show("第一次使用需要安装证书，有关证书的对话框请点击“是 (Y)”。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (UserConfirm == MessageBoxResult.OK)
                    {
                        InstallCertificate();
                    }
                }
            }

            try
            {
                if (!AcrylicService.AcrylicServiceIsRunning())
                {
                    /** 日志信息 **/ WriteLog("DNS服务未在运行，将进行处理。", LogLevel.Info);

                    if (AcrylicService.AcrylicServiceIsInstalled() == false)
                    {
                        /** 日志信息 **/ WriteLog("DNS服务未安装，将尝试安装。", LogLevel.Info);

                        await AcrylicService.InstallAcrylicService();
                    }
                    else
                    {
                        /** 日志信息 **/ WriteLog("DNS服务已安装，将尝试重新安装。", LogLevel.Info);

                        await AcrylicService.UninstallAcrylicService();
                        await AcrylicService.InstallAcrylicService();
                    }
                }
            }catch (Exception ex)
            {
                /** 日志信息 **/ WriteLog($"遇到异常。", LogLevel.Error, ex);

                HandyControl.Controls.MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // 启动用于定期更新服务状态信息的定时器，
            _TabAUpdateTimer.Start();

            // 区别由服务启动与用户启动进行不同处理
            string curPath = Environment.CurrentDirectory;
            bool isRunWinService = (curPath != Path.GetDirectoryName(PathsSet.currentDirectory));
            if (isRunWinService)
            {
                /** 日志信息 **/ WriteLog("程序应为计划任务启动，将托盘运行并自动启动服务。", LogLevel.Info);

                // 如果是由服务启动，则托盘运行并自动启动服务
                TaskbarIconRunBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                MenuItem_StartService.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }

            // 更新一言
            await UpdateYiyan();

            /** 日志信息 **/ WriteLog("完成Window_Loaded。", LogLevel.Debug);
        }

        // 窗口关闭事件
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入Window_Closing。", LogLevel.Debug);

            // 取消事件
            e.Cancel = true;
            // 模拟点击“最小化到托盘图标运行”按钮
            TaskbarIconRunBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            /** 日志信息 **/ WriteLog("完成Window_Closing。", LogLevel.Debug);
        }

        // 菜单选项鼠标进入事件（用于突出显示）
        private void MenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入MenuItem_MouseEnter。", LogLevel.Debug);

            if (sender is MenuItem menuitem)
            {
                switch (menuitem.Header.ToString())
                {
                    case "显示主窗口":
                        menuitem.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00A2FF"));
                        break;
                    case "启动服务":
                        menuitem.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2BFF00"));
                        break;
                    case "停止服务":
                        menuitem.Foreground = new SolidColorBrush(Colors.Red);
                        break;
                    case "退出工具":
                        menuitem.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF00C7"));
                        break;
                    default:
                        break;
                }
                menuitem.Header = $"『{menuitem.Header}』";
                menuitem.FontSize = menuitem.FontSize + 2;
            }

            /** 日志信息 **/ WriteLog("完成MenuItem_MouseEnter。", LogLevel.Debug);
        }

        // 菜单选项鼠标离开事件（用于还原显示）
        private void MenuItem_MouseLeave(object sender, MouseEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入MenuItem_MouseLeave。", LogLevel.Debug);

            if (sender is MenuItem menuitem)
            {
                menuitem.Header = $"{menuitem.Header.ToString().Substring(1, menuitem.Header.ToString().Length - 2)}";
                menuitem.Foreground = new SolidColorBrush(Colors.White);
                menuitem.FontSize = menuitem.FontSize - 2;
            }

            /** 日志信息 **/ WriteLog("完成MenuItem_MouseLeave。", LogLevel.Debug);
        }

        // 调试模式按钮的点击事件
        private void DebugModeBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入DebugModeBtn_Click。", LogLevel.Debug);

            if (DebugModeBtn.Content.ToString() == "调试模式：\n关")
            {
                if (HandyControl.Controls.MessageBox.Show("调试模式仅供测试和开发使用，强烈建议您在没有开发者明确指示的情况下不要随意打开。\r\n是否打开调试模式？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    ConfigINI.INIWrite("高级设置", "DebugMode", "true", PathsSet.INIPath);
                }
            }
            else
            {
                ConfigINI.INIWrite("高级设置", "DebugMode", "false", PathsSet.INIPath);
                ConfigINI.INIWrite("高级设置", "GUIDebug", "false", PathsSet.INIPath);
                ConfigINI.INIWrite("高级设置", "DomainNameResolutionMethod", "DnsService", PathsSet.INIPath);
                ConfigINI.INIWrite("高级设置", "AcrylicDebug", "false", PathsSet.INIPath);
            }

            SyncControlsFromConfig();

            /** 日志信息 **/ WriteLog("完成DebugModeBtn_Click。", LogLevel.Debug);
        }

        // 域名解析模式按钮的点击事件
        private void SwitchDomainNameResolutionMethodBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入SwitchDomainNameResolutionMethodBtn_Click。", LogLevel.Debug);

            if (SwitchDomainNameResolutionMethodBtn.Content.ToString() == "域名解析：\nDNS服务")
            {
                if (HandyControl.Controls.MessageBox.Show("在DNS服务无法正常启动的情况下，系统hosts可以作为备选方案使用，\r\n但具有一定局限性（例如pixivFANBOX的作者页面需要手动向系统hosts添加记录）。\r\n是否切换域名解析模式为系统hosts？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ConfigINI.INIWrite("高级设置", "DomainNameResolutionMethod", "SystemHosts", PathsSet.INIPath);
                }
            }
            else
            {
                ConfigINI.INIWrite("高级设置", "DomainNameResolutionMethod", "DnsService", PathsSet.INIPath);
            }

            SyncControlsFromConfig();

            /** 日志信息 **/ WriteLog("完成SwitchDomainNameResolutionMethodBtn_Click。", LogLevel.Debug);
        }

        // DNS服务调试按钮的点击事件
        private void AcrylicDebugBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入AcrylicDebugBtn_Click。", LogLevel.Debug);

            if (AcrylicDebugBtn.Content.ToString() == "DNS服务调试：\n关")
            {
                if (HandyControl.Controls.MessageBox.Show("开启DNS服务调试可以诊断某些问题，重启服务后生效。\r\n请在重启直到程序出现问题后，将\\data\\dns目录下的AcrylicDebug.txt提交给开发者。\r\n是否打开DNS服务调试？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ConfigINI.INIWrite("高级设置", "AcrylicDebug", "true", PathsSet.INIPath);
                }
            }
            else
            {
                ConfigINI.INIWrite("高级设置", "AcrylicDebug", "false", PathsSet.INIPath);
            }

            SyncControlsFromConfig();

            /** 日志信息 **/ WriteLog("完成AcrylicDebugBtn_Click。", LogLevel.Debug);
        }

        // GUI调试按钮的点击事件
        private void GUIDebugBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入GUIDebugBtn_Click。", LogLevel.Debug);

            if (GUIDebugBtn.Content.ToString() == "GUI调试：\n关")
            {
                if (HandyControl.Controls.MessageBox.Show("开启GUI调试模式可以更准确地诊断问题，但生成日志会产生额外的性能开销，请在不需要时关闭。\r\n开启后将自动关闭程序，重启程序后生效。\r\n请在重启直到程序出现问题后，将\\data\\logs目录下的GUI.log提交给开发者。\r\n是否打开GUI调试模式？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ConfigINI.INIWrite("高级设置", "GUIDebug", "true", PathsSet.INIPath);
                    // 模拟点击“退出工具”按钮
                    ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            }
            else
            {
                ConfigINI.INIWrite("高级设置", "GUIDebug", "false", PathsSet.INIPath);

                SyncControlsFromConfig();
            }

            /** 日志信息 **/ WriteLog("完成GUIDebugBtn_Click。", LogLevel.Debug);
        }

        // 编辑系统hosts按钮点击事件
        private void EditHostsBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入EditHostsBtn_Click。", LogLevel.Debug);

            // 检查文件是否存在
            if (File.Exists(PathsSet.SystemHosts))
            {
                // 启动记事本并打开文件
                Process.Start("notepad.exe", PathsSet.SystemHosts);
            }
            else
            {
                /** 日志信息 **/ WriteLog("未在指定路径找到系统hosts！", LogLevel.Warning);

                HandyControl.Controls.MessageBox.Show("未在指定路径找到系统hosts！\r\n请尝试手动创建该文件。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            /** 日志信息 **/ WriteLog("完成EditHostsBtn_Click。", LogLevel.Debug);
        }

        // 还原系统hosts按钮点击事件
        private void BackHostsBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入BackHostsBtn_Click。", LogLevel.Debug);

            if (HandyControl.Controls.MessageBox.Show("还原系统hosts功能用于消除本程序对系统hosts所产生的影响。\r\n当您认为本程序（特别是历史版本）对您的系统hosts造成了不良影响时可以使用此功能。\r\n是否还原系统hosts？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (SwitchItem pair in Switchs)
                {
                    /** 日志信息 **/ WriteLog($"移除{pair.SectionName}的记录部分。", LogLevel.Info);

                    FileHelper.RemoveSection(PathsSet.SystemHosts, pair.SectionName);
                }
            }

            DNS.FlushDNS();

            /** 日志信息 **/ WriteLog("完成BackHostsBtn_Click。", LogLevel.Debug);
        }

        // 自动获取活动适配器按钮的点击事件
        private void GetActiveAdapterBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入GetActiveAdapterBtn_Click。", LogLevel.Debug);

            // 更新适配器列表
            UpdateAdaptersCombo();

            // 获取所有适配器
            List<NetworkAdapter> adapters = NetworkAdapter.GetNetworkAdapters();
            NetworkAdapter activeAdapter = null;

            // 遍历所有适配器
            foreach (var adapter in adapters)
            {
                if (adapter.NetConnectionStatus == 2)
                {
                    // 找到已经连接的适配器，记录备用并跳出循环
                    activeAdapter = adapter;
                    break;
                }
            }
            if (activeAdapter != null)
            {
                // 找到符合条件的适配器的情况
                // Cast<string>() 假定 AdaptersCombo.Items 中的所有项都是 string 类型。如果不是，可能会遇到运行时错误。
                // 使用 OfType<string>() 来安全地处理不同类型的项。OfType<string>() 会自动跳过非字符串类型的项。
                if (AdaptersCombo.Items.OfType<string>().Contains(activeAdapter.Caption))
                {
                    // SelectedItem 会确保 AdaptersCombo 正确选中与 PreviousSelectedAdapter 匹配的项。
                    // 使用 Text 设置文本会导致 AdaptersCombo 显示 PreviousSelectedAdapter，但它并不意味着该项被选中了（特别是当该项不在 Items 中时）。
                    AdaptersCombo.SelectedItem = activeAdapter.Caption;                   
                }
            }
            else
            {
                // 未能找到符合条件的适配器的情况

                /** 日志信息 **/ WriteLog($"没有找到活动且可设置的网络适配器！", LogLevel.Warning);

                if (HandyControl.Controls.MessageBox.Show($"没有找到活动且可设置的网络适配器！您可能需要手动设置。\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(LinksSet.当您找不到当前正在使用的适配器或启动时遇到适配器设置失败时) { UseShellExecute = true });
                }
            }

            /** 日志信息 **/ WriteLog("完成GetActiveAdapterBtn_Click。", LogLevel.Debug);
        }

        // 帮助按钮：如何选择适配器的点击事件
        private void HelpBtn_HowToFindActiveAdapter_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入HelpBtn_HowToFindActiveAdapter_Click。", LogLevel.Debug);

            Process.Start(new ProcessStartInfo(LinksSet.当您无法确定当前正在使用的适配器时) { UseShellExecute = true });

            /** 日志信息 **/
            WriteLog("完成HelpBtn_HowToFindActiveAdapter_Click。", LogLevel.Debug);
        }

        // 为s.pximg.net设置优选IP的方法
        private void PixivIPPreference()
        {
            /** 日志信息 **/ WriteLog("进入PixivIPPreference。", LogLevel.Debug);
            FileHelper.RemoveSection(PathsSet.SystemHosts, "s.pximg.net");
            string ip = SendPing.FindFastetsIP(pximgIP);
            if (ip != null)
            {
                string[] NewAPIRecord = new string[]
                {
                    "#\ts.pximg.net Start",
                    $"{ip}\ts.pximg.net",
                    "#\ts.pximg.net End",
                };
                FileHelper.WriteLinesToFileTop(NewAPIRecord, PathsSet.SystemHosts);
                // 刷新DNS缓存
                DNS.FlushDNS();
            }
            else
            {
                /** 日志信息 **/ WriteLog("Pixiv IP优选失败，没有找到最优IP。", LogLevel.Warning);
            }

            /** 日志信息 **/ WriteLog("完成PixivIPPreference。", LogLevel.Debug);
        }

        // Pixiv IP优选按钮的点击事件
        private void PixivIPPreferenceBtn_Click(object sender, RoutedEventArgs e)
        {
            /** 日志信息 **/ WriteLog("进入PixivIPPreferenceBtn_Click。", LogLevel.Debug);

            PixivIPPreferenceBtn.IsEnabled = false;

            if (!StringBoolConverter.StringToBool(ConfigINI.INIRead("程序设置", "PixivIPPreference", PathsSet.INIPath)))
            {
                if (HandyControl.Controls.MessageBox.Show("Pixiv IP优选是实验性功能。\r\n当您遇到服务正常运行，但打开Pixiv白屏时可以尝试使用此功能。\r\n您要打开该功能吗？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ConfigINI.INIWrite("程序设置", "PixivIPPreference", "true", PathsSet.INIPath);
                    PixivIPPreference();
                }
            }
            else
            {
                ConfigINI.INIWrite("程序设置", "PixivIPPreference", "false", PathsSet.INIPath);
                FileHelper.RemoveSection(PathsSet.SystemHosts, "s.pximg.net");
            }

            PixivIPPreferenceBtn.IsEnabled = true;

            /** 日志信息 **/ WriteLog("完成PixivIPPreferenceBtn_Click。", LogLevel.Debug);
        }

        // 托盘图标点击命令
        public class TaskbarIconLeftClickCommand : ICommand
        {
            private readonly MainWindow _mainWindow;

            public TaskbarIconLeftClickCommand(MainWindow mainWindow)
            {
                _mainWindow = mainWindow;
            }

            public event EventHandler  CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                _mainWindow.TaskbarIcon_LeftClick();
            }
        }
    }
}