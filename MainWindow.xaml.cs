using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using static SNIBypassGUI.PublicHelper;
using static SNIBypassGUI.LogHelper;

namespace SNIBypassGUI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 定义用于更新日志和 Nginx 状态的计时器
        private readonly DispatcherTimer _logUpdateTimer;
        private readonly DispatcherTimer _ServiceStUpdateTimer;

        // 从配置文件读取的程序信息
        bool IsFirst;

        string BackgroundVal;

        // 窗口构造函数
        public MainWindow()
        {
            // 读取日志开关
            OutputLog = StringBoolConverter.StringToBool(ConfigINI.INIRead("日志开关", "OutputLog", INIPath));

            if (OutputLog && !Directory.Exists(GUILogDirectory))
            {
                // 创建目录
                Directory.CreateDirectory(GUILogDirectory);
            }

            WriteLog("进入MainWindow()", LogLevel.Debug);

            InitializeComponent();
            // 创建一个新的 DispatcherTimer 实例，用于定期更新日志信息
            _logUpdateTimer = new DispatcherTimer
            {
                // 设置 timer 的时间间隔为每5秒触发一次
                Interval = TimeSpan.FromSeconds(5)
            };
            // 当 timer 到达指定的时间间隔时，将调用 LogUpdateTimer_Tick 方法
            _logUpdateTimer.Tick += LogUpdateTimer_Tick;
            // 创建另一个新的 DispatcherTimer 实例，用于定期更新服务状态信息
            _ServiceStUpdateTimer = new DispatcherTimer
            {
                // 设置 timer 的时间间隔为每5秒触发一次
                Interval = TimeSpan.FromSeconds(5)
            };
            // 当 timer 到达指定的时间间隔时，将调用 ServiceStUpdateTimer_Tick 方法
            _ServiceStUpdateTimer.Tick += ServiceStUpdateTimer_Tick;

            // 窗口可拖动化
            this.TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };

            WriteLog("完成MainWindow()", LogLevel.Debug);
        }

        // 日志更新计时器触发事件
        private void LogUpdateTimer_Tick(object sender, EventArgs e)
        {
            WriteLog("进入LogUpdateTimer_Tick(object sender, EventArgs e)", LogLevel.Debug);

            // 更新清理日志按钮的内容，显示所有日志文件的总大小（以MB为单位）
            CleanlogBtn.Content = $"清理服务运行日志 ({GetTotalFileSizeInMB(LogfilePaths)}MB)";

            WriteLog("完成LogUpdateTimer_Tick(object sender, EventArgs e)", LogLevel.Debug);
        }

        // Nginx 状态更新计时器触发事件
        private void ServiceStUpdateTimer_Tick(object sender, EventArgs e)
        {
            WriteLog("进入ServiceStUpdateTimer_Tick(object sender, EventArgs e)", LogLevel.Debug);

            // 更新服务状态
            UpdateServiceST();

            WriteLog("完成ServiceStUpdateTimer_Tick(object sender, EventArgs e)", LogLevel.Debug);
        }

        // 更新服务状态的方法
        public void UpdateServiceST()
        {
            WriteLog("进入UpdateServiceST()", LogLevel.Debug);

            // 使用 Process.GetProcessesByName 方法获取所有名为 "SNIBypass" 的进程，这将返回一个包含所有匹配进程的 Process 数组
            Process[] ps = Process.GetProcessesByName("SNIBypass");
            // 检查获取到的进程数组长度是否大于 0
            // 如果大于 0，说明服务正在运行

            WriteLog($"获取到的进程数组长度为 {ps.Length}", LogLevel.Debug);

            if (ps.Length > 0)
            {
                // 更新服务状态文本为 "当前服务状态：运行中"
                ServiceST.Text = "当前服务状态：运行中";
                // 将服务状态文本的前景色设置为森林绿
                ServiceST.Foreground = new SolidColorBrush(Colors.ForestGreen);
                // 禁用启动按钮，因为服务已经在运行
                StartBtn.IsEnabled = false;
                // 启用停止按钮，因为可以停止正在运行的服务
                StopBtn.IsEnabled = true;
            }
            else
            {
                // 如果进程数组长度为 0，说明服务没有运行
                // 更新服务状态文本为 "当前服务状态：已停止"
                ServiceST.Text = "当前服务状态：已停止";
                // 将服务状态文本的前景色设置为红色
                ServiceST.Foreground = new SolidColorBrush(Colors.Red);
                // 启用启动按钮，因为可以启动服务
                StartBtn.IsEnabled = true;
                // 禁用停止按钮，因为没有正在运行的服务可以停止
                StopBtn.IsEnabled = false;
            }

            WriteLog("完成UpdateServiceST()", LogLevel.Debug);
        }

        // 更新背景图片的方法
        public void UpdateBackground()
        {
            WriteLog("进入UpdateBackground()", LogLevel.Debug);

            string Background = ConfigINI.INIRead("程序设置", "Background", INIPath);
            if (Background == "Custom")
            {
                FileHelper fileHelper = new FileHelper(dataDirectory);
                var CustomBkgPath = fileHelper.FindCustomBkg();
                if (CustomBkgPath != null)
                {
                    ImageBrush cbg = new ImageBrush();
                    cbg.ImageSource = GetImage(CustomBkgPath);
                    cbg.Stretch= Stretch.UniformToFill;
                    MainPage.Background = cbg;

                    WriteLog("完成UpdateBackground()，设置为自定义背景图片。", LogLevel.Debug);

                    return;
                }
            }
            ConfigINI.INIWrite("程序设置", "Background", "Preset", INIPath);
            ImageBrush bg = new ImageBrush();
            bg.ImageSource = new BitmapImage(new Uri("pack://application:,,,/SNIBypassGUI;component/Resources/DefaultBkg.jpg"));
            bg.Stretch = Stretch.UniformToFill;
            MainPage.Background = bg;

            WriteLog("完成UpdateBackground()，设置为默认背景图片。", LogLevel.Debug);
        }

        // 用于检查重要目录是否存在以及日志文件、配置文件是否创建的方法
        public void CheckFiles()
        {
            WriteLog("进入CheckFiles()", LogLevel.Debug);

            // 确保必要目录存在
            EnsureDirectoryExists(dataDirectory);
            EnsureDirectoryExists(NginxDirectory);
            EnsureDirectoryExists(nginxConfigDirectory);
            EnsureDirectoryExists(CADirectory);
            EnsureDirectoryExists(nginxLogDirectory);
            EnsureDirectoryExists(nginxTempDirectory);

            // 释放相关文件
            if (!File.Exists(nginxPath))
            {
                WriteLog($"文件{nginxPath}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.SNIBypass, nginxPath);
            }
            if (!File.Exists(nginxConfigFile_A))
            {
                WriteLog($"文件{nginxConfigFile_A}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.nginx, nginxConfigFile_A);
            }
            if (!File.Exists(nginxConfigFile_B))
            {
                WriteLog($"文件{nginxConfigFile_B}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.bypass, nginxConfigFile_B);
            }
            if (!File.Exists(nginxConfigFile_C))
            {
                WriteLog($"文件{nginxConfigFile_C}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.shared_proxy_params_1, nginxConfigFile_C);
            }
            if (!File.Exists(nginxConfigFile_D))
            {
                WriteLog($"文件{nginxConfigFile_D}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.shared_proxy_params_2, nginxConfigFile_D);
            }
            if (!File.Exists(nginxConfigFile_E))
            {
                WriteLog($"文件{nginxConfigFile_E}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.cert, nginxConfigFile_E);
            }
            if (!File.Exists(CERFile))
            {
                WriteLog($"文件{CERFile}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.ca, CERFile);
            }
            if (!File.Exists(CRTFile))
            {
                WriteLog($"文件{CRTFile}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.SNIBypassCrt, CRTFile);
            }
            if (!File.Exists(KeyFile))
            {
                WriteLog($"文件{KeyFile}不存在，释放。", LogLevel.Warning);

                ExtractNormalFileInResx(Properties.Resources.SNIBypassKey, KeyFile);
            }

            // 如果配置文件不存在，则创建配置文件
            if (!File.Exists(INIPath))
            {
                WriteLog($"配置文件{INIPath}不存在，创建。", LogLevel.Warning);

                ConfigINI.INIWrite("程序设置", "IsFirst", "true", INIPath);
                ConfigINI.INIWrite("程序设置", "Background", "Preset", INIPath);
                ConfigINI.INIWrite("代理开关", "Archive of Our Own", "true", INIPath);
                ConfigINI.INIWrite("代理开关", "E-Hentai", "true", INIPath);
                ConfigINI.INIWrite("代理开关", "Nyaa", "true", INIPath);
                ConfigINI.INIWrite("代理开关", "Pixiv", "true", INIPath);
                ConfigINI.INIWrite("代理开关", "Pornhub", "true", INIPath);
                ConfigINI.INIWrite("代理开关", "Steam Community", "true", INIPath);
                ConfigINI.INIWrite("代理开关", "WallHaven", "true", INIPath);
                ConfigINI.INIWrite("代理开关", "Wikimedia Foundation", "true", INIPath);
                ConfigINI.INIWrite("日志开关", "OutputLog", "false", INIPath);
            }

            WriteLog("完成CheckFiles()", LogLevel.Debug);
        }

        // 窗口加载完成事件
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WriteLog("进入Window_Loaded(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 检查文件，当配置文件不存在时可以创建
            CheckFiles();

            // 更新背景图像
            UpdateBackground();

            // 从配置信息更新
            UpdateInfo();

            // 在主窗口标题显示版本
            WindowTitle.Text = "SNIBypassGUI " + PresetGUIVersion;

            // 更新服务的状态信息
            UpdateServiceST();

            // 检查应用程序的设置，判断是否为第一次使用
            if (IsFirst)
            {
                WriteLog("软件应为第一次使用，提示用户安装证书。", LogLevel.Info);
                MessageBoxResult UserConfirm = HandyControl.Controls.MessageBox.Show("第一次使用需要安装证书，安装证书的对话框请点击“是 (Y)”。","提示",MessageBoxButton.OK,MessageBoxImage.Information);
                if (UserConfirm == MessageBoxResult.OK)
                {
                    InstallCertificate();
                }
            }

            // 为 TabControl 的 SelectionChanged 事件添加事件处理程序，用户切换选项卡时，将调用 TabControl_SelectionChanged 方法
            // 在这里才添加的原因是如果在xaml中添加，窗口加载完成时就会触发 TabControl_SelectionChanged ，而所选页面为主页时会 UpdateServiceST() ，此时 ServiceST 为 Null
            tabcontrol.SelectionChanged += TabControl_SelectionChanged;
            // 启动用于定期更新服务状态信息的定时器，
            _ServiceStUpdateTimer.Start();

            WriteLog("完成Window_Loaded(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 从配置信息更新
        public void UpdateInfo()
        {
            WriteLog("进入UpdateInfo()", LogLevel.Debug);

            // 从配置文件中读取代理开关，更新至开关列表
            archiveofourownTB.IsChecked = StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", "Archive of Our Own", INIPath));
            ehentaiTB.IsChecked = StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", "E-Hentai",INIPath));
            nyaaTB.IsChecked = StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", "Nyaa", INIPath));
            pixivTB.IsChecked = StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", "Pixiv", INIPath));
            pornhubTB.IsChecked = StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", "Pornhub", INIPath));
            steamcommunityTB.IsChecked = StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", "Steam Community", INIPath));
            wallhavenTB.IsChecked = StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", "WallHaven", INIPath));
            wikimediafoundationTB.IsChecked = StringBoolConverter.StringToBool(ConfigINI.INIRead("代理开关", "Wikimedia Foundation", INIPath));
            IsFirst = StringBoolConverter.StringToBool(ConfigINI.INIRead("程序设置", "IsFirst", INIPath));
            BackgroundVal = ConfigINI.INIRead("程序设置", "Background", INIPath);

            WriteLog($"从配置文件读取到IsFirst：{IsFirst}", LogLevel.Info);
            WriteLog($"从配置文件读取到BackgroundVal：{BackgroundVal}", LogLevel.Info);

            // 根据配置文件更新 hosts
            RemoveSection(SystemHosts, "Archive of Our Own");
            RemoveSection(SystemHosts, "E-Hentai");
            RemoveSection(SystemHosts, "Nyaa");
            RemoveSection(SystemHosts, "Pixiv");
            RemoveSection(SystemHosts, "Pornhub");
            RemoveSection(SystemHosts, "Steam Community");
            RemoveSection(SystemHosts, "Wallhaven");
            RemoveSection(SystemHosts, "Wikimedia Foundation");

            if (archiveofourownTB.IsChecked == true)
            {
                WriteLinesToFile(ArchiveofOurOwnSection, SystemHosts);
            }
            if (ehentaiTB.IsChecked == true)
            {
                WriteLinesToFile(EHentaiSection, SystemHosts);
            }
            if (nyaaTB.IsChecked == true)
            {
                WriteLinesToFile(NyaaSection, SystemHosts);
            }
            if (pixivTB.IsChecked == true)
            {
                WriteLinesToFile (PixivSection, SystemHosts);
            }
            if (pornhubTB.IsChecked == true)
            {
                WriteLinesToFile (PornhubSection, SystemHosts);
            }
            if (steamcommunityTB.IsChecked == true)
            {
                WriteLinesToFile(SteamCommunitySection, SystemHosts);
            }
            if (wallhavenTB.IsChecked == true)
            {
                WriteLinesToFile(WallhavenSection, SystemHosts);
            }
            if (wikimediafoundationTB.IsChecked == true)
            {
                WriteLinesToFile(WikimediaFoundationSection, SystemHosts);
            }
            Flushdns();

            WriteLog("完成UpdateInfo()", LogLevel.Debug);
        }


        // 刷新状态按钮的点击事件
        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入RefreshBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 更新服务的状态信息
            UpdateServiceST();

            WriteLog("完成RefreshBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 启动按钮的点击事件
        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入StartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 创建一个新的Process对象，用于启动外部进程
            Process process = new Process();
            // 设置要启动的进程的文件名
            process.StartInfo.FileName = nginxPath;
            // 设置进程的工作目录
            process.StartInfo.WorkingDirectory = NginxDirectory;
            // 设置是否使用操作系统外壳启动进程
            process.StartInfo.UseShellExecute = false;
            try
            {
                // 尝试启动进程
                process.Start();
            }
            catch (Exception ex)
            {
                WriteLog($"尝试启动进程时遇到异常：{ex}", LogLevel.Error);

                // 如果启动进程时发生异常，则显示一个错误消息框
                HandyControl.Controls.MessageBox.Show($"无法启动进程: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // 更新服务的状态信息
            UpdateServiceST();

            WriteLog("完成StartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 停止按钮的点击事件
        private async void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入StopBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 异步调用该方法，用于杀死所有名为"SNIBypass"的进程
            await KillSNIBypass();

            // 更新服务的状态信息
            UpdateServiceST();

            WriteLog("完成StopBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 设置开机启动按钮的点击事件
        private void SetStartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入SetStartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            try
            {
                // 使用 TaskService 来访问和操作任务计划程序
                using (TaskService ts = new TaskService())
                {
                    // 定义要创建或更新的任务名称
                    string taskName = "StartSNIBypassService";
                    // 尝试获取已存在的同名任务
                    Task existingTask = ts.GetTask(taskName);
                    // 如果任务已存在，则删除它，以便创建新的任务
                    if (existingTask != null)
                    {
                        WriteLog("计划任务StartSNIBypassService已经存在，尝试移除。", LogLevel.Warning);

                        ts.RootFolder.DeleteTask(taskName);
                    }
                    // 创建一个新的任务定义
                    TaskDefinition td = ts.NewTask();
                    // 设置任务的描述信息和作者
                    td.RegistrationInfo.Description = "开机启动 SNIBypass 服务。";
                    td.RegistrationInfo.Author = "SNIBypassGUI";
                    // 创建一个登录触发器，当用户登录时触发任务
                    LogonTrigger logonTrigger = new LogonTrigger();
                    // 将登录触发器添加到任务定义中
                    td.Triggers.Add(logonTrigger);
                    // 创建一个执行操作，指定要执行的 Nginx 路径、参数和工作目录
                    ExecAction execAction = new ExecAction(nginxPath, null, NginxDirectory);
                    // 将执行操作添加到任务定义中
                    td.Actions.Add(execAction);
                    // 在根文件夹中注册新的任务定义
                    ts.RootFolder.RegisterTaskDefinition(taskName, td);
                }
                // 显示提示信息，表示 Nginx 已成功设置为开机启动
                HandyControl.Controls.MessageBox.Show("成功设置 SNIBypass 服务为开机启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteLog($"尝试设置开机启动时遇到异常：{ex}", LogLevel.Error);

                // 捕获异常并显示错误信息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成SetStartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 退出工具按钮的点击事件
        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ExitBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 退出程序
            Environment.Exit(0);

            // 不必要的日志记录
            WriteLog("完成ExitBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 停止开机启动按钮的点击事件
        private void DelStartBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入DelStartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            try
            {
                // 使用 TaskService 来访问和操作任务计划程序
                using (TaskService ts = new TaskService())
                {
                    // 定义要创建或更新的任务名称
                    string taskName = "StartSNIBypassService";
                    // 尝试获取已存在的同名任务
                    Task existingTask = ts.GetTask(taskName);
                    // 如果任务已存在，则删除它
                    if (existingTask != null)
                    {
                        WriteLog("计划任务StartSNIBypassService已经存在，尝试移除。", LogLevel.Info);

                        ts.RootFolder.DeleteTask(taskName);
                    }
                }
                // 显示提示信息，表示 Nginx 已成功停止开机启动
                HandyControl.Controls.MessageBox.Show("成功停止 SNIBypass 服务的开机启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WriteLog($"尝试停止开机启动时遇到异常：{ex}", LogLevel.Error);

                // 捕获异常并显示错误信息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成DelStartBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 检查更新按钮的点击事件，用于检查SNIBypassGUI是否有新版本可用
        private async void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入CheckUpdateBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 修改按钮内容为“检查更新中...”以提示用户正在进行检查
            CheckUpdateBtn.Content = "检查更新中...";

            RemoveSection(SystemHosts, "api.github.com");
            EnsureGithubAPI();

            try
            {
                // 异步获取 GitHub 的最新发布信息
                string LatestReleaseInfo = await GetAsync("https://api.github.com/repos/racpast/SNIBypassGUI/releases/latest");

                WriteLog($"获取到GitHub的最新发布信息LatestReleaseInfo：{LatestReleaseInfo}", LogLevel.Info);

                // 将返回的JSON字符串解析为JObject
                JObject repodata = JObject.Parse(LatestReleaseInfo);
                // 从解析后的JSON中获取最后一次发布的信息
                string LatestReleaseTag = repodata["tag_name"].ToString();
                string LatestReleasePublishedDt = repodata["published_at"].ToString();

                WriteLog($"提取到最后一次发布的信息LatestReleaseTag：{LatestReleaseTag}", LogLevel.Info);
                WriteLog($"提取到最后一次发布的信息LatestReleasePublishedDt：{LatestReleasePublishedDt}", LogLevel.Info);

                // 比较当前安装的版本与最后一次发布的版本
                if (LatestReleaseTag.ToUpper() != PresetGUIVersion)
                {
                    WriteLog("SNIBypassGUI有新版本可以使用。", LogLevel.Info);

                    // 如果有新版本，则弹出提示框
                    HandyControl.Controls.MessageBox.Show($"SNIBypassGUI 有新版本可用，请及时获取最新版本！\r\n版本号：{LatestReleaseTag.ToUpper()}\r\n发布时间(GMT)：{LatestReleasePublishedDt}", "检查更新", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    WriteLog($"SNIBypassGUI已经是最新版本。", LogLevel.Info);

                    // 如果没有新版本，则弹出提示框告知用户已是最新版本
                    HandyControl.Controls.MessageBox.Show("SNIBypassGUI 目前已是最新版本！", "更新", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            // 更准确的报错信息（https://github.com/racpast/Pixiv-Nginx-GUI/issues/2）
            catch (OperationCanceledException)
            {
                WriteLog("获取信息请求超时，遇到OperationCanceledException。", LogLevel.Error);

                // 如果获取信息请求超时，显示提示信息
                HandyControl.Controls.MessageBox.Show("从 api.github.com 获取信息超时！\r\n请检查是否可以访问到 api.github.com 或反馈！", "错误", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常：{ex}", LogLevel.Error);

                // 捕获异常并弹出错误提示框
                HandyControl.Controls.MessageBox.Show($"检查更新时遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 检查完成后，将按钮内容改回“检查 SNIBypassGUI 是否有新版本可用”
                CheckUpdateBtn.Content = "检查 SNIBypassGUI 是否有新版本可用";
            }

            WriteLog("完成CheckUpdateBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        //清理日志按钮的点击事件
        private async void CleanlogBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入CleanlogBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            // 异步调用该方法，用于杀死所有名为"SNIBypass"的进程
            await KillSNIBypass();
            // 更新 Nginx 的状态信息
            UpdateServiceST();

            // 停止定期更新日志的计时器
            _logUpdateTimer.Stop();

            // 删除所有日志文件
            foreach(string logpath in LogfilePaths)
            {
                if (File.Exists(logpath))
                {
                    WriteLog($"删除日志文件{logpath}。", LogLevel.Info);
                    File.Delete(logpath);
                }
            }

            WriteLog($"日志文件清理完成。", LogLevel.Info);

            // 弹出窗口提示日志清理完成
            HandyControl.Controls.MessageBox.Show("日志清理完成！", "清理日志", MessageBoxButton.OK, MessageBoxImage.Information);

            // 更新清理日志按钮的内容，显示所有日志文件的总大小（以MB为单位）
            CleanlogBtn.Content = $"清理服务运行日志 ({GetTotalFileSizeInMB(LogfilePaths)}MB)";

            // 重新启用定期更新日志的计时器
            _logUpdateTimer.Start();

            WriteLog("完成CleanlogBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 选项卡选项发生改变时的事件
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WriteLog("进入TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)", LogLevel.Debug);

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
                    CleanlogBtn.Content = $"清理服务运行日志 ({GetTotalFileSizeInMB(LogfilePaths)}MB)";
                    // 启动日志更新定时器并停止服务状态信息更新定时器
                    _logUpdateTimer.Start();
                    _ServiceStUpdateTimer.Stop();
                    break;
                // 如果选中项的标题是"主页"
                case "主页":
                    // 更新服务的状态信息
                    UpdateServiceST();
                    // 停止日志更新定时器并启动服务状态信息更新定时器
                    _ServiceStUpdateTimer.Start();
                    _logUpdateTimer.Stop();
                    break;
                // 如果选中项的标题不是上述两者之一
                default:
                    // 停止所有定时器
                    _logUpdateTimer.Stop();
                    _ServiceStUpdateTimer.Stop();
                    break;
            }

            WriteLog("完成TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)", LogLevel.Debug);
        }

        // 全部开启按钮的点击事件
        private void AllOnBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入AllOnBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            RemoveSection(SystemHosts, "Archive of Our Own");
            RemoveSection(SystemHosts, "E-Hentai");
            RemoveSection(SystemHosts, "Nyaa");
            RemoveSection(SystemHosts, "Pixiv");
            RemoveSection(SystemHosts, "Pornhub");
            RemoveSection(SystemHosts, "Steam Community");
            RemoveSection(SystemHosts, "Wallhaven");
            RemoveSection(SystemHosts, "Wikimedia Foundation");
            WriteLinesToFile(ArchiveofOurOwnSection, SystemHosts);
            WriteLinesToFile(EHentaiSection, SystemHosts);
            WriteLinesToFile(NyaaSection, SystemHosts);
            WriteLinesToFile(PixivSection, SystemHosts);
            WriteLinesToFile(PornhubSection, SystemHosts);
            WriteLinesToFile(SteamCommunitySection, SystemHosts);
            WriteLinesToFile(WallhavenSection, SystemHosts);
            WriteLinesToFile(WikimediaFoundationSection, SystemHosts);
            ConfigINI.INIWrite("代理开关", "Archive of Our Own", "true", INIPath);
            ConfigINI.INIWrite("代理开关", "E-Hentai", "true", INIPath);
            ConfigINI.INIWrite("代理开关", "Nyaa", "true", INIPath);
            ConfigINI.INIWrite("代理开关", "Pixiv", "true", INIPath);
            ConfigINI.INIWrite("代理开关", "Pornhub", "true", INIPath);
            ConfigINI.INIWrite("代理开关", "Steam Community", "true", INIPath);
            ConfigINI.INIWrite("代理开关", "WallHaven", "true", INIPath);
            ConfigINI.INIWrite("代理开关", "Wikimedia Foundation", "true", INIPath);
            UpdateInfo();
            Flushdns();

            WriteLog("完成AllOnBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 全部关闭按钮的点击事件
        private void AllOffBtn_Click(object sender, RoutedEventArgs e)
        {
            RemoveSection(SystemHosts, "Archive of Our Own");
            RemoveSection(SystemHosts, "E-Hentai");
            RemoveSection(SystemHosts, "Nyaa");
            RemoveSection(SystemHosts, "Pixiv");
            RemoveSection(SystemHosts, "Pornhub");
            RemoveSection(SystemHosts, "Steam Community");
            RemoveSection(SystemHosts, "Wallhaven");
            RemoveSection(SystemHosts, "Wikimedia Foundation");
            ConfigINI.INIWrite("代理开关", "Archive of Our Own", "false", INIPath);
            ConfigINI.INIWrite("代理开关", "E-Hentai", "false", INIPath);
            ConfigINI.INIWrite("代理开关", "Nyaa", "false", INIPath);
            ConfigINI.INIWrite("代理开关", "Pixiv", "false", INIPath);
            ConfigINI.INIWrite("代理开关", "Pornhub", "false", INIPath);
            ConfigINI.INIWrite("代理开关", "Steam Community", "false", INIPath);
            ConfigINI.INIWrite("代理开关", "WallHaven", "false", INIPath);
            ConfigINI.INIWrite("代理开关", "Wikimedia Foundation", "false", INIPath);
            UpdateInfo();
            Flushdns();
        }

        // 安装证书按钮的点击事件
        private void InstallCertBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入InstallCertBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            InstallCertificate();

            WriteLog("完成InstallCertBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 自定义背景按钮的点击事件
        private void CustomBkgBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入CustomBkgBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

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
                    if (!Directory.Exists(dataDirectory))
                    {
                        Directory.CreateDirectory(dataDirectory);
                    }
                    // 获取文件的后缀名
                    string extension = Path.GetExtension(sourceFile);

                    // 构造新的文件名
                    string newFileName = "CustomBkg" + extension;

                    // 获取目标文件路径
                    string destinationFile = Path.Combine(dataDirectory, newFileName);

                    // 复制文件
                    File.Copy(sourceFile, destinationFile, overwrite: true);

                    // 写入配置文件
                    ConfigINI.INIWrite("程序设置", "Background", "Custom", INIPath);

                    UpdateBackground();
                }
                catch (Exception ex)
                {
                    WriteLog($"遇到错误：{ex}", LogLevel.Error);
                }
            }

            WriteLog("完成CustomBkgBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 恢复默认背景按钮的点击事件
        private void DefaultBkgBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入DefaultBkgBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            ConfigINI.INIWrite("程序设置", "Background", "Preset", INIPath);
            UpdateBackground();

            WriteLog("完成DefaultBkgBtn_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // Archive of Our Own 开关
        private void archiveofourownTB_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入archiveofourownTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            RemoveSection(SystemHosts, "Archive of Our Own");
            if (archiveofourownTB.IsChecked == true)
            {
                WriteLinesToFile(ArchiveofOurOwnSection, SystemHosts);
                ConfigINI.INIWrite("代理开关", "Archive of Our Own", "true", INIPath);
            }
            else
            {
                ConfigINI.INIWrite("代理开关", "Archive of Our Own", "false", INIPath);
            }
            Flushdns();

            WriteLog("完成archiveofourownTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // E-Hentai 开关
        private void ehentaiTB_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入ehentaiTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            RemoveSection(SystemHosts, "E-Hentai");
            if (ehentaiTB.IsChecked == true)
            {
                WriteLinesToFile(EHentaiSection, SystemHosts);
                ConfigINI.INIWrite("代理开关", "E-Hentai", "true", INIPath);
            }
            else
            {
                ConfigINI.INIWrite("代理开关", "E-Hentai", "false", INIPath);
            }
            Flushdns();

            WriteLog("完成ehentaiTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // Nyaa 开关
        private void nyaaTB_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入nyaaTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            RemoveSection(SystemHosts, "Nyaa");
            if (nyaaTB.IsChecked == true)
            {
                WriteLinesToFile(NyaaSection, SystemHosts);
                ConfigINI.INIWrite("代理开关", "Nyaa", "true", INIPath);
            }
            else
            {
                ConfigINI.INIWrite("代理开关", "Nyaa", "false", INIPath);
            }
            Flushdns();

            WriteLog("完成nyaaTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // Pixiv 开关
        private void pixivTB_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入pixivTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            RemoveSection(SystemHosts, "Pixiv");
            if (pixivTB.IsChecked == true)
            {
                WriteLinesToFile(PixivSection, SystemHosts);
                ConfigINI.INIWrite("代理开关", "Pixiv", "true", INIPath);

            }
            else
            {
                ConfigINI.INIWrite("代理开关", "Pixiv", "false", INIPath);
            }
            Flushdns();

            WriteLog("完成pixivTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // Pornhub 开关
        private void pornhubTB_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入pixivTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            RemoveSection(SystemHosts, "Pornhub");
            if (pornhubTB.IsChecked == true)
            {
                WriteLinesToFile(PornhubSection, SystemHosts);
                ConfigINI.INIWrite("代理开关", "Pornhub", "true", INIPath);

            }
            else
            {
                ConfigINI.INIWrite("代理开关", "Pornhub", "false", INIPath);
            }
            Flushdns();

            WriteLog("完成pixivTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // Steam Community 开关
        private void steamcommunityTB_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入steamcommunityTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            RemoveSection(SystemHosts, "Steam Community");
            if (steamcommunityTB.IsChecked == true)
            {
                WriteLinesToFile(SteamCommunitySection, SystemHosts);
                ConfigINI.INIWrite("代理开关", "Steam Community", "true", INIPath);

            }
            else
            {
                ConfigINI.INIWrite("代理开关", "Steam Community", "false", INIPath);
            }
            Flushdns();

            WriteLog("完成steamcommunityTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // Wikimedia Foundation 开关
        private void wikimediafoundationTB_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入wikimediafoundationTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            RemoveSection(SystemHosts, "Wikimedia Foundation");
            if (wikimediafoundationTB.IsChecked == true)
            {
                WriteLinesToFile(WikimediaFoundationSection, SystemHosts);
                ConfigINI.INIWrite("代理开关", "Wikimedia Foundation", "true", INIPath);

            }
            else
            {
                ConfigINI.INIWrite("代理开关", "Wikimedia Foundation", "false", INIPath);
            }
            Flushdns();

            WriteLog("完成wikimediafoundationTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // Wallhaven 开关
        private void wallhavenTB_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入wallhavenTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);

            RemoveSection(SystemHosts, "Wallhaven");
            if (wallhavenTB.IsChecked == true)
            {
                WriteLinesToFile(WallhavenSection, SystemHosts);
                ConfigINI.INIWrite("代理开关", "WallHaven", "true", INIPath);

            }
            else
            {
                ConfigINI.INIWrite("代理开关", "WallHaven", "false", INIPath);
            }
            Flushdns();

            WriteLog("完成wallhavenTB_Click(object sender, RoutedEventArgs e)", LogLevel.Debug);
        }

        // 链接文本点击事件
        private void LinkText_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            WriteLog("进入LinkText_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)", LogLevel.Debug);

            string url = "";
            if (sender is TextBlock textblock)
            {
                // 要打开的链接
                url = textblock.Text;

                WriteLog($"LinkText_PreviewMouseDown由 {textblock.Name} 触发。", LogLevel.Info);
            }
            else if (sender is Run run)
            {
                // 要打开的链接
               url = run.Text;

                WriteLog($"LinkText_PreviewMouseDown由 {run.Name} 触发。", LogLevel.Info);
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

            WriteLog("完成LinkText_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)", LogLevel.Debug);
        }
    }
}
