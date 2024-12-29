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
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using static SNIBypassGUI.PublicHelper;
using static SNIBypassGUI.LogHelper;
using RpNet.FileHelper;
using RpNet.NetworkHelper;
using RpNet.AcrylicServiceHelper;
using RpNet.TaskBarHelper;
using Microsoft.Win32.TaskScheduler;
using Task = System.Threading.Tasks.Task;
using System.Windows.Controls.Primitives;
using System.Linq;

namespace SNIBypassGUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 定义用于更新临时文件大小和服务状态的计时器
        private readonly DispatcherTimer _TabCUpdateTimer;
        private readonly DispatcherTimer _TabAUpdateTimer;

        // 创建 TaskbarIconLeftClickCommand 实例，并将它作为 DataContext 的一部分传递。
        public TaskbarIconLeftClickCommand _TaskbarIconLeftClickCommand { get; }

        // 窗口构造函数
        public MainWindow()
        {
            OutputLog = StringBoolConverter.StringToBool(ConfigINI.INIRead("高级设置", "GUIDebug", PathsSet.INIPath));

            foreach (var headerline in GUILogHead)
            {
                WriteLog(headerline, LogLevel.None);
            }

            WriteLog("进入MainWindow。", LogLevel.Debug);

            // 你们别多开辣，会有奇怪的报错的 (╥﹏╥)
            string MName = Process.GetCurrentProcess().MainModule.ModuleName;
            string PName = Path.GetFileNameWithoutExtension(MName);
            Process[] GUIProcess = Process.GetProcessesByName(PName);
            if (GUIProcess.Length > 1)
            {
                WriteLog("检测到程序已经在运行，将退出程序。", LogLevel.Warning);

                HandyControl.Controls.MessageBox.Show("SNIBypassGUI 已经在运行！\r\n请检查是否有托盘图标！(((ﾟДﾟ;)))", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                Environment.Exit(1);
                return;
            }

            // 将 MainWindow 作为 DataContext 设置
            this.DataContext = this;
            _TaskbarIconLeftClickCommand = new TaskbarIconLeftClickCommand(this);

            InitializeComponent();

            // 创建一个新的 DispatcherTimer 实例，用于定期更新日志信息
            _TabCUpdateTimer = new DispatcherTimer
            {
                // 设置 timer 的时间间隔为每5秒触发一次
                Interval = TimeSpan.FromSeconds(5)
            };
            // 当 timer 到达指定的时间间隔时，将调用 LogUpdateTimer_Tick 方法
            _TabCUpdateTimer.Tick += TabCUpdateTimer_Tick;
            // 创建另一个新的 DispatcherTimer 实例，用于定期更新服务状态信息
            _TabAUpdateTimer = new DispatcherTimer
            {
                // 设置 timer 的时间间隔为每5秒触发一次
                Interval = TimeSpan.FromSeconds(5)
            };
            // 当 timer 到达指定的时间间隔时，将调用 TabAUpdateTimer_Tick 方法
            _TabAUpdateTimer.Tick += TabAUpdateTimer_Tick;

            // 窗口可拖动
            this.TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };

            // 刷新托盘图标，避免有多个托盘图标出现
            TaskBarIconHelper.RefreshNotification();

            WriteLog("完成MainWindow。", LogLevel.Debug);
        }

        // 临时文件大小更新计时器触发事件
        private void TabCUpdateTimer_Tick(object sender, EventArgs e)
        {
            WriteLog("进入TabCUpdateTimer_Tick。", LogLevel.Debug);

            // 更新清理日志按钮的内容，显示所有日志文件的总大小（以MB为单位）
            CleanBtn.Content = $"清理服务运行日志及缓存 ({FileHelper.GetTotalFileSizeInMB(PathsSet.TempFilesPathsIncludingGUILog)}MB)";

            SyncFromConfig();

            WriteLog("完成TabCUpdateTimer_Tick。", LogLevel.Debug);
        }

        // 更新适配器列表的方法
        private void UpdateAdaptersCombo()
        {
            string PreviousSelectedAdapter = AdaptersCombo.SelectedItem?.ToString();
            List<NetAdp> adapters = NetAdp.GetAdapters();

            // 先更新数据
            AdaptersCombo.Items.Clear();
            foreach (var adapter in adapters)
            {
                if (adapter.Name != null)
                {
                    AdaptersCombo.Items.Add(adapter.Name);
                }
            }

            // 更新选中的项
            if (AdaptersCombo.Items.Cast<string>().Contains(PreviousSelectedAdapter))
            {
                AdaptersCombo.Text = PreviousSelectedAdapter;
            }
            else
            {
                AdaptersCombo.SelectedItem = null;
            }
        }

        // 服务状态更新计时器触发事件
        private void TabAUpdateTimer_Tick(object sender, EventArgs e)
        {
            WriteLog("进入TabAUpdateTimer_Tick。", LogLevel.Debug);

            // 更新服务状态
            UpdateServiceST();

            // 更新网络适配器列表
            UpdateAdaptersCombo();

            WriteLog("完成TabAUpdateTimer_Tick。", LogLevel.Debug);
        }

        // 更新服务状态的方法
        public void UpdateServiceST()
        {
            WriteLog("进入UpdateServiceST。", LogLevel.Debug);

            if (ServiceST == null)
            {
                return;
            }

            bool IsNginxRunning;
            bool IsDnsRunning;

            // 使用 Process.GetProcessesByName 方法获取所有名为 "SNIBypass" 的进程，这将返回一个包含所有匹配进程的 Process 数组
            Process[] ps1 = Process.GetProcessesByName("SNIBypass");

            // 检查获取到的进程数组长度是否大于 0
            // 如果大于 0，说明服务正在运行
            IsNginxRunning = ps1.Length > 0;
            IsDnsRunning = AcrylicService.AcrylicServiceIsRunning();

            WriteLog($"主服务运行中：{StringBoolConverter.BoolToYesNo(IsNginxRunning)}",LogLevel.Info);
            WriteLog($"DNS服务运行中：{StringBoolConverter.BoolToYesNo(IsDnsRunning)}",LogLevel.Info);

            if (IsNginxRunning && IsDnsRunning)
            {
                ServiceST.Text = "当前服务状态：\r\n主服务和DNS服务运行中";
                TaskbarIconServiceST.Text = "主服务和DNS服务运行中";
                ServiceST.Foreground = new SolidColorBrush(Colors.ForestGreen);
                TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.ForestGreen);
                AdaptersCombo.IsEnabled = false;
                GetActiveAdapterBtn.IsEnabled = false;
            }
            else if (IsNginxRunning)
            {
                ServiceST.Text = "当前服务状态：\r\n主服务运行中，但DNS服务未运行";
                TaskbarIconServiceST.Text = "仅主服务运行中";
                ServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);
                TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);
                AdaptersCombo.IsEnabled = false;
                GetActiveAdapterBtn.IsEnabled = false;
            }
            else if (IsDnsRunning)
            {
                ServiceST.Text = "当前服务状态：\r\n主服务未运行，但DNS服务运行中";
                TaskbarIconServiceST.Text = "仅DNS服务运行中";
                ServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);
                TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);
                AdaptersCombo.IsEnabled = false;
                GetActiveAdapterBtn.IsEnabled = false;
            }
            else
            {
                ServiceST.Text = "当前服务状态：\r\n主服务与DNS服务未运行";
                TaskbarIconServiceST.Text = "主服务与DNS服务未运行";
                ServiceST.Foreground = new SolidColorBrush(Colors.Red);
                TaskbarIconServiceST.Foreground = new SolidColorBrush(Colors.Red);
                AdaptersCombo.IsEnabled = true;
                GetActiveAdapterBtn.IsEnabled = true;
            }

            WriteLog("完成UpdateServiceST。", LogLevel.Debug);
        }

        // 更新背景图片的方法
        public void UpdateBackground()
        {
            WriteLog("进入UpdateBackground。", LogLevel.Debug);

            string Background = ConfigINI.INIRead("程序设置", "Background", PathsSet.INIPath);
            if (Background == "Custom")
            {
                var CustomBkgPath = FileHelper.FindCustomBkg();
                if (CustomBkgPath != null)
                {
                    ImageBrush cbg = new ImageBrush();
                    cbg.ImageSource = FileHelper.GetImage(CustomBkgPath);
                    cbg.Stretch = Stretch.UniformToFill;
                    MainPage.Background = cbg;

                    WriteLog("完成UpdateBackground，背景图片设置为自定义。", LogLevel.Debug);

                    return;
                }
                else
                {
                    WriteLog("背景图片设置为自定义但未在指定位置找到文件，或被删除？", LogLevel.Warning);
                }
            }
            ConfigINI.INIWrite("程序设置", "Background", "Preset", PathsSet.INIPath);
            ImageBrush bg = new ImageBrush();
            bg.ImageSource = new BitmapImage(new Uri("pack://application:,,,/SNIBypassGUI;component/Resources/DefaultBkg.png"));
            bg.Stretch = Stretch.UniformToFill;
            MainPage.Background = bg;

            WriteLog("完成UpdateBackground，背景图片设置为默认。", LogLevel.Debug);
        }

        // 用于检查重要目录是否存在以及日志文件、配置文件是否创建的方法
        public void CheckFiles()
        {
            WriteLog("进入CheckFiles。", LogLevel.Debug);

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
                    WriteLog($"文件{pair.Key}不存在，释放。", LogLevel.Info);

                    FileHelper.ExtractNormalFileInResx(pair.Value, pair.Key);
                }
            }

            // 如果配置文件不存在，则创建配置文件
            if (!File.Exists(PathsSet.INIPath))
            {
                WriteLog($"配置文件{PathsSet.INIPath}不存在，创建。", LogLevel.Info);

                ConfigINI.INIWrite("程序设置", "IsFirst", "true", PathsSet.INIPath);
                ConfigINI.INIWrite("程序设置", "Background", "Preset", PathsSet.INIPath);
                ConfigINI.INIWrite("程序设置", "ActiveAdapter", "", PathsSet.INIPath);
                ConfigINI.INIWrite("高级设置", "DebugMode", "false",PathsSet.INIPath);
                ConfigINI.INIWrite("高级设置", "GUIDebug", "false", PathsSet.INIPath);
                ConfigINI.INIWrite("高级设置", "DomainNameResolutionMethod", "DnsService", PathsSet.INIPath);
                ConfigINI.INIWrite("高级设置", "AcrylicDebug", "false", PathsSet.INIPath);
                ConfigINI.INIWrite("暂存数据", "PreviousDNS1", "", PathsSet.INIPath);
                ConfigINI.INIWrite("暂存数据", "PreviousDNS2", "", PathsSet.INIPath);
                ConfigINI.INIWrite("暂存数据", "IsPreviousDnsAutomatic", "true", PathsSet.INIPath);
                foreach (var configkeyname in SectionNamesSet)
                {
                    ConfigINI.INIWrite("代理开关", configkeyname, "true", PathsSet.INIPath);
                }
            }

            WriteLog("完成CheckFiles。", LogLevel.Debug);
        }

        // 更新一言的方法
        public async Task UpdateYiyan()
        {
            WriteLog("进入UpdateYiyan。", LogLevel.Debug);

            try
            {
                string YiyanJson = await HTTPHelper.GetAsync("https://v1.hitokoto.cn/?c=d");

                WriteLog($"获取到一言的数据为{YiyanJson}。", LogLevel.Info);

                // 将返回的JSON字符串解析为JObject
                JObject repodata = JObject.Parse(YiyanJson);
                string Hitokoto = repodata["hitokoto"].ToString();
                string From = repodata["from"].ToString();
                string FromWho = repodata["from_who"].ToString();
                TaskbarIconYiyan.Text = Hitokoto;
                TaskbarIconYiyanFrom.Text = $"—— {FromWho}「{From}」";

                WriteLog($"解析到hitokoto为{Hitokoto}，from为{From}，from_who为{FromWho}。", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常，将设置为默认一言。", LogLevel.Error,ex);

                TaskbarIconYiyan.Text = PresetYiyan;
                TaskbarIconYiyanFrom.Text = PresetYiyanForm;
            }

            WriteLog("完成UpdateYiyan。", LogLevel.Debug);
        }

        // 从配置文件处理 Hosts，会先移除所有已经存在的块再根据配置文件添加
        public void UpdateHosts()
        {
            WriteLog("进入UpdateHosts。", LogLevel.Debug);

            if (ConfigINI.INIRead("高级设置", "DomainNameResolutionMethod", PathsSet.INIPath) == "DnsService")
            {
                WriteLog($"当前域名解析模式为DNS服务，将更新{PathsSet.AcrylicHostsPath}。", LogLevel.Info);

                RemoveHosts();

                foreach (var sectionname in SectionNamesSet)
                {
                    if (StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", sectionname, PathsSet.INIPath)) == true)
                    {
                        WriteLog($"{sectionname}的代理开关为开启，将添加记录。", LogLevel.Info);

                        FileHelper.WriteLinesToFile(SectionNameToHostsRecordDic[sectionname], PathsSet.AcrylicHostsPath);
                    }
                }
            }
            else
            {
                WriteLog($"当前域名解析模式不为DNS服务，将更新{PathsSet.SystemHosts}。", LogLevel.Info);

                RemoveHosts();

                foreach (var sectionname in SectionNamesSet)
                {
                    if (StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", sectionname, PathsSet.INIPath)) == true)
                    {
                        WriteLog($"{sectionname}的代理开关为开启，将添加记录。", LogLevel.Info);

                        FileHelper.WriteLinesToFile(SectionNameToOldHostsRecordDic[sectionname], PathsSet.SystemHosts);
                    }
                }
            }

            WriteLog("完成UpdateHosts。", LogLevel.Debug);
        }

        // 移除全部 Hosts 记录
        public void RemoveHosts()
        {
            WriteLog("进入RemoveHosts。", LogLevel.Debug);

            if (ConfigINI.INIRead("高级设置", "DomainNameResolutionMethod", PathsSet.INIPath) == "DnsService")
            {
                WriteLog($"当前域名解析模式为DNS服务，将更新{PathsSet.AcrylicHostsPath}。", LogLevel.Info);

                foreach (var sectionname in SectionNamesSet)
                {
                    WriteLog($"移除{sectionname}的记录部分。", LogLevel.Info);

                    FileHelper.RemoveSection(PathsSet.AcrylicHostsPath,sectionname);
                }
            }
            else
            {
                WriteLog($"当前域名解析模式不为DNS服务，将更新{PathsSet.SystemHosts}。", LogLevel.Info);

                foreach (var sectionname in SectionNamesSet)
                {
                    WriteLog($"移除{sectionname}的记录部分。", LogLevel.Info);

                    FileHelper.RemoveSection(PathsSet.SystemHosts, sectionname);
                }
            }

            WriteLog("完成RemoveHosts。", LogLevel.Debug);
        }

        // 从配置文件同步开关状态
        public void SyncFromConfig()
        {
            WriteLog("进入SyncFromConfig。", LogLevel.Debug);

            // 更新代理开关
            foreach (var togglebutton in ToggleButtonToSectionNamedDic)
            {
                bool ShouldChecked = StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", togglebutton.Value, PathsSet.INIPath));

                WriteLog($"开关{togglebutton.Key.Name}从配置键{togglebutton.Value}同步状态：{StringBoolConverter.BoolToYesNo(ShouldChecked)}。", LogLevel.Debug);

                togglebutton.Key.IsChecked = ShouldChecked;
            }

            // 更新调试开关

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
                SwitchDomainNameResolutionMethodBtn.Content = "域名解析模式：\nDNS服务";
            }
            else
            {
                SwitchDomainNameResolutionMethodBtn.Content = "域名解析模式：\n系统hosts";
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

            string activeAdapter = ConfigINI.INIRead("程序设置", "ActiveAdapter", PathsSet.INIPath);
            if (AdaptersCombo.Items.Cast<string>().Contains(activeAdapter))
            {
                AdaptersCombo.Text = activeAdapter;
            }
            else
            {
                AdaptersCombo.SelectedItem = null;
            }

            WriteLog("完成SyncFromConfig。", LogLevel.Debug);
        }

        // 从开关列表向配置文件同步
        public void UpdateConfigINI()
        {
            WriteLog("进入UpdateConfigINI。", LogLevel.Debug);

            foreach (var togglebutton in ToggleButtonToSectionNamedDic)
            {
                if (togglebutton.Key.IsChecked == true)
                {

                    ConfigINI.INIWrite("代理开关", togglebutton.Value, "true", PathsSet.INIPath);
                }
                else
                {
                    ConfigINI.INIWrite("代理开关", togglebutton.Value, "false", PathsSet.INIPath);
                }

                WriteLog($"配置键{togglebutton.Value}从开关{togglebutton.Key.Name}同步状态：{StringBoolConverter.BoolToYesNo(togglebutton.Key.IsChecked)}。", LogLevel.Debug);
            }

            WriteLog("完成UpdateConfigINI。", LogLevel.Debug);
        }

        // 将活动网络适配器首选DNS设置为127.0.0.1并记录先前的为有效的IPv4地址且非127.0.0.1的地址到配置
        public bool SetLocalDNS(NetAdp activeAdapter)
        {
            WriteLog("进入SetLocalDNS。", LogLevel.Debug);

            try
            {
                if (activeAdapter.DNS != null)
                {
                    WriteLog($"获取到可设置的网络适配器：{activeAdapter.Name}，开始配置。", LogLevel.Info);

                    bool IsDnsAutomatic = activeAdapter.IsDnsAutomatic;// 应该在设置DNS之前记录是否为自动获取
                    string PreviousDNS1 = null;
                    string PreviousDNS2 = null;

                    if (activeAdapter.DNS[0]?.ToString() != "127.0.0.1")
                    {
                        if (activeAdapter.DNS.Length == 0)
                        {
                            PreviousDNS1 = "";
                            PreviousDNS2 = "";
                        }
                        else if (activeAdapter.DNS.Length == 1)
                        {
                            if (IPv4Validator.IsValidIPv4(activeAdapter.DNS[0]))
                            {
                                PreviousDNS1 = activeAdapter.DNS[0];
                                PreviousDNS2 = "";
                            }
                            else
                            {
                                PreviousDNS1 = "";
                                PreviousDNS2 = "";
                            }
                        }
                        else if (activeAdapter.DNS.Length == 2)
                        {
                            if (IPv4Validator.IsValidIPv4(activeAdapter.DNS[0]))
                            {
                                PreviousDNS1 = activeAdapter.DNS[0];

                                if (IPv4Validator.IsValidIPv4(activeAdapter.DNS[1]))
                                {
                                    if (activeAdapter.DNS[1] == "127.0.0.1")
                                    {
                                        PreviousDNS2 = "";
                                    }
                                    else
                                    {
                                        PreviousDNS2 = activeAdapter.DNS[1];
                                    }
                                }
                                else
                                {
                                    PreviousDNS2 = "";
                                }
                            }
                            else
                            {
                                PreviousDNS2 = "";

                                if (IPv4Validator.IsValidIPv4(activeAdapter.DNS[1]))
                                {
                                    if (activeAdapter.DNS[1] == "127.0.0.1")
                                    {
                                        PreviousDNS1 = "";
                                    }
                                    else
                                    {
                                        PreviousDNS1 = activeAdapter.DNS[1];
                                    }
                                }
                                else
                                {
                                    PreviousDNS1 = "";
                                }
                            }
                        }

                        activeAdapter.DNS = new string[] { "127.0.0.1" };

                        activeAdapter.Fresh();

                        WriteLog($"指定网络适配器是否为自动获取DNS：{StringBoolConverter.BoolToYesNo(IsDnsAutomatic)}", LogLevel.Info);
                        WriteLog($"指定网络适配器的DNS成功设置为首选{activeAdapter.DNS[0]}", LogLevel.Info);
                        WriteLog($"将暂存的DNS为：{PreviousDNS1}，{PreviousDNS2}", LogLevel.Debug);

                        ConfigINI.INIWrite("暂存数据", "PreviousDNS1", PreviousDNS1, PathsSet.INIPath);
                        ConfigINI.INIWrite("暂存数据", "PreviousDNS2", PreviousDNS2, PathsSet.INIPath);
                        ConfigINI.INIWrite("暂存数据", "IsPreviousDnsAutomatic", IsDnsAutomatic.ToString(), PathsSet.INIPath);
                    }

                    WriteLog("完成SetLocalDNS，返回true。", LogLevel.Debug);

                    return true;
                }
                else
                {
                    WriteLog($"无法设置指定的网络适配器，因为DNS为null！", LogLevel.Warning);

                    if (HandyControl.Controls.MessageBox.Show($"无法设置指定的网络适配器！请手动设置！\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        VideoHelpWindow videoHelpWindow = new VideoHelpWindow("如何手动设置适配器", PathsSet.HelpVideo_如何手动设置适配器_Path);
                        videoHelpWindow.Show();
                    }

                    WriteLog("完成SetLocalDNS，返回false。", LogLevel.Debug);

                    return false;
                }
            }
            catch (NetAdpSetException ex)
            {
                WriteLog($"设置活动网络适配器时发生错误。", LogLevel.Error, ex);

                HandyControl.Controls.MessageBox.Show($"设置活动网络适配器时发生错误：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常。", LogLevel.Error, ex);

                HandyControl.Controls.MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成SetLocalDNS，返回false。", LogLevel.Debug);

            return false;
        }

        // 启动所有服务
        public async Task StartService()
        {
            WriteLog("进入StartService。", LogLevel.Debug);

            if (ConfigINI.INIRead("高级设置", "DomainNameResolutionMethod", PathsSet.INIPath) == "DnsService")
            {
                // 使用 Process.GetProcessesByName 方法获取所有名为 "SNIBypass" 的进程，这将返回一个包含所有匹配进程的 Process 数组
                Process[] ps1 = Process.GetProcessesByName("SNIBypass");

                // 检查获取到的进程数组长度是否大于 0
                // 如果大于 0，说明服务正在运行
                bool IsNginxRunning = ps1.Length > 0;

                WriteLog($"主服务运行中：{StringBoolConverter.BoolToYesNo(IsNginxRunning)}", LogLevel.Info);

                if (ps1.Length <= 0)
                {
                    WriteLog($"主服务未运行，将启动主服务。", LogLevel.Info);

                    ServiceST.Text = "当前服务状态：\r\n主服务启动中";
                    ServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);

                    // 创建一个新的Process对象，用于启动外部进程
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
                        WriteLog($"尝试启动主服务时遇到异常。", LogLevel.Error, ex);

                        // 如果启动进程时发生异常，则显示一个错误消息框
                        HandyControl.Controls.MessageBox.Show($"无法启动主服务: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // 更新服务的状态信息
                UpdateServiceST();

                bool IsDnsRunning = AcrylicService.AcrylicServiceIsRunning();

                WriteLog($"DNS服务运行中：{StringBoolConverter.BoolToYesNo(IsDnsRunning)}", LogLevel.Info);

                if (!IsDnsRunning)
                {
                    WriteLog($"DNS服务未运行，将启动DNS服务。", LogLevel.Info);

                    ServiceST.Text = "当前服务状态：\r\nDNS服务启动中";
                    ServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);
                    try
                    {
                        await AcrylicService.StartAcrylicService();
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"尝试启动DNS服务时遇到异常。", LogLevel.Error, ex);

                        HandyControl.Controls.MessageBox.Show($"无法启动DNS服务:{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                UpdateServiceST();

                List<NetAdp> adapters = NetAdp.GetAdapters();
                NetAdp activeAdapter = null;
                foreach (var adapter in adapters)
                {
                    if (adapter.Name == AdaptersCombo.SelectedItem?.ToString())
                    {
                        activeAdapter = adapter;
                        break;
                    }
                }
                if (activeAdapter != null)
                {
                    if (SetLocalDNS(activeAdapter))
                    {
                        WriteLog($"指定网络适配器为：{activeAdapter.Name}", LogLevel.Info);

                        ConfigINI.INIWrite("程序设置", "ActiveAdapter", activeAdapter.Name, PathsSet.INIPath);

                        if (!activeAdapter.DisableIPv6())
                        {
                            WriteLog($"指定网络适配器的Internet 协议版本 6(TCP/IPv6)禁用失败！", LogLevel.Warning);

                            if (HandyControl.Controls.MessageBox.Show($"指定网络适配器的Internet 协议版本 6(TCP/IPv6)禁用失败！请手动设置！\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                            {
                                VideoHelpWindow videoHelpWindow = new VideoHelpWindow("如何手动设置适配器", PathsSet.HelpVideo_如何手动设置适配器_Path);
                                videoHelpWindow.Show();
                            }
                        }

                    }
                }
                else
                {
                    WriteLog($"没有找到指定的网络适配器！", LogLevel.Warning);

                    if (HandyControl.Controls.MessageBox.Show($"没有找到指定的网络适配器！您可能需要手动设置。\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        VideoHelpWindow videoHelpWindow = new VideoHelpWindow("如何手动设置适配器", PathsSet.HelpVideo_如何手动设置适配器_Path);
                        videoHelpWindow.Show();
                    }
                }
            }
            else
            {
                // 使用 Process.GetProcessesByName 方法获取所有名为 "SNIBypass" 的进程，这将返回一个包含所有匹配进程的 Process 数组
                Process[] ps = Process.GetProcessesByName("SNIBypass");

                bool IsNginxRunning = ps.Length > 0;

                WriteLog($"主服务运行中：{StringBoolConverter.BoolToYesNo(IsNginxRunning)}", LogLevel.Info);

                if (ps.Length <= 0)
                {
                    WriteLog($"主服务未运行，将启动主服务。", LogLevel.Info);

                    ServiceST.Text = "当前服务状态：\r\n主服务启动中";
                    ServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);

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
                        WriteLog($"尝试启动主服务时遇到异常。", LogLevel.Error, ex);

                        HandyControl.Controls.MessageBox.Show($"无法启动主服务: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                UpdateServiceST();
            }

            WriteLog("完成StartService。", LogLevel.Debug);
        }

        public bool UnsetLocalDNS(NetAdp activeAdapter)
        {
            WriteLog("进入UnsetLocalDNS。", LogLevel.Debug);

            if (activeAdapter.DNS != null)
            {
                if (activeAdapter.DNS.Length > 0)
                {
                    // 如果适配器的DNS长度大于0的情况，一般不会发生为0的情况因为已经设置过了
                    if (activeAdapter.DNS[0] == "127.0.0.1")
                    {
                        // 如果适配器的首选DNS为127.0.0.1的情况，需要更改回去
                        if (StringBoolConverter.StringToBool(ConfigINI.INIRead("暂存数据", "IsPreviousDnsAutomatic", PathsSet.INIPath)))
                        {
                            // 之前是自动获取的情况
                            activeAdapter.DNS = null;

                            WriteLog($"活动网络适配器的DNS成功设置为自动获取。", LogLevel.Info);
                        }
                        else
                        {
                            // 之前不是自动获取的情况，按照暂存数据设置回去
                            if (string.IsNullOrEmpty(ConfigINI.INIRead("暂存数据", "PreviousDNS1", PathsSet.INIPath)))
                            {
                                activeAdapter.DNS = null;

                                WriteLog($"指定网络适配器的DNS成功设置为自动获取。", LogLevel.Info);
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(ConfigINI.INIRead("暂存数据", "PreviousDNS2", PathsSet.INIPath)))
                                {
                                    activeAdapter.DNS = new string[] { ConfigINI.INIRead("暂存数据", "PreviousDNS1", PathsSet.INIPath) };

                                    WriteLog($"指定网络适配器的DNS成功设置为首选{ConfigINI.INIRead("暂存数据", "PreviousDNS1", PathsSet.INIPath)}。", LogLevel.Info);
                                }
                                else
                                {
                                    activeAdapter.DNS = new string[] { ConfigINI.INIRead("暂存数据", "PreviousDNS1", PathsSet.INIPath), ConfigINI.INIRead("暂存数据", "PreviousDNS2", PathsSet.INIPath) };

                                    WriteLog($"指定网络适配器的DNS成功设置为首选{ConfigINI.INIRead("暂存数据", "PreviousDNS1", PathsSet.INIPath)}，备用{ConfigINI.INIRead("暂存数据", "PreviousDNS2", PathsSet.INIPath)}。", LogLevel.Info);
                                }
                            }
                        }
                    }
                }

                WriteLog("完成UnsetLocalDNS，返回true。", LogLevel.Debug);

                return true;
            }
            else
            {
                WriteLog($"无法设置指定的网络适配器，因为DNS为null！", LogLevel.Warning);

                if (HandyControl.Controls.MessageBox.Show($"无法还原指定的网络适配器！请手动还原！\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    VideoHelpWindow videoHelpWindow = new VideoHelpWindow("如何手动还原适配器", PathsSet.HelpVideo_如何手动还原适配器_Path);
                    videoHelpWindow.Show();
                }

                WriteLog("完成UnsetLocalDNS，返回false", LogLevel.Debug);

                return false;
            }
        }

        // 停止所有服务
        public async Task StopService()
        {
            WriteLog("进入StopService。", LogLevel.Debug);

            // 使用 Process.GetProcessesByName 方法获取所有名为 "SNIBypass" 的进程，这将返回一个包含所有匹配进程的 Process 数组
            Process[] ps1 = Process.GetProcessesByName("SNIBypass");

            // 检查获取到的进程数组长度是否大于 0
            // 如果大于 0，说明服务正在运行
            bool IsNginxRunning = ps1.Length > 0;

            WriteLog($"主服务运行中：{StringBoolConverter.BoolToYesNo(IsNginxRunning)}", LogLevel.Info);

            if (IsNginxRunning)
            {
                WriteLog($"主服务运行中，将停止主服务。", LogLevel.Info);

                ServiceST.Text = "当前服务状态：\r\n主服务停止中";
                ServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);

                // 创建一个任务列表，用于存储每个杀死进程任务的任务对象
                List<Task> tasks = new List<Task>();
                // 遍历所有找到的 "SNIBypass" 进程
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
                                WriteLog($"进程{process.ProcessName}退出超时。", LogLevel.Warning);

                                HandyControl.Controls.MessageBox.Show($"进程{process.ProcessName}退出超时。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        catch (Exception ex)
                        {
                            WriteLog($"尝试停止主服务时遇到异常。", LogLevel.Error, ex);

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
            UpdateServiceST();

            bool IsDnsRunning = AcrylicService.AcrylicServiceIsRunning();

            WriteLog($"DNS服务运行中：{StringBoolConverter.BoolToYesNo(IsDnsRunning)}", LogLevel.Info);

            if (IsDnsRunning)
            {
                WriteLog($"DNS服务运行中，将停止DNS服务。", LogLevel.Info);

                ServiceST.Text = "当前服务状态：\r\nDNS服务停止中";
                ServiceST.Foreground = new SolidColorBrush(Colors.DarkOrange);
                try
                {
                    await AcrylicService.StopAcrylicService();
                }
                catch (Exception ex)
                {
                    WriteLog($"尝试停止DNS服务时遇到异常。", LogLevel.Error);

                    HandyControl.Controls.MessageBox.Show($"停止DNS服务时遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            UpdateServiceST();

            List<NetAdp> adapters = NetAdp.GetAdapters();
            NetAdp activeAdapter = null;
            if (string.IsNullOrEmpty(AdaptersCombo.SelectedItem?.ToString()))
            {
                return;
            }
            foreach (var adapter in adapters)
            {
                if (adapter.Name == AdaptersCombo.SelectedItem?.ToString())
                {
                    activeAdapter = adapter;
                    break;
                }
            }
            if (activeAdapter != null)
            {
                if (UnsetLocalDNS(activeAdapter))
                {
                    if (!activeAdapter.EnableIPv6())
                    {

                        WriteLog($"指定网络适配器的Internet 协议版本 6(TCP/IPv6)启用失败！", LogLevel.Warning);

                        if (HandyControl.Controls.MessageBox.Show($"指定网络适配器的Internet 协议版本 6(TCP/IPv6)启用失败！请手动还原！\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                        {
                            VideoHelpWindow videoHelpWindow = new VideoHelpWindow("如何手动还原适配器", PathsSet.HelpVideo_如何手动还原适配器_Path);
                            videoHelpWindow.Show();
                        }
                    }
                }
            }
            else
            {
                WriteLog($"没有找到指定的网络适配器！", LogLevel.Warning);

                if (HandyControl.Controls.MessageBox.Show($"没有找到指定的网络适配器！您可能需要手动还原。\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    VideoHelpWindow videoHelpWindow = new VideoHelpWindow("如何手动还原适配器", PathsSet.HelpVideo_如何手动还原适配器_Path);
                    videoHelpWindow.Show();
                }
            }

            WriteLog("完成StopService。", LogLevel.Debug);
        }

        // 刷新状态按钮的点击事件
        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入RefreshBtn_Click。", LogLevel.Debug);

            // 更新服务的状态信息
            UpdateServiceST();

            WriteLog("完成RefreshBtn_Click。", LogLevel.Debug);
        }

        // 启动按钮的点击事件
        private async void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入StartBtn_Click。", LogLevel.Debug);

            // 禁用防手贱重复启动
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = false;
            AdaptersCombo.IsEnabled = false;
            GetActiveAdapterBtn.IsEnabled = false;

            if (AdaptersCombo.Text == "")
            {
                HandyControl.Controls.MessageBox.Show("请先在下拉框中选择当前正在使用的适配器！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);

                AdaptersCombo.IsEnabled = true;
            }
            else
            {
                // 从配置文件修改更新 Hosts
                UpdateHosts();

                await StartService();

                // 刷新 DNS缓存
                DNS.FlushDNS();
            }

            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = true;

            WriteLog("完成StartBtn_Click。", LogLevel.Debug);
        }

        // 停止按钮的点击事件
        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入StopBtn_Click。", LogLevel.Debug);

            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = false;

            RemoveHosts();

            await StopService();

            DNS.FlushDNS();

            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = true;

            AdaptersCombo.IsEnabled = true;
            GetActiveAdapterBtn.IsEnabled = true;

            WriteLog("完成StopBtn_Click。", LogLevel.Debug);
        }

        // 设置开机启动按钮的点击事件
        private void SetStartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入SetStartBtn_Click。", LogLevel.Debug);

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
                        WriteLog("计划任务StartSNIBypassGUI已经存在，进行移除。", LogLevel.Warning);

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

                    // 设置任务的安全选项
                    td.Principal.GroupId = @"BUILTIN\Administrators"; // 设置为管理员组
                    td.Principal.RunLevel = TaskRunLevel.Highest;  // 设置任务以最高权限运行
                    td.Principal.LogonType = TaskLogonType.Group;

                    // 在根文件夹中注册新的任务定义
                    ts.RootFolder.RegisterTaskDefinition(taskName, td);
                }

                WriteLog("成功设置SNIBypassGUI为开机启动。", LogLevel.Info);

                // 显示提示信息，表示已成功设置为开机启动
                HandyControl.Controls.MessageBox.Show("成功设置SNIBypassGUI为开机启动！\r\n当开机自动启动时，将会自动在托盘图标运行并启动服务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteLog($"尝试设置SNIBypassGUI为开机启动时遇到异常。", LogLevel.Error,ex);

                HandyControl.Controls.MessageBox.Show($"设置开机启动时遇到异常: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成SetStartBtn_Click。", LogLevel.Debug);
        }

        // 停止开机启动按钮的点击事件
        private void DelStartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入DelStartBtn_Click。", LogLevel.Debug);

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
                        WriteLog("计划任务StartSNIBypassGUI存在，进行移除。", LogLevel.Info);

                        ts.RootFolder.DeleteTask(taskName);
                    }
                }

                WriteLog("成功停止SNIBypassGUI的开机启动。", LogLevel.Info);

                // 显示提示信息，表示已成功停止开机启动
                HandyControl.Controls.MessageBox.Show("成功停止StartSNIBypassGUI的开机启动！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteLog($"尝试停止SNIBypassGUI的开机启动时遇到异常。", LogLevel.Error, ex);

                // 捕获异常并显示错误信息
                HandyControl.Controls.MessageBox.Show($"停止开机启动时遇到异常: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成DelStartBtn_Click。", LogLevel.Debug);
        }

        // 退出工具按钮的点击事件
        private async void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ExitBtn_Click。", LogLevel.Debug);

            this.Hide();

            RemoveHosts();

            await StopService();

            ConfigINI.INIWrite("暂存数据", "PreviousDNS1", "", PathsSet.INIPath);
            ConfigINI.INIWrite("暂存数据", "PreviousDNS2", "", PathsSet.INIPath);
            ConfigINI.INIWrite("暂存数据", "IsPreviousDnsAutomatic", "True", PathsSet.INIPath);

            DNS.FlushDNS();

            TaskbarIcon.Visibility = Visibility.Collapsed;

            // 退出程序
            Environment.Exit(0);

            // 不必要的日志记录
            WriteLog("完成ExitBtn_Click。", LogLevel.Debug);
        }

        // 检查更新按钮的点击事件，用于检查 SNIBypassGUI 是否有新版本可用
        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入CheckUpdateBtn_Click。", LogLevel.Debug);

            CheckUpdateBtn.IsEnabled = false;
            // 修改按钮内容以提示用户正在进行检查
            CheckUpdateBtn.Content = "检查更新中...";

            try
            {
                FileHelper.RemoveSection(PathsSet.SystemHosts, "api.github.com");

                Github.EnsureGithubAPI();

                // 异步获取 GitHub 的最新发布信息
                string LatestReleaseInfo = await HTTPHelper.GetAsync("https://api.github.com/repos/racpast/SNIBypassGUI/releases/latest");

                WriteLog($"获取到GitHub的最新发布JSON信息为{LatestReleaseInfo}", LogLevel.Info);

                // 将返回的JSON字符串解析为JObject
                JObject repodata = JObject.Parse(LatestReleaseInfo);
                // 从解析后的JSON中获取最后一次发布的信息
                string LatestReleaseTag = repodata["tag_name"].ToString();
                string LatestReleasePublishedDt = repodata["published_at"].ToString();
                // 从解析后的JSON中获取资产信息
                JArray assets = (JArray)repodata["assets"];
                string LatestReleaseDownloadLink = assets[0]["browser_download_url"].ToString();

                WriteLog($"解析到最后一次发布的标签为{LatestReleaseTag}，日期为{LatestReleasePublishedDt}，下载地址为{LatestReleaseDownloadLink}", LogLevel.Debug);

                // 比较当前安装的版本与最后一次发布的版本
                if (LatestReleaseTag.ToUpper() != PresetGUIVersion)
                {
                    WriteLog("检测到SNIBypassGUI有新版本可以使用。", LogLevel.Info);

                    // 拼接下载地址
                    string proxiedLatestReleaseDownloadLink = $"https://{await Github.FindFastestProxy(Github.proxies, LatestReleaseDownloadLink)}/{LatestReleaseDownloadLink}";

                    WriteLog($"获取到下载加速代理链接：{proxiedLatestReleaseDownloadLink}。", LogLevel.Info);

                    if (HandyControl.Controls.MessageBox.Show($"SNIBypassGUI有新版本可用，请及时获取最新版本！\r\n点击“确定”将直接打开下载链接。\r\n版本号：{LatestReleaseTag.ToUpper()}\r\n发布时间(GMT)：{LatestReleasePublishedDt}", "检查更新", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                    {
                        // 使用 Process.Start 来打开默认浏览器并导航到链接
                        Process.Start(new ProcessStartInfo(proxiedLatestReleaseDownloadLink) { UseShellExecute = true });
                    }
                }
                else
                {
                    WriteLog($"检测到SNIBypassGUI已经是最新版本。", LogLevel.Info);

                    // 如果没有新版本，则弹出提示框告知用户已是最新版本
                    HandyControl.Controls.MessageBox.Show("SNIBypassGUI目前已是最新版本！", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            // 更准确的报错信息（https://github.com/racpast/Pixiv-Nginx-GUI/issues/2）
            catch (OperationCanceledException ex)
            {
                WriteLog("尝试检查更新时遇到异常。", LogLevel.Error, ex);

                // 如果获取信息请求超时，显示提示信息
                HandyControl.Controls.MessageBox.Show("获取信息操作超时或被取消！\r\n请检查是否可以访问到api.github.com，或反馈给开发者！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                WriteLog($"尝试检查更新时遇到异常。", LogLevel.Error, ex);

                // 捕获异常并弹出错误提示框
                HandyControl.Controls.MessageBox.Show($"检查更新时遇到异常: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CheckUpdateBtn.IsEnabled = true;
                // 检查完成后，将按钮内容改回原内容
                CheckUpdateBtn.Content = "检查 SNIBypassGUI 是否有新版本可用";               
            }

            WriteLog("完成CheckUpdateBtn_Click。", LogLevel.Debug);
        }

        // 清理按钮的点击事件
        private async void CleanBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入CleanBtn_Click。", LogLevel.Debug);

            CleanBtn.IsEnabled = false;
            CleanBtn.Content = "服务运行日志及缓存清理中";

            // 停止定期更新大小的计时器
            _TabCUpdateTimer.Stop();

            bool NeedRestart = false;

            // 使用 Process.GetProcessesByName 方法获取所有名为 "SNIBypass" 的进程，这将返回一个包含所有匹配进程的 Process 数组
            Process[] ps = Process.GetProcessesByName("SNIBypass");

            bool IsNginxRunning;

            // 检查获取到的进程数组长度是否大于 0
            // 如果大于 0，说明服务正在运行
            IsNginxRunning = ps.Length > 0;

            WriteLog($"主服务运行中：{StringBoolConverter.BoolToYesNo(IsNginxRunning)}", LogLevel.Info);

            if (IsNginxRunning)
            {
                WriteLog($"主服务运行中，稍后需要重新启动。", LogLevel.Info);

                // 如果主服务在运行，则稍后需要重启
                NeedRestart = true;
            }

            // 先关停所有服务
            await StopService();

            // 删除所有临时文件
            foreach (string path in PathsSet.TempFilesPaths)
            {
                if (File.Exists(path))
                {
                    WriteLog($"临时文件{path}存在，删除。", LogLevel.Info);

                    File.Delete(path);
                }
            }
            if (File.Exists(PathsSet.GUILogPath))
            {
                if (OutputLog)
                {
                    WriteLog($"GUI调试开启，将不会删除调试日志{PathsSet.GUILogPath}。", LogLevel.Info);

                    HandyControl.Controls.MessageBox.Show("GUI调试开启时将不会删除调试日志，请尝试关闭GUI调试。","提示",MessageBoxButton.OK,MessageBoxImage.Information);
                }
                else
                {
                    File.Delete(PathsSet.GUILogPath);
                }
                WriteLog($"临时文件{PathsSet.GUILogPath}存在，删除。", LogLevel.Info);
            }

            if (NeedRestart)
            {
                // 重启服务
                await StartService();
            }

            WriteLog($"服务运行日志及缓存清理完成。", LogLevel.Info);

            // 重新启用定期更新大小的计时器
            _TabCUpdateTimer.Start();

            // 弹出窗口提示清理完成
            HandyControl.Controls.MessageBox.Show("服务运行日志及缓存清理完成！", "清理", MessageBoxButton.OK, MessageBoxImage.Information);

            // 更新清理按钮的内容，显示所有临时文件的总大小
            CleanBtn.Content = $"清理服务运行日志及缓存 ({FileHelper.GetTotalFileSizeInMB(PathsSet.TempFilesPathsIncludingGUILog)}MB)";
            CleanBtn.IsEnabled = true;

            WriteLog("完成CleanBtn_Click。", LogLevel.Debug);
        }

        // 选项卡发生改变时的事件
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WriteLog("进入TabControl_SelectionChanged。", LogLevel.Debug);

            // 使用模式匹配和null条件运算符来检查 sender 是否为 TabControl 的实例，如果不是，则直接返回
            if (sender is not TabControl tabControl) return;

            // 尝试将 TabControl 的选中项转换为 TabItem
            var selectedItem = tabControl.SelectedItem as TabItem;

            WriteLog($"获取到选项卡标题：{selectedItem?.Header.ToString()}。", LogLevel.Debug);

            // 根据选中项的标题来决定执行哪个操作
            switch (selectedItem?.Header.ToString())
            {
                // 如果选中项的标题是"日志"
                case "设置":
                    CleanBtn.Content = $"清理服务运行日志及缓存 ({FileHelper.GetTotalFileSizeInMB(PathsSet.TempFilesPathsIncludingGUILog)}MB)";
                    SyncFromConfig();
                    // 启动日志更新定时器并停止服务状态信息更新定时器
                    _TabCUpdateTimer?.Start();
                    _TabAUpdateTimer?.Stop();
                    break;
                // 如果选中项的标题是"主页"
                case "主页":
                    // 不 UpdateAdaptersCombo() 是因为会引发绑定异常
                    UpdateServiceST();
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

            WriteLog("完成TabControl_SelectionChanged。", LogLevel.Debug);
        }

        // 全部开启按钮的点击事件
        private void AllOnBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入AllOnBtn_Click。", LogLevel.Debug);

            bool IsChanged = false;

            foreach (var togglebutton in ToggleButtonToSectionNamedDic)
            {
                if(togglebutton.Key.IsChecked != true)
                {
                    togglebutton.Key.IsChecked = true;
                    IsChanged = true;
                }
            }

            if (IsChanged)
            {
                ApplyBtn.IsEnabled = true;
                UnchangeBtn.IsEnabled = true;
            }

            WriteLog("完成AllOnBtn_Click。", LogLevel.Debug);
        }

        // 全部关闭按钮的点击事件
        private void AllOffBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入AllOffBtn_Click。", LogLevel.Debug);

            bool IsChanged = false;

            foreach (var togglebutton in ToggleButtonToSectionNamedDic)
            {
                if (togglebutton.Key.IsChecked != false)
                {
                    togglebutton.Key.IsChecked = false;
                    IsChanged = true;
                }
            }

            if (IsChanged)
            {
                ApplyBtn.IsEnabled = true;
                UnchangeBtn.IsEnabled = true;
            }

            WriteLog("完成AllOffBtn_Click。", LogLevel.Debug);
        }

        // 安装证书按钮的点击事件
        private void InstallCertBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入InstallCertBtn_Click。", LogLevel.Debug);

            InstallCertificate();

            WriteLog("完成InstallCertBtn_Click。", LogLevel.Debug);
        }

        // 自定义背景按钮的点击事件
        private void CustomBkgBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入CustomBkgBtn_Click。", LogLevel.Debug);

            // 创建OpenFileDialog实例
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "选择图片",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Filter = "图片 (*.jpg; *.jpeg; *.png)|*.jpg;*.jpeg;*.png"
            };
            // 显示对话框并检查结果
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                // 获取选中的文件路径
                string sourceFile = openFileDialog.FileName;

                WriteLog($"用户在对话框中选择了{sourceFile}。", LogLevel.Info);

                try
                {
                    // 确保目标目录存在
                    if (!Directory.Exists(PathsSet.dataDirectory))
                    {
                        Directory.CreateDirectory(PathsSet.dataDirectory);
                    }
                    // 获取文件的后缀名
                    string extension = Path.GetExtension(sourceFile);

                    // 构造新的文件名
                    string newFileName = "CustomBkg" + extension;

                    // 获取目标文件路径
                    string destinationFile = Path.Combine(PathsSet.dataDirectory, newFileName);

                    // 复制文件
                    File.Copy(sourceFile, destinationFile, overwrite: true);

                    // 写入配置文件
                    ConfigINI.INIWrite("程序设置", "Background", "Custom", PathsSet.INIPath);

                    UpdateBackground();
                }
                catch (Exception ex)
                {
                    WriteLog($"遇到异常。", LogLevel.Error, ex);

                    HandyControl.Controls.MessageBox.Show($"遇到异常: {ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            WriteLog("完成CustomBkgBtn_Click。", LogLevel.Debug);
        }

        // 恢复默认背景按钮的点击事件
        private void DefaultBkgBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入DefaultBkgBtn_Click。", LogLevel.Debug);

            ConfigINI.INIWrite("程序设置", "Background", "Preset", PathsSet.INIPath);
            UpdateBackground();

            WriteLog("完成DefaultBkgBtn_Click。", LogLevel.Debug);
        }

        // 代理开关点击事件
        private void ToggleButtonsClick(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ToggleButtonsClick。", LogLevel.Debug);

            if (sender is ToggleButton toggleButton)
            {
                WriteLog($"ToggleButtonsClick，由{toggleButton.Name}触发。", LogLevel.Info);
            }

            ApplyBtn.IsEnabled = true;
            UnchangeBtn.IsEnabled = true;

            WriteLog("完成ToggleButtonsClick。", LogLevel.Debug);
        }

        // 链接文本点击事件
        private void LinkText_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            string url = "";
            if (sender is TextBlock textblock)
            {
                // 要打开的链接
                url = textblock.Text;

                WriteLog($"进入LinkText_PreviewMouseDown，由{textblock.Name}触发。", LogLevel.Debug);
            }
            else if (sender is Run run)
            {
                url = run.Text;

                WriteLog($"进入LinkText_PreviewMouseDown，由{run.Name}触发。", LogLevel.Debug);
            }
            else
            {
                WriteLog("完成LinkText_PreviewMouseDown。", LogLevel.Debug);

                return;
            }
            // 检查URL是否以http://或https://开头
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                // 如果不是，则添加https://
                url = "https://" + url;
            }

            WriteLog($"用户点击的链接被识别为{url}。", LogLevel.Info);

            // 使用 Process.Start 来打开默认浏览器并导航到链接
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            WriteLog("完成LinkText_PreviewMouseDown。", LogLevel.Debug);
        }

        // 应用更改按钮点击事件
        private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ApplyBtn_Click。", LogLevel.Debug);

            ApplyBtn.IsEnabled = false;
            UnchangeBtn.IsEnabled = false;
            ApplyBtn.Content = "应用更改中";

            bool NeedRestart = false;
            bool IsDnsRunning = AcrylicService.AcrylicServiceIsRunning();

            WriteLog($"DNS服务运行中：{StringBoolConverter.BoolToYesNo(IsDnsRunning)}", LogLevel.Info);

            if (IsDnsRunning)
            {
                WriteLog($"DNS服务运行中，稍后需要重新启动。", LogLevel.Info);

                // 如果DNS服务在运行，则稍后需要重启
                NeedRestart = true;
            }

            // 先关停所有服务
            await StopService();

            // 根据开关状态更新配置文件
            UpdateConfigINI();

            // 再从配置文件同步到 Hosts
            UpdateHosts();

            if (NeedRestart)
            {
                // 重启服务
                await StartService();
            }

            ApplyBtn.Content = "应用更改";

            WriteLog("完成ApplyBtn_Click。", LogLevel.Debug);
        }

        // 取消更改按钮点击事件
        private void UnchangeBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入UnchangeBtn_Click。", LogLevel.Debug);

            // 从配置文件同步开关
            SyncFromConfig();
            ApplyBtn.IsEnabled = false;
            UnchangeBtn.IsEnabled = false;

            WriteLog("完成UnchangeBtn_Click。", LogLevel.Debug);
        }

        // 菜单：显示主窗口点击事件
        private void MenuItem_ShowMainWin_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入MenuItem_ShowMainWin_Click。", LogLevel.Debug);

            this.Show();
            this.Activate();

            WriteLog("完成MenuItem_ShowMainWin_Click。", LogLevel.Debug);
        }

        // 菜单：启动服务点击事件
        private void MenuItem_StartService_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入MenuItem_StartService_Click。", LogLevel.Debug);

            StartBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            TaskbarIcon.ShowBalloonTip("服务启动完成", "您现在可以尝试访问列表中的网站。", BalloonIcon.Info);

            WriteLog("完成MenuItem_StartService_Click。", LogLevel.Debug);
        }

        // 菜单：停止服务点击事件
        private void MenuItem_StopService_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入MenuItem_StopService_Click。", LogLevel.Debug);

            StopBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            TaskbarIcon.ShowBalloonTip("服务停止完成", "感谢您的使用 ~\\(≥▽≤)/~", BalloonIcon.Info);

            WriteLog("完成MenuItem_StopService_Click。", LogLevel.Debug);
        }

        // 菜单：退出工具点击事件
        private void MenuItem_ExitTool_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入MenuItem_ExitTool_Click。", LogLevel.Debug);

            ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // 不必要的日志记录
            WriteLog("进入MenuItem_ExitTool_Click。", LogLevel.Debug);
        }

        // 托盘图标点击事件
        public async void TaskbarIcon_LeftClick()
        {
            WriteLog("进入TaskbarIcon_LeftClick。", LogLevel.Debug);

            this.Show();
            this.Activate();
            await UpdateYiyan();

            WriteLog("完成TaskbarIcon_LeftClick。", LogLevel.Debug);
        }

        // 最小化到托盘图标运行按钮点击事件
        private void TaskbarIconRunBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入TaskbarIconRunBtn_Click。", LogLevel.Debug);

            this.Hide();
            TaskbarIcon.ShowBalloonTip("已最小化运行", "点击图标显示主窗体或右键显示菜单", BalloonIcon.Info);

            WriteLog("完成TaskbarIconRunBtn_Click。", LogLevel.Debug);
        }

        // 窗口加载完成事件
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WriteLog("进入Window_Loaded。", LogLevel.Debug);

            // 初始化字典
            InitializeToggleButtonDictionary(this);

            // 检查文件，当配置文件不存在时可以创建
            CheckFiles();

            // 更新背景图像
            UpdateBackground();

            UpdateServiceST();

            UpdateAdaptersCombo();

            // 从配置信息更新开关状态
            SyncFromConfig();

            // 在主窗口标题显示版本
            WindowTitle.Text = "SNIBypassGUI " + PresetGUIVersion;

            // 检查应用程序的设置，判断是否为第一次使用
            if (StringBoolConverter.StringToBool(ConfigINI.INIRead("程序设置", "IsFirst", PathsSet.INIPath)))
            {
                WriteLog("软件应为首次使用，提示用户安装证书与选择正在使用的网络适配器。", LogLevel.Info);

                MessageBoxResult UserConfirm = HandyControl.Controls.MessageBox.Show("第一次使用需要安装证书，安装证书的对话框请点击“是 (Y)”。\r\n还需要在下拉框中选择当前正在使用的网络适配器。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                if (UserConfirm == MessageBoxResult.OK)
                {
                    if (InstallCertificate())
                    {
                        ConfigINI.INIWrite("程序设置", "IsFirst", "false", PathsSet.INIPath);
                    }
                }
            }

            try
            {
                if (!AcrylicService.AcrylicServiceIsRunning())
                {
                    WriteLog("DNS服务未在运行，将进行处理。", LogLevel.Info);

                    // DNS服务处理
                    if (AcrylicService.AcrylicServiceIsInstalled() == false)
                    {
                        WriteLog("DNS服务未安装，将尝试安装。", LogLevel.Info);

                        await AcrylicService.InstallAcrylicService();
                    }
                    else
                    {
                        WriteLog("DNS服务已安装，将尝试重新安装。", LogLevel.Info);

                        await AcrylicService.UninstallAcrylicService();
                        await AcrylicService.InstallAcrylicService();
                    }
                }
            }
            catch (AcrylicServicException ex)
            {
                WriteLog($"处理DNS服务时遇到异常。", LogLevel.Error, ex);

                HandyControl.Controls.MessageBox.Show($"处理DNS服务时遇到异常：{ex.Source}\r\n{ex}","错误",MessageBoxButton.OK,MessageBoxImage.Error);
            }catch (Exception ex)
            {
                WriteLog($"遇到异常。", LogLevel.Error, ex);

                HandyControl.Controls.MessageBox.Show($"遇到异常：{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // 启动用于定期更新服务状态信息的定时器，
            _TabAUpdateTimer.Start();

            // 区别由服务启动与用户启动进行不同处理
            string curPath = Environment.CurrentDirectory;
            bool isRunWinService = (curPath != Path.GetDirectoryName(PathsSet.currentDirectory));
            if (isRunWinService)
            {
                WriteLog("程序应为计划任务启动，将托盘运行并自动启动服务。", LogLevel.Info);

                // 如果是由服务启动，则托盘运行并自动启动服务
                TaskbarIconRunBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                MenuItem_StartService.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }

            // 更新一言
            await UpdateYiyan();

            WriteLog("完成Window_Loaded。", LogLevel.Debug);
        }

        // 窗口关闭事件
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            WriteLog("进入Window_Closing。", LogLevel.Debug);

            e.Cancel = true;

            TaskbarIconRunBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            WriteLog("完成Window_Closing。", LogLevel.Debug);
        }

        // 菜单选项鼠标进入事件（用于突出显示）
        private void MenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            WriteLog("进入MenuItem_MouseEnter。", LogLevel.Debug);

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

            WriteLog("完成MenuItem_MouseEnter。", LogLevel.Debug);
        }

        // 菜单选项鼠标离开事件（用于突出显示）
        private void MenuItem_MouseLeave(object sender, MouseEventArgs e)
        {
            WriteLog("进入MenuItem_MouseLeave。", LogLevel.Debug);

            if (sender is MenuItem menuitem)
            {
                menuitem.Header = $"{menuitem.Header.ToString().Substring(1, menuitem.Header.ToString().Length - 2)}";
                menuitem.Foreground = new SolidColorBrush(Colors.White);
                menuitem.FontSize = menuitem.FontSize - 2;
            }

            WriteLog("完成MenuItem_MouseLeave。", LogLevel.Debug);
        }

        // 调试模式按钮点击事件
        private void DebugModeBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入DebugModeBtn_Click。", LogLevel.Debug);

            if (DebugModeBtn.Content.ToString() == "调试模式：\n关")
            {
                if (HandyControl.Controls.MessageBox.Show("调试模式仅供测试和开发使用，强烈建议您在没有开发者明确指示的情况下不要随意打开。\r\n是否打开调试模式？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    ConfigINI.INIWrite("高级设置", "DebugMode", "true", PathsSet.INIPath);
                    SyncFromConfig();
                }
            }
            else
            {
                ConfigINI.INIWrite("高级设置", "DebugMode", "false", PathsSet.INIPath);
                ConfigINI.INIWrite("高级设置", "GUIDebug", "false", PathsSet.INIPath);
                ConfigINI.INIWrite("高级设置", "DomainNameResolutionMethod", "DnsService", PathsSet.INIPath);
                ConfigINI.INIWrite("高级设置", "AcrylicDebug", "false", PathsSet.INIPath);
                SyncFromConfig();
            }

            WriteLog("完成DebugModeBtn_Click。", LogLevel.Debug);
        }

        // 域名解析模式按钮点击事件
        private void SwitchDomainNameResolutionMethodBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入SwitchDomainNameResolutionMethodBtn_Click。", LogLevel.Debug);

            if (SwitchDomainNameResolutionMethodBtn.Content.ToString() == "域名解析模式：\nDNS服务")
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
            SyncFromConfig();

            WriteLog("完成SwitchDomainNameResolutionMethodBtn_Click。", LogLevel.Debug);
        }

        // DNS服务调试按钮点击事件
        private void AcrylicDebugBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入AcrylicDebugBtn_Click。", LogLevel.Debug);

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
            SyncFromConfig();

            WriteLog("完成AcrylicDebugBtn_Click。", LogLevel.Debug);
        }

        // GUI调试按钮点击事件
        private void GUIDebugBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入GUIDebugBtn_Click。", LogLevel.Debug);

            if (GUIDebugBtn.Content.ToString() == "GUI调试：\n关")
            {
                if (HandyControl.Controls.MessageBox.Show("开启GUI调试模式可以更准确地诊断问题，但生成日志会产生额外的性能开销，请在不需要时关闭。\r\n开启后将自动关闭程序，重启程序后生效。\r\n请在重启直到程序出现问题后，将\\data\\logs目录下的GUI.log提交给开发者。\r\n是否打开GUI调试模式？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    ConfigINI.INIWrite("高级设置", "GUIDebug", "true", PathsSet.INIPath);
                    ExitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            }
            else
            {
                ConfigINI.INIWrite("高级设置", "GUIDebug", "false", PathsSet.INIPath);
                SyncFromConfig();
            }

            WriteLog("完成GUIDebugBtn_Click。", LogLevel.Debug);
        }

        // 编辑系统hosts按钮点击事件
        private void EditHostsBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入EditHostsBtn_Click。", LogLevel.Debug);

            // 检查文件是否存在
            if (File.Exists(PathsSet.SystemHosts))
            {
                // 启动记事本并打开文件
                Process.Start("notepad.exe", PathsSet.SystemHosts);
            }
            else
            {
                HandyControl.Controls.MessageBox.Show("未在指定路径找到系统hosts！\r\n请尝试手动创建该文件。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            WriteLog("完成EditHostsBtn_Click。", LogLevel.Debug);
        }

        // 还原系统hosts按钮点击事件
        private void BackHostsBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入BackHostsBtn_Click。", LogLevel.Debug);

            if (HandyControl.Controls.MessageBox.Show("还原系统hosts功能用于消除本程序对系统hosts所产生的影响。\r\n当您认为本程序（特别是历史版本）对您的系统hosts造成了不良影响时可以使用此功能。\r\n是否还原系统hosts？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var sectionname in SectionNamesSet)
                {
                    WriteLog($"移除{sectionname}的记录部分。", LogLevel.Info);

                    FileHelper.RemoveSection(PathsSet.SystemHosts, sectionname);
                }
            }

            DNS.FlushDNS();

            WriteLog("完成BackHostsBtn_Click。", LogLevel.Debug);
        }

        // 自动获取活动适配器按钮的点击事件
        private void GetActiveAdapterBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入GetActiveAdapterBtn_Click。", LogLevel.Debug);

            UpdateAdaptersCombo();

            List<NetAdp> adapters = NetAdp.GetAdapters();
            NetAdp activeAdapter = null;
            foreach (var adapter in adapters)
            {
                if (adapter.Status == System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    if (adapter.DNS != null)
                    {
                        activeAdapter = adapter;
                        break;
                    }
                }
            }
            if (activeAdapter != null)
            {
                if (AdaptersCombo.Items.Cast<string>().Contains(activeAdapter.Name))
                {
                    AdaptersCombo.Text = activeAdapter.Name;
                   
                }
            }
            else
            {
                WriteLog($"没有找到活动且可设置的网络适配器！", LogLevel.Warning);

                if (HandyControl.Controls.MessageBox.Show($"没有找到活动且可设置的网络适配器！您可能需要手动设置。\r\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning)==MessageBoxResult.Yes)
                {
                    VideoHelpWindow videoHelpWindow = new VideoHelpWindow("如何手动设置适配器", PathsSet.HelpVideo_如何手动设置适配器_Path);
                    videoHelpWindow.Show();
                }
            }

            WriteLog("完成GetActiveAdapterBtn_Click。", LogLevel.Debug);
        }

        private void HelpBtn_HowToFindActiveAdapter_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入HelpBtn_HowToFindActiveAdapter_Click。", LogLevel.Debug);

            VideoHelpWindow videoHelpWindow = new VideoHelpWindow("如何寻找活动的网络适配器", PathsSet.HelpVideo_如何寻找活动适配器_Path);
            videoHelpWindow.Show();

            WriteLog("完成HelpBtn_HowToFindActiveAdapter_Click。", LogLevel.Debug);
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