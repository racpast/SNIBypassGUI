using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32.TaskScheduler;
using HandyControl.Tools.Extension;
using Hardcodet.Wpf.TaskbarNotification;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Commands;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.IO;
using SNIBypassGUI.Common.Network;
using SNIBypassGUI.Common.Security;
using SNIBypassGUI.Common.System;
using SNIBypassGUI.Common.Text;
using SNIBypassGUI.Common.Tools;
using SNIBypassGUI.Common.UI;
using SNIBypassGUI.Consts;
using SNIBypassGUI.Models;
using SNIBypassGUI.Services;
using static SNIBypassGUI.Common.LogManager;
using Action = System.Action;
using MessageBox = HandyControl.Controls.MessageBox;
using Task = System.Threading.Tasks.Task;

namespace SNIBypassGUI.Views
{
    public partial class MainWindow : Window
    {
        #region Fields & Services
        private readonly StartupService _startupService = new();
        private readonly ProxyService _proxyService = new();

        // Timers
        private readonly DispatcherTimer _serviceStatusTimer = new() { Interval = TimeSpan.FromSeconds(3) };
        private readonly DispatcherTimer _tempFilesTimer = new() { Interval = TimeSpan.FromSeconds(10) };
        private readonly DispatcherTimer _adapterSwitchTimer = new() { Interval = TimeSpan.FromSeconds(5) };

        // Configuration Watcher
        public static FileSystemWatcher ConfigWatcher = new()
        {
            Filter = Path.GetFileName(PathConsts.ConfigJson),
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };
        private static readonly Timer _reloadDebounceTimer = new(500.0) { AutoReset = false };

        private bool _suppressAdapterSave = false;

        private volatile bool _isSwitchingAdapter = false;
        private bool _isSilentStartup = false;
        private bool _isInitialized = false;
        private bool _isBusy = false;

        private UpdateManifest _pendingUpdateManifest;
        private bool _pendingPortConflict = false;

        public ICommand TaskbarIconLeftClickCommand { get; }
        public static ImageSwitcherService BackgroundService { get; private set; }

        #endregion

        #region Constructor & Init

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            _adapterSwitchTimer.Tick += AdapterAutoSwitchTimer_Tick;
            _serviceStatusTimer.Tick += (_, _) => UpdateUiState();
            _tempFilesTimer.Tick += (_, _) => UpdateTempFilesSize();
            _reloadDebounceTimer.Elapsed += OnReloadDebounceTimerElapsed;

            BackgroundService = new ImageSwitcherService();
            BackgroundService.PropertyChanged += OnBackgroundChanged;
            CurrentImage.Source = BackgroundService.CurrentImage;

            TaskbarIconLeftClickCommand = new AsyncCommand(TaskbarIcon_LeftClick);
            TopBar.MouseLeftButtonDown += (o, e) => DragMove();
            TaskbarIcon.TrayBalloonTipClicked += (s, e) => ShowMainFromTray();

            MainTabControl.SelectionChanged += TabControl_SelectionChanged;

            if (string.IsNullOrEmpty(ConfigWatcher.Path))
            {
                if (Directory.Exists(PathConsts.DataDirectory))
                {
                    ConfigWatcher.Path = PathConsts.DataDirectory;
                    ConfigWatcher.Changed += OnConfigChanged;
                    ConfigWatcher.EnableRaisingEvents = true;
                }
            }

            TrayIconUtils.RefreshNotification();
        }

        public void RunInSilentMode()
        {
            _isSilentStartup = true;
            ShowInTaskbar = false;
            Visibility = Visibility.Hidden;
            _ = InitializeAppAsync(true);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isSilentStartup)
                await InitializeAppAsync(false);
        }

        private async Task InitializeAppAsync(bool isSilent)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            WindowTitle.Text += $" {AppConsts.CurrentVersion}";

            if (ConfigManager.Instance.Settings == null) await ConfigManager.Instance.LoadAsync();

            await Task.Run(_startupService.EnsureTaskScheduler);

            ApplySettings();

            await AddSwitchesToList();
            await InitializeSwitchConfig();

            if (isSilent) await Task.Delay(1000);

            await InitializeAdapterSelection();
            await CheckAndInstallService();

            if (ArgumentUtils.ContainsArgument(Environment.GetCommandLineArgs(), AppConsts.CleanUpArgument))
            {
                Environment.Exit(0);
                return;
            }

            if (isSilent)
            {
                bool started = await ExecuteStartServiceAsync(true);
                if (started) TaskbarIcon.ShowBalloonTip("启动成功", "服务已启动，正在后台运行。", BalloonIcon.Info);
                else if (!_pendingPortConflict) TaskbarIcon.ShowBalloonTip("启动失败", "服务启动失败，请打开主界面查看详情。", BalloonIcon.Error);

                if (ConfigManager.Instance.Settings.Program.AutoCheckUpdate)
                    await CheckUpdate(true, suppressNotification: _pendingPortConflict);
            }
            else
            {
                ShowInTaskbar = true;
                ShowMainFromTray();

                if (ConfigManager.Instance.Settings.Program.AutoCheckUpdate)
                    await CheckUpdate(false);
            }
            _adapterSwitchTimer.Start();
            _serviceStatusTimer.Start();

            TaskbarIcon.Visibility = Visibility.Visible;
        }

        private async Task InitializeSwitchConfig()
        {
            await Task.Run(() =>
            {
                var settings = ConfigManager.Instance.Settings.ProxySettings;
                bool changed = false;

                foreach (var item in CollectionConsts.Switches)
                {
                    if (!settings.ContainsKey(item.Id))
                    {
                        settings[item.Id] = true;
                        changed = true;
                    }
                }

                if (changed) ConfigManager.Instance.Save();
            });
        }

        #endregion

        #region Core Actions

        private async void StartBtn_Click(object sender, RoutedEventArgs e) => await ExecuteStartServiceAsync(false);
        private async void StopBtn_Click(object sender, RoutedEventArgs e) => await ExecuteStopServiceAsync(false);

        private async Task<bool> ExecuteStartServiceAsync(bool silent)
        {
            if (_isBusy) return false;

            _serviceStatusTimer.Stop(); // Pause timer to prevent status overwrite
            SetBusyState(true);

            try
            {
                string targetAdapterName = silent 
                    ? ConfigManager.Instance.Settings.Program.SpecifiedAdapter
                    : AdaptersCombo.SelectedItem?.ToString();

                if (string.IsNullOrEmpty(targetAdapterName))
                {
                    if (!silent) MessageBox.Show("请先选择一个有效的网络适配器。", "提示", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    else WriteLog("Silent start failed: No adapter specified in config.", LogLevel.Warning);
                    return false;
                }
                if (!await CheckAndHandlePortConflicts(silent)) return false;

                try
                {
                    await Task.Run(() =>
                    {
                        File.Copy(PathConsts.AcrylicConfigTemplate, PathConsts.AcrylicConfig, true);
                    });
                    WriteLog("Successfully overwritten AcrylicConfig with Template.", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    WriteLog($"Failed to overwrite config with template: {ex.Message}", LogLevel.Error, ex);
                    if (!silent) MessageBox.Show($"还原配置模板失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                await _proxyService.UpdateHostsFromConfigAsync();
                await _proxyService.StartAsync(status =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ServiceStatusText.Text = status;
                        ServiceStatusText.Foreground = Brushes.DarkOrange;
                    });
                });

                await Task.Run(NetworkUtils.FlushDNS);
                return true;
            }
            catch (Exception ex)
            {
                WriteLog($"Failed to start service: {ex.Message}", LogLevel.Error, ex);
                if (!silent) MessageBox.Show($"启动服务失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                SetBusyState(false);
                Dispatcher.Invoke(UpdateUiState);
                _serviceStatusTimer.Start();
            }
        }

        private async Task<bool> CheckAndHandlePortConflicts(bool silent)
        {
            int pid80 = PortUtils.FindPidForPort(80);
            int pid443 = PortUtils.FindPidForPort(443);

            if (pid80 != 0 || pid443 != 0)
            {
                if (silent)
                {
                    _pendingPortConflict = true;
                    WriteLog($"Ports 80(PID:{pid80})/443(PID:{pid443}) in use. Marked for UI prompt.", LogLevel.Warning);
                    TaskbarIcon.ShowBalloonTip("端口被占用", "检测到 80/443 端口被占用，请点击此处处理端口冲突。", BalloonIcon.Warning);
                    return false;
                }
                else
                {
                    string occupiedBy = (pid80 == 4 || pid443 == 4) ? "系统服务 (如 IIS)" : "其他程序";
                    var result = MessageBox.Show($"检测到 80 或 443 端口被{occupiedBy}占用。是否尝试自动释放端口？\n这将结束占用进程或停止相关服务。",
                        "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.Yes)
                    {
                        ServiceStatusText.Text = "端口占用清理中";
                        ServiceStatusText.Foreground = Brushes.Magenta;

                        await PortUtils.FreeTcpPortsAsync([80, 443]);

                        bool isFreed = false;
                        for (int i = 0; i < 6; i++)
                        {
                            if (!PortUtils.IsTcpPortInUse(80) && !PortUtils.IsTcpPortInUse(443))
                            {
                                isFreed = true;
                                break;
                            }
                            await Task.Delay(500);
                        }

                        if (isFreed)
                        {
                            MessageBox.Show("尝试释放端口失败，或者端口被顽固进程占用。\n请尝试手动关闭相关软件。", "清理失败", MessageBoxButton.OK, MessageBoxImage.Error);
                            WriteLog("Port 80 or 443 is in use. Nginx might fail to start.", LogLevel.Warning);
                            return false;
                        }
                        else
                        {
                            WriteLog("Ports freed successfully.", LogLevel.Info);
                            return true;
                        }
                    }
                    else return true;
                }
            }
            return true;
        }

        private async Task<bool> ExecuteStopServiceAsync(bool silent)
        {
            if (_isBusy) return false;

            _serviceStatusTimer.Stop();
            SetBusyState(true);
            bool success = false;

            try
            {
                await _proxyService.RemoveHostsRecordsAsync();
                await _proxyService.StopAsync(status =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ServiceStatusText.Text = status;
                        ServiceStatusText.Foreground = Brushes.DarkOrange;
                    });
                });

                await Task.Run(NetworkUtils.FlushDNS);
                success = true;
            }
            catch (Exception ex)
            {
                WriteLog($"Failed to stop service.", LogLevel.Error, ex);
                if (!silent) MessageBox.Show($"停止服务失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetBusyState(false);
                Dispatcher.Invoke(UpdateUiState);
                _serviceStatusTimer.Start();
            }

            return success;
        }

        private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyBtn.IsEnabled = UnchangeBtn.IsEnabled = false;
            ApplyBtn.Content = "正在应用更改…";

            UpdateConfigFromToggleButtons();
            await ConfigManager.Instance.SaveNowAsync();

            if (AcrylicUtils.IsAcrylicServiceRunning())
            {
                await AcrylicUtils.StopAcrylicService();
                await _proxyService.UpdateHostsFromConfigAsync();
                await AcrylicUtils.StartAcrylicService();
            }

            AcrylicUtils.RemoveAcrylicCacheFile();
            await Task.Run(NetworkUtils.FlushDNS);

            ApplyBtn.Content = "应用更改";
        }

        private void SetBusyState(bool isBusy)
        {
            _isBusy = isBusy;
            bool enabled = !isBusy;

            StartBtn.IsEnabled = enabled;
            StopBtn.IsEnabled = enabled;
            AutoSwitchAdapterBtn.IsEnabled = enabled;
            RefreshBtn.IsEnabled = enabled;

            bool autoSwitch = ConfigManager.Instance.Settings.Program.AutoSwitchAdapter;
            AdaptersCombo.IsEnabled = enabled && !autoSwitch;
        }

        #endregion

        #region UI Updates & Helper Methods

        private void UpdateUiState()
        {
            if (_isBusy) return;

            bool isNginxRunning = ProcessUtils.IsProcessRunning(AppConsts.NginxProcessName);
            bool isDnsRunning = AcrylicUtils.IsAcrylicServiceRunning();
            bool isAutoSwitch = ConfigManager.Instance.Settings.Program.AutoSwitchAdapter;
            bool hasSelectedAdapter = AdaptersCombo.SelectedItem != null;

            var (text, color) = (isNginxRunning, isDnsRunning) switch
            {
                (true, true) => ("主服务和DNS服务运行中", Brushes.ForestGreen),
                (true, false) => ("仅主服务运行中", Brushes.DarkOrange),
                (false, true) => ("仅DNS服务运行中", Brushes.DarkOrange),
                (false, false) => ("主服务与DNS服务未运行", Brushes.Red)
            };

            ServiceStatusText.Text = TaskbarIconServiceST.Text = text;
            ServiceStatusText.Foreground = TaskbarIconServiceST.Foreground = color;

            bool isRunning = isNginxRunning || isDnsRunning;

            StartBtn.IsEnabled = !isRunning && !_isBusy && hasSelectedAdapter;
            StopBtn.IsEnabled = isRunning && !_isBusy;
            AutoSwitchAdapterBtn.IsEnabled = !isRunning && !_isBusy;
            RefreshBtn.IsEnabled = !_isBusy;

            AdaptersCombo.IsEnabled = !isRunning && !_isBusy && !isAutoSwitch;
        }

        private void UpdateTempFilesSize()
        {
            long total = FileUtils.GetDirectorySize(PathConsts.LogDirectory) +
                            FileUtils.GetFileSize(PathConsts.NginxAccessLog) +
                            FileUtils.GetFileSize(PathConsts.NginxErrorLog) +
                            FileUtils.GetFileSize(PathConsts.AcrylicCache) +
                            FileUtils.GetDirectorySize(PathConsts.NginxCacheDirectory);
            CleanBtn.Content = $"清理临时文件 ({total.ToReadableSize()})";
        }

        private async Task UpdateYiyan()
        {
            try
            {
                string text = await NetworkUtils.GetAsync("https://v1.hitokoto.cn/?c=d");
                JObject repodata = JObject.Parse(text);
                TaskbarIconYiyan.Text = repodata["hitokoto"].ToString();
                TaskbarIconYiyanFrom.Text = $"—— {repodata["from_who"]}「{repodata["from"]}」";
            }
            catch (Exception ex)
            {
                WriteLog("Failed to fetch Hitokoto. Using default.", LogLevel.Error, ex);
                TaskbarIconYiyan.Text = AppConsts.DefaultYiyan;
                TaskbarIconYiyanFrom.Text = AppConsts.DefaultYiyanFrom;
            }
        }

        private async Task CheckAndInstallService()
        {
            try
            {
                int serviceState = ServiceUtils.CheckServiceState(AppConsts.DnsServiceName);
                if (serviceState == 0 || serviceState == 2)
                {
                    await AcrylicUtils.InstallAcrylicServiceAsync();
                }
                else if (serviceState == 1)
                {
                    string currentPath = ServiceUtils.GetServiceBinaryPath(AppConsts.DnsServiceName)?.Trim('"');
                    if (!string.IsNullOrEmpty(currentPath) && !string.Equals(currentPath, PathConsts.AcrylicServiceExe, StringComparison.OrdinalIgnoreCase))
                    {
                        WriteLog("DNS Service path changed. Reinstalling...", LogLevel.Info);
                        await AcrylicUtils.UninstallAcrylicServiceAsync();
                        await AcrylicUtils.InstallAcrylicServiceAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("Exception checking DNS service.", LogLevel.Error, ex);
                MessageBox.Show($"检查服务时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        #endregion

        #region Adapter Logic

        private async Task InitializeAdapterSelection()
        {
            _suppressAdapterSave = true;

            try
            {
                if (AdaptersCombo.Items.Count == 0) await UpdateAdaptersCombo();

                string configAdapter = ConfigManager.Instance.Settings.Program.SpecifiedAdapter;
                bool isRestored = false;

                if (!string.IsNullOrEmpty(configAdapter))
                {
                    if (AdaptersCombo.Items.Contains(configAdapter))
                    {
                        AdaptersCombo.SelectedItem = configAdapter;
                        isRestored = true;
                    }
                    else WriteLog($"Saved adapter '{configAdapter}' not found in current network interfaces.", LogLevel.Warning);
                }

                if (!isRestored)
                {
                    var active = await GetActiveAdapter();
                    if (active != null)
                    {
                        if (!AdaptersCombo.Items.Contains(active.FriendlyName)) await UpdateAdaptersCombo();
                        if (AdaptersCombo.Items.Contains(active.FriendlyName))
                        {
                            AdaptersCombo.SelectedItem = active.FriendlyName;

                            ConfigManager.Instance.Settings.Program.SpecifiedAdapter = active.FriendlyName;
                            ConfigManager.Instance.Save();
                        }
                    }
                    else
                    {
                        if (AdaptersCombo.Items.Count == 0)
                        {
                            WriteLog("No active network adapters found.", LogLevel.Warning);
                            if (MessageBox.Show("没有找到活动且可设置的网络适配器，您可能需要手动设置。\n点击“是”将为您展示有关帮助。", "警告", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
                                ProcessUtils.StartProcess(LinksConsts.AdapterNotFoundOrSetupFailedHelpLink, useShellExecute: true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog("Exception during adapter initialization.", LogLevel.Error, ex);
            }
            finally
            {
                _suppressAdapterSave = false;
            }
        }

        private async Task<NetworkAdapter> GetActiveAdapter()
        {
            uint? interfaceIndex = NetworkAdapterUtils.GetDefaultRouteInterfaceIndex();
            if (interfaceIndex != null)
            {
                var adapters = await NetworkAdapterUtils.GetNetworkAdaptersAsync(NetworkAdapterUtils.ScopeNeeded.FriendlyNameNotNullOnly);
                return adapters.FirstOrDefault(a => a.InterfaceIndex == interfaceIndex.Value);
            }
            return null;
        }

        private async Task UpdateAdaptersCombo()
        {
            var adapters = await NetworkAdapterUtils.GetNetworkAdaptersAsync(NetworkAdapterUtils.ScopeNeeded.FriendlyNameNotNullOnly);
            AdaptersCombo.Items.Clear();
            foreach (var adapter in adapters)
            {
                if (!string.IsNullOrEmpty(adapter.FriendlyName))
                    AdaptersCombo.Items.Add(adapter.FriendlyName);
            }
        }

        private async void AdapterAutoSwitchTimer_Tick(object sender, EventArgs e)
        {
            if (_isSwitchingAdapter || _isBusy) return;

            bool isServiceRunning = !ServiceStatusText.Text.Contains("未运行");
            bool isAutoMode = ConfigManager.Instance.Settings.Program.AutoSwitchAdapter;

            _isSwitchingAdapter = true;

            try
            {
                var allAdapters = await NetworkAdapterUtils.GetNetworkAdaptersAsync(NetworkAdapterUtils.ScopeNeeded.FriendlyNameNotNullOnly);
                var bestRouteIndex = NetworkAdapterUtils.GetDefaultRouteInterfaceIndex();
                var bestAdapter = bestRouteIndex.HasValue
                    ? allAdapters.FirstOrDefault(a => a.InterfaceIndex == bestRouteIndex.Value)
                    : null;

                if (isServiceRunning)
                {
                    string currentConfigured = ConfigManager.Instance.Settings.Program.SpecifiedAdapter;
                    bool shouldEmergencyStop = false;
                    string stopReason = "";

                    if (isAutoMode)
                    {
                        if (bestAdapter == null)
                        {
                            shouldEmergencyStop = true;
                            stopReason = "Network connection lost (Auto Mode)";
                        }
                    }
                    else
                    {
                        if (!allAdapters.Any(a => a.FriendlyName == currentConfigured))
                        {
                            shouldEmergencyStop = true;
                            stopReason = $"Configured adapter [{currentConfigured}] removed from system (Manual Mode)";
                        }
                    }

                    if (shouldEmergencyStop)
                    {
                        WriteLog(stopReason, LogLevel.Error);
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            TaskbarIcon.ShowBalloonTip("服务已停止", "检测到当前网络适配器丢失，已停止服务。", BalloonIcon.Error);
                            await ExecuteStopServiceAsync(true);
                            UpdateAdaptersCombo(allAdapters);
                        });
                        return;
                    }
                }

                if (isAutoMode && bestAdapter != null)
                {
                    string currentConfigured = ConfigManager.Instance.Settings.Program.SpecifiedAdapter;

                    // If the best adapter is not the currently configured one
                    if (bestAdapter.FriendlyName != currentConfigured)
                    {
                        WriteLog($"Auto-Switch: Detected {bestAdapter.FriendlyName} is better than {currentConfigured}. Switching...", LogLevel.Info);

                        if (isServiceRunning)
                        {
                            // Service is running: switch traffic
                            var oldAdapter = allAdapters.FirstOrDefault(d => d.FriendlyName == currentConfigured);
                            if (oldAdapter != null) await _proxyService.RestoreAdapterDNSAsync(oldAdapter);

                            await _proxyService.SetLoopbackDNSAsync(bestAdapter);
                            await Task.Run(NetworkUtils.FlushDNS);
                        }

                        // Update Config
                        ConfigManager.Instance.Settings.Program.SpecifiedAdapter = bestAdapter.FriendlyName;
                        ConfigManager.Instance.Save();

                        Dispatcher.Invoke(() =>
                        {
                            UpdateAdaptersCombo(allAdapters);
                            AdaptersCombo.SelectedItem = bestAdapter.FriendlyName;
                        });
                    }
                }

                if (!isServiceRunning)
                    Dispatcher.Invoke(() => UpdateAdaptersCombo(allAdapters));
            }
            catch (Exception ex)
            {
                WriteLog($"Adapter monitor error.", LogLevel.Error, ex);
            }
            finally
            {
                _isSwitchingAdapter = false;
            }
        }

        private void UpdateAdaptersCombo(List<NetworkAdapter> adapters)
        {
            var currentItems = AdaptersCombo.Items.OfType<string>().ToList();
            var newItems = adapters.Select(a => a.FriendlyName).ToList();

            if (currentItems.SequenceEqual(newItems)) return;

            var selected = AdaptersCombo.SelectedItem;
            AdaptersCombo.Items.Clear();
            foreach (var name in newItems) AdaptersCombo.Items.Add(name);

            if (selected != null && newItems.Contains(selected))
                AdaptersCombo.SelectedItem = selected;
        }

        private void AdaptersCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAdapterSave)
            {
                e.Handled = true;
                return;
            }

            if (AdaptersCombo.SelectedItem != null)
            {
                string newAdapter = AdaptersCombo.SelectedItem.ToString();
                string oldAdapter = ConfigManager.Instance.Settings.Program.SpecifiedAdapter;

                if (newAdapter != oldAdapter)
                {
                    ConfigManager.Instance.Settings.Program.SpecifiedAdapter = newAdapter;
                    ConfigManager.Instance.Save();
                }
            }
            e.Handled = true;
        }

        private void AutoSwitchAdapterBtn_Click(object sender, RoutedEventArgs e)
        {
            bool current = ConfigManager.Instance.Settings.Program.AutoSwitchAdapter;
            bool newState = !current;

            // Immediate UI update logic
            if (newState) AdapterAutoSwitchTimer_Tick(null, new EventArgs());
            ConfigManager.Instance.Settings.Program.AutoSwitchAdapter = newState;
            ConfigManager.Instance.Save();

            AutoSwitchAdapterBtn.Content = $"自动：{newState.ToOnOff()}";
            AdaptersCombo.IsEnabled = !newState;
        }

        #endregion

        #region Switch & Config Logic

        private async Task AddSwitchesToList()
        {
            if (!File.Exists(PathConsts.ProxyRules))
            {
                WriteLog("Switch config file not found.", LogLevel.Warning);
                return;
            }

            string json = await FileUtils.ReadAllTextAsync(PathConsts.ProxyRules);
            List<SwitchItem> switchItems = JsonConvert.DeserializeObject<List<SwitchItem>>(json);

            if (switchItems == null || switchItems.Count == 0) return;

            CollectionConsts.Switches.Clear();
            foreach (var item in switchItems)
            {
                item.FaviconImage = ImageUtils.Base64ToBitmapImage(item.Favicon);
                CollectionConsts.Switches.Add(item);
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                for (int i = Switchlist.Children.Count - 1; i >= 0; i--)
                {
                    var child = Switchlist.Children[i];
                    if (child != FirstColumnBorder && child != LastColumnBorder)
                        Switchlist.Children.RemoveAt(i);
                }

                Switchlist.RowDefinitions.Clear();

                foreach (var item in CollectionConsts.Switches) AddSwitchToUI(item);

                if (Switchlist.RowDefinitions.Count > 0)
                {
                    Grid.SetRowSpan(FirstColumnBorder, Switchlist.RowDefinitions.Count);
                    Grid.SetRowSpan(LastColumnBorder, Switchlist.RowDefinitions.Count);
                }
            });
        }

        private void AddSwitchToUI(SwitchItem item)
        {
            Switchlist.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            int rowIndex = Switchlist.RowDefinitions.Count - 1;

            Image favicon = new()
            {
                Source = item.FaviconImage,
                Height = 32.0,
                Width = 32.0,
                Margin = new Thickness(10.0, 10.0, 10.0, 5.0)
            };
            Grid.SetRow(favicon, rowIndex);
            Grid.SetColumn(favicon, 0);
            Switchlist.Children.Add(favicon);

            StackPanel contentPanel = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5.0, 3.0, 10.0, 3.0),
                Orientation = Orientation.Vertical
            };

            Grid headerGrid = new();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock nameBlock = new()
            {
                Text = item.DisplayName,
                FontSize = 16.0,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            };
            Grid.SetColumn(nameBlock, 0);
            headerGrid.Children.Add(nameBlock);


            if (item.Status != ItemBadgeStatus.None)
            {
                Border badgeBorder = new()
                {
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };

                TextBlock badgeText = new()
                {
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Padding = new Thickness(6, 0, 6, 0)
                };

                switch (item.Status)
                {
                    case ItemBadgeStatus.IPv6:
                        badgeBorder.BorderBrush = Brushes.DodgerBlue;
                        badgeBorder.ToolTip = "此站点需要 IPv6 网络环境";
                        badgeText.Text = "IPv6";
                        badgeText.Foreground = Brushes.DodgerBlue;
                        break;

                    case ItemBadgeStatus.KnownIssue:
                        badgeBorder.BorderBrush = Brushes.OrangeRed;
                        badgeBorder.ToolTip = "此站点适配尚存已知问题";
                        badgeText.Text = "已知问题";
                        badgeText.Foreground = Brushes.OrangeRed;
                        break;

                    case ItemBadgeStatus.Cloudflare:
                        badgeBorder.BorderBrush = Brushes.MediumPurple;
                        badgeBorder.ToolTip = "此站点托管于 Cloudflare 并启用 ECH";
                        badgeText.Text = "Cloudflare";
                        badgeText.Foreground = Brushes.MediumPurple;
                        break;
                }

                badgeBorder.Child = badgeText;
                Grid.SetColumn(badgeBorder, 1);
                headerGrid.Children.Add(badgeBorder);
            }

            contentPanel.Children.Add(headerGrid);

            TextBlock linksBlock = new()
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            };

            foreach (string part in item.Links)
            {
                if (part.Length <= 1)
                    linksBlock.Inlines.Add(new Run { Text = part, FontSize = 15.0, FontWeight = FontWeights.Bold });
                else
                {
                    Run run = new()
                    {
                        Text = part,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 249, 255)),
                        FontSize = 15.0,
                        Cursor = Cursors.Hand
                    };
                    run.PreviewMouseDown += LinkText_PreviewMouseDown;
                    linksBlock.Inlines.Add(run);
                }
            }
            contentPanel.Children.Add(linksBlock);

            Grid.SetRow(contentPanel, rowIndex);
            Grid.SetColumn(contentPanel, 1);
            Switchlist.Children.Add(contentPanel);

            string safeName = $"Toggle_{item.Id.ToSafeIdentifier()}";

            ToggleButton toggleButton = new()
            {
                Width = 40.0,
                Margin = new Thickness(5.0, 0.0, 5.0, 0.0),
                IsChecked = true,
                Style = (Style)FindResource("ToggleButtonSwitch"),
                Tag = item.Id
            };
            toggleButton.Click += ToggleButtonsClick;

            Grid.SetRow(toggleButton, rowIndex);
            Grid.SetColumn(toggleButton, 2);
            Switchlist.Children.Add(toggleButton);

            if (Switchlist.FindName(safeName) != null)
                Switchlist.UnregisterName(safeName);
            Switchlist.RegisterName(safeName, toggleButton);
        }

        private void ApplySettings()
        {
            var settings = ConfigManager.Instance.Settings;

            bool isNight = settings.Program.ThemeMode != ConfigConsts.LightMode;
            SwitchTheme(isNight);
            ThemeSwitchTB.IsChecked = isNight;

            foreach (var item in CollectionConsts.Switches)
            {
                string safeName = $"Toggle_{item.Id.ToSafeIdentifier()}";
                ToggleButton tb = (ToggleButton)Switchlist.FindName(safeName);

                if (settings.ProxySettings.TryGetValue(item.Id, out bool enabled))
                    tb?.SetCurrentValue(ToggleButton.IsCheckedProperty, enabled);
            }

            bool debugMode = settings.Advanced.DebugMode;
            DebugModeBtn.Content = $"调试模式：\n{debugMode.ToOnOff()}";

            bool guiDebug = settings.Advanced.GUIDebug;
            GUIDebugBtn.Content = $"GUI调试：\n{guiDebug.ToOnOff()}";

            bool acrylicDebug = settings.Advanced.AcrylicDebug;
            AcrylicDebugBtn.Content = $"DNS调试：\n{acrylicDebug.ToOnOff()}";

            if (!debugMode)
            {
                DisableLog();
                TailUtils.StopTracking(GetLogPath()).GetAwaiter();
                TailUtils.StopTracking(PathConsts.NginxAccessLog).GetAwaiter();
                TailUtils.StopTracking(PathConsts.NginxErrorLog).GetAwaiter();
                TailUtils.StopTracking(AcrylicUtils.GetLogPath()).GetAwaiter();
                FileUtils.ClearFolder(PathConsts.TempDirectory);
            }

            if (!guiDebug) DisableLog();
            if (!acrylicDebug) AcrylicUtils.DisableAcrylicServiceHitLog();

            AcrylicDebugBtn.IsEnabled = GUIDebugBtn.IsEnabled = TraceNginxLogBtn.IsEnabled = debugMode;

            bool autoCheckUpdate = settings.Program.AutoCheckUpdate;
            AutoCheckUpdateBtn.Content = $"自动检查更新：{autoCheckUpdate.ToOnOff()}";

            bool autoSwitch = settings.Program.AutoSwitchAdapter;
            AutoSwitchAdapterBtn.Content = $"自动：{autoSwitch.ToOnOff()}";
            AdaptersCombo.IsEnabled = !autoSwitch;
        }

        private void UpdateConfigFromToggleButtons()
        {
            foreach (var item in CollectionConsts.Switches)
            {
                string safeName = $"Toggle_{item.Id.ToSafeIdentifier()}";
                ToggleButton tb = (ToggleButton)Switchlist.FindName(safeName);
                if (tb != null)
                    ConfigManager.Instance.Settings.ProxySettings[item.Id] = tb.IsChecked.GetValueOrDefault();
            }
        }

        private static void OnConfigChanged(object source, FileSystemEventArgs e)
        {
            _reloadDebounceTimer.Stop();
            _reloadDebounceTimer.Start();
        }

        private void OnReloadDebounceTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(async () =>
            {
                try
                {
                    if (File.Exists(PathConsts.ConfigJson))
                    {
                        DateTime fileTime = File.GetLastWriteTime(PathConsts.ConfigJson);
                        if ((fileTime - ConfigManager.Instance.LastSaveTime).Duration() < TimeSpan.FromSeconds(2))
                        {
                            WriteLog("Ignored internal config save event.", LogLevel.Debug);
                            return;
                        }
                    }

                    WriteLog("Configuration file changed externally, reloading...", LogLevel.Info);
                    await ConfigManager.Instance.LoadAsync();
                    ApplySettings();
                    WriteLog("Hot reload complete.", LogLevel.Info);
                }
                catch (Exception ex) { WriteLog("Error during hot reload.", LogLevel.Error, ex); }
            });
        }

        #endregion

        #region Cleanup, Uninstall & Update

        private async void CleanBtn_Click(object sender, RoutedEventArgs e)
        {
            CleanBtn.IsEnabled = false;
            _tempFilesTimer.Stop();
            CleanBtn.Content = "服务停止中…";

            await _proxyService.StopAsync(null);

            CleanBtn.Content = "清理中…";
            await Task.Run(async () =>
            {
                try
                {
                    string[] tempfiles = [PathConsts.NginxAccessLog, PathConsts.NginxErrorLog, PathConsts.AcrylicCache, AcrylicUtils.GetLogPath()];
                    foreach (string path in tempfiles)
                    {
                        await TailUtils.StopTracking(path);
                        FileUtils.TryDelete(path, 5, 500);
                    }

                    if (IsLogEnabled)
                    {
                        Dispatcher.Invoke(() => MessageBox.Show("GUI 调试模式已启用，日志清理操作已跳过。如需清理日志文件，请先关闭 GUI 调试模式。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk));
                        foreach (string file in Directory.GetFiles(PathConsts.LogDirectory))
                            if (file != GetLogPath()) FileUtils.TryDelete(file, 5, 500);
                    }
                    else
                    {
                        await TailUtils.StopTracking(GetLogPath());
                        FileUtils.ClearFolder(PathConsts.LogDirectory);
                    }
                }
                catch (Exception ex) { WriteLog("Cleanup error.", LogLevel.Error, ex); }
            });

            WriteLog("Cleanup complete.", LogLevel.Info);
            _tempFilesTimer.Start();
            MessageBox.Show("日志及缓存已清理完成，请重新启动服务。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            UpdateTempFilesSize();
            CleanBtn.IsEnabled = true;
        }

        private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("此操作将从系统中彻底移除本程序，并还原对系统设置的所有更改。\n是否确认继续卸载？", "卸载", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                UninstallBtn.Content = "卸载中…";
                UninstallBtn.IsEnabled = false;
                try
                {
                    CertificateUtils.UninstallCertificate(AppConsts.CertificateThumbprint);
                    await ExecuteStopServiceAsync(true);

                    using (TaskService ts = new())
                        if (ts.GetTask(AppConsts.TaskName) != null) ts.RootFolder.DeleteTask(AppConsts.TaskName, true);

                    var adapters = await NetworkAdapterUtils.GetNetworkAdaptersAsync(NetworkAdapterUtils.ScopeNeeded.FriendlyNameNotNullOnly);
                    var activeAdapter = adapters.FirstOrDefault(a => a.FriendlyName == ConfigManager.Instance.Settings.Program.SpecifiedAdapter);
                    if (activeAdapter != null) await _proxyService.RestoreAdapterDNSAsync(activeAdapter);

                    await Task.Run(NetworkUtils.FlushDNS);
                    await TailUtils.StopTracking();
                    await AcrylicUtils.UninstallAcrylicServiceAsync();
                    BackgroundService.Cleanup();

                    FileUtils.TryDelete(PathConsts.DataDirectory, 5, 500);
                    FileUtils.TryDelete(PathConsts.TempDirectory, 5, 500);

                    string bat = $@"@echo off
                                    timeout /t 1 /nobreak >nul
                                    taskkill /f /pid {Process.GetCurrentProcess().Id} >nul 2>&1
                                    taskkill /f /im ""tail.exe"" >nul 2>&1
                                    timeout /t 1 /nobreak >nul
                                    del /f /q ""{PathConsts.CurrentExe}"" >nul 2>&1
                                    if exist ""{PathConsts.CurrentExe}"" (
                                    echo MsgBox ""卸载失败，请手动删除文件！"", 48, ""警告"" > ""%temp%\temp.vbs""
                                    cscript /nologo ""%temp%\temp.vbs"" >nul
                                    del ""%temp%\temp.vbs""
                                    ) else (
                                    echo MsgBox ""卸载成功！"", 64, ""提示"" > ""%temp%\temp.vbs""
                                    cscript /nologo ""%temp%\temp.vbs"" >nul
                                    del ""%temp%\temp.vbs""
                                    )
                                    start /b cmd /c del ""%~f0""";

                    string batPath = Path.Combine(Path.GetTempPath(), "uninstall_snibypassgui.bat");
                    await FileUtils.WriteAllTextAsync(batPath, bat, Encoding.Default);
                    Process.Start(new ProcessStartInfo { FileName = batPath, WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true });
                    Exit(true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"卸载时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
                }
            }
        }

        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            UpdateBtn.IsEnabled = false;
            UpdateBtn.Content = "获取信息…";
            try
            {
                WriteLog("Checking for updates...", LogLevel.Info);

                string json = await NetworkUtils.GetAsync(LinksConsts.LatestVersionJson);
                UpdateManifest manifest = JsonConvert.DeserializeObject<UpdateManifest>(json) ?? throw new Exception("Failed to parse update manifest.");

                WriteLog($"Remote version: {manifest.Version}, Current: {AppConsts.CurrentVersion}", LogLevel.Info);

                bool exeUpdated = false;
                bool assetsUpdated = false;

                if (manifest.Version != AppConsts.CurrentVersion && manifest.Executable != null && manifest.Executable.UpdateRequired)
                {
                    WriteLog("New executable version found. Starting update...", LogLevel.Info);
                    UpdateBtn.Content = "正在更新…";
                    await UpdateExecutable(manifest.Executable);
                    exeUpdated = true;
                    WriteLog("Executable update completed.", LogLevel.Info);
                }

                if (manifest.Assets != null && manifest.Assets.Count > 0)
                    assetsUpdated = await SyncAssets(manifest.Assets);

                if (exeUpdated || assetsUpdated)
                {
                    WriteLog($"[Update] Update finished. ExeUpdated: {exeUpdated}, AssetsUpdated: {assetsUpdated}. Restarting...", LogLevel.Info);

                    UpdateBtn.Content = "更新完成";
                    string msg = exeUpdated
                        ? $"主程序已更新至 {manifest.Version}，即将重启。"
                        : "数据文件已同步，即将重启以应用更改。";

                    MessageBox.Show(msg, "更新完成", MessageBoxButton.OK, MessageBoxImage.Asterisk);

                    ProcessUtils.StartProcess(PathConsts.CurrentExe, $"{AppConsts.WaitForParentArgument} {Process.GetCurrentProcess().Id}");
                    ExitBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
                else
                {
                    WriteLog("System is already up to date.", LogLevel.Info);
                    MessageBox.Show("所有文件已同步，当前已是最新版本。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Update exception.", LogLevel.Error, ex);
                MessageBox.Show($"更新数据时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
            finally
            {
                UpdateBtn.IsEnabled = true;
                if (UpdateBtn.Content.ToString() != "更新完成") UpdateBtn.Content = "更新数据";
            }
        }

        private async Task UpdateExecutable(ExecutableInfo exeInfo)
        {
            FileUtils.EnsureDirectoryExists(PathConsts.UpdateDirectory);
            FileUtils.ClearFolder(PathConsts.UpdateDirectory);
            string zipPath = $"update_{Guid.NewGuid():N}.zip";
            try
            {
                using (FileStream fs = new(zipPath, FileMode.Create, FileAccess.Write))
                {
                    int partIndex = 1;
                    foreach (string partUrl in exeInfo.Parts)
                    {
                        UpdateProgressText($"下载分片 ({partIndex}/{exeInfo.Parts.Count})");
                        byte[] data = await NetworkUtils.GetByteArrayAsync(partUrl, 120.0);
                        await fs.WriteAsync(data, 0, data.Length);
                        partIndex++;
                    }
                }

                UpdateProgressText("正在解压…");
                ZipFile.ExtractToDirectory(zipPath, PathConsts.UpdateDirectory);

                if (!File.Exists(PathConsts.NewVersionExe)) throw new FileNotFoundException("Main executable not found after extraction!");

                string downloadedHash = FileUtils.CalculateFileHash(PathConsts.NewVersionExe);
                if (!string.Equals(downloadedHash, exeInfo.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Hash mismatch! Expected: {exeInfo.Hash} Actual: {downloadedHash}");

                FileUtils.TryDelete(PathConsts.OldVersionExe, 5, 500);
                File.Move(PathConsts.CurrentExe, PathConsts.OldVersionExe);
                File.Move(PathConsts.NewVersionExe, PathConsts.CurrentExe);
                FileUtils.TryDelete(PathConsts.UpdateDirectory, 5, 500);
            }
            finally
            {
                FileUtils.TryDelete(zipPath, 5, 500);
            }
        }

        private async Task<bool> SyncAssets(List<AssetInfo> assets)
        {
            bool hasChanges = false;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            WriteLog($"Starting sync for {assets.Count} assets...", LogLevel.Info);

            foreach (AssetInfo asset in assets)
            {
                string localPath = Path.Combine(baseDir, asset.Path);
                bool needDownload = true;

                if (File.Exists(localPath))
                {
                    if (string.Equals(FileUtils.CalculateFileHash(localPath), asset.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        needDownload = false;
                        WriteLog($"Asset skipped (UpToDate): {asset.Path}", LogLevel.Debug);
                    }
                }

                if (needDownload)
                {
                    WriteLog($"Downloading asset: {asset.Path}", LogLevel.Info);

                    UpdateProgressText($"正在同步…");
                    FileUtils.EnsureDirectoryExists(Path.GetDirectoryName(localPath));

                    bool result = await NetworkUtils.TryDownloadFile(asset.Url, localPath, (p) => { }, 30.0);
                    if (!result)
                    {
                        WriteLog($"[Update] Failed to download asset: {asset.Path}", LogLevel.Error);
                        throw new Exception($"Failed to download asset {asset.Path}");
                    }

                    WriteLog($"Asset downloaded successfully: {asset.Path}", LogLevel.Info);
                    hasChanges = true;
                }
            }

            return hasChanges;
        }

        private async Task CheckUpdate(bool silent, bool suppressNotification = false)
        {
            try
            {
                string json = await NetworkUtils.GetAsync(LinksConsts.LatestVersionJson);
                UpdateManifest manifest = JsonConvert.DeserializeObject<UpdateManifest>(json);
                if (manifest != null)
                {
                    bool needUpdate = false;
                    string tipMessage = "";

                    if (manifest.Version != AppConsts.CurrentVersion)
                    {
                        needUpdate = true;
                        tipMessage = $"发现主程序新版本 {manifest.Version}";
                    }
                    else if (manifest.Assets != null)
                    {
                        bool assetsChanged = await Task.Run(() =>
                        {
                            foreach (var asset in manifest.Assets)
                            {
                                string localPath = Path.Combine(PathConsts.CurrentDirectory, asset.Path);
                                if (!File.Exists(localPath) || !string.Equals(FileUtils.CalculateFileHash(localPath), asset.Hash, StringComparison.OrdinalIgnoreCase))
                                    return true;
                            }
                            return false;
                        });

                        if (assetsChanged)
                        {
                            needUpdate = true;
                            tipMessage = "发现新的配置或数据文件更新";
                        }
                    }

                    if (needUpdate)
                    {
                        if (!silent)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (MessageBox.Show(tipMessage + "！\n是否立即更新？", "发现更新", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                                    UpdateBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            });
                        }
                        else
                        {
                            _pendingUpdateManifest = manifest;
                            if (!suppressNotification) TaskbarIcon.ShowBalloonTip("发现更新", $"{tipMessage}，点击查看详情。", BalloonIcon.Info);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Auto-update check failed.", LogLevel.Error, ex);
            }
        }

        private void UpdateProgressText(string text) => Dispatcher.Invoke(() => UpdateBtn.Content = text);

        #endregion

        #region Other Events & Animations

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            RefreshBtn.IsEnabled = false;
            Dispatcher.Invoke(UpdateUiState);
            await ConfigManager.Instance.LoadAsync();
            ApplySettings();
            WriteLog("Manual refresh triggered.", LogLevel.Info);
            RefreshBtn.IsEnabled = true;
        }

        private async void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("此操作将把所有配置文件及规则数据重置为内置版本。\n该功能适用于修复因配置错误或文件损坏导致的问题。\n是否确认重置数据？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ResetBtn.IsEnabled = false;
                ResetBtn.Content = "还原中…";
                try
                {
                    await _proxyService.StopAsync(null);
                    string[] files = [PathConsts.NginxConfig, PathConsts.SNIBypassCrt, PathConsts.ProxyRules, PathConsts.AcrylicHosts, PathConsts.NginxCacheDirectory, PathConsts.ProxyRules, PathConsts.ConfigJson];
                    FileUtils.TryDelete(files);
                    _startupService.InitializeDirectoriesAndFiles();
                    await ConfigManager.Instance.LoadAsync();
                    ApplySettings();
                }
                catch (Exception ex)
                {
                    WriteLog("Reset exception.", LogLevel.Error, ex);
                    MessageBox.Show($"还原数据时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    ResetBtn.IsEnabled = true;
                    ResetBtn.Content = "还原数据";
                }
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != sender) return;
            if (sender is TabControl tabControl && tabControl.SelectedItem is TabItem selectedItem)
            {
                string header = selectedItem.Header.ToString();
                if (header == "设置")
                {
                    UpdateTempFilesSize();
                    if (!_tempFilesTimer.IsEnabled) _tempFilesTimer.Start();
                }
                else
                {
                    if (_tempFilesTimer.IsEnabled) _tempFilesTimer.Stop();
                    Dispatcher.Invoke(UpdateUiState);
                }
            }
        }

        private async void TraceNginxLogBtn_Click(object sender, RoutedEventArgs e)
        {
            TraceNginxLogBtn.IsEnabled = false;
            await Task.Run(async () =>
            {
                await TailUtils.StopTracking(PathConsts.NginxAccessLog);
                await TailUtils.StopTracking(PathConsts.NginxErrorLog);
                TailUtils.StartTracking(PathConsts.NginxAccessLog, "AccessLog");
                TailUtils.StartTracking(PathConsts.NginxErrorLog, "ErrorLog");
            });
            TraceNginxLogBtn.IsEnabled = true;
        }

        private void InstallCertBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CertificateUtils.IsCertificateInstalled(AppConsts.CertificateThumbprint))
                    CertificateUtils.UninstallCertificate(AppConsts.CertificateThumbprint);

                CertificateUtils.InstallCertificate(PathConsts.CA);
                MessageBox.Show("证书安装成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            catch (Exception ex)
            {
                WriteLog($"Certificate installation error.", LogLevel.Error, ex);
                MessageBox.Show($"安装证书时发生异常：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        private void ToggleButtonsClick(object sender, RoutedEventArgs e) =>
            ApplyBtn.IsEnabled = UnchangeBtn.IsEnabled = true;

        private async void UnchangeBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyBtn.IsEnabled = UnchangeBtn.IsEnabled = false;
            await ConfigManager.Instance.LoadAsync();
            ApplySettings();
        }

        private void AllOnBtn_Click(object sender, RoutedEventArgs e)
        {
            bool changed = false;
            foreach (var item in CollectionConsts.Switches)
            {
                string safeName = $"Toggle_{item.Id.ToSafeIdentifier()}";
                ToggleButton tb = (ToggleButton)Switchlist.FindName(safeName);

                if (tb != null && !tb.IsChecked.GetValueOrDefault())
                {
                    tb.IsChecked = true;
                    changed = true;
                }
            }

            if (changed) ApplyBtn.IsEnabled = UnchangeBtn.IsEnabled = true;
        }

        private void AllOffBtn_Click(object sender, RoutedEventArgs e)
        {
            bool changed = false;
            foreach (var item in CollectionConsts.Switches)
            {
                string safeName = $"Toggle_{item.Id.ToSafeIdentifier()}";
                ToggleButton tb = (ToggleButton)Switchlist.FindName(safeName);

                if (tb != null && tb.IsChecked.GetValueOrDefault())
                {
                    tb.IsChecked = false;
                    changed = true;
                }
            }

            if (changed) ApplyBtn.IsEnabled = UnchangeBtn.IsEnabled = true;
        }

        private void CustomBkgBtn_Click(object sender, RoutedEventArgs e)
        {
            CustomBkgBtn.IsEnabled = false;
            AnimateWindow(1.0, 0.0, () =>
            {
                Hide();
                HideContent();
                new CustomBackgroundWindow().ShowDialog();
                Show();
                AnimateWindow(0.0, 1.0, () => { Activate(); ShowContent(); });
                CustomBkgBtn.IsEnabled = true;
            });
        }

        private void DefaultBkgBtn_Click(object sender, RoutedEventArgs e)
        {
            FileUtils.ClearFolder(PathConsts.BackgroundDirectory);
            foreach (var pair in CollectionConsts.DefaultBackgroundMap)
                FileUtils.ExtractResourceToFile(pair.Value, Path.Combine(PathConsts.BackgroundDirectory, pair.Key));

            var imgs = AppConsts.ImageExtensions.SelectMany(ext => Directory.GetFiles(PathConsts.BackgroundDirectory, "*" + ext));

            List<string> imageOrder = [.. imgs.Select(FileUtils.CalculateFileHash)];

            var config = ConfigManager.Instance.Settings.Background;
            config.ImageOrder = imageOrder;
            config.ChangeInterval = 15;
            config.ChangeMode = ConfigConsts.SequentialMode;
            ConfigManager.Instance.Save();

            BackgroundService.CleanAllCache();
            BackgroundService.ReloadConfig();
            BackgroundService.ValidateCurrentImage();
        }

        private void LinkText_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            string url = (sender as TextBlock)?.Text ?? (sender as Run)?.Text;
            if (!string.IsNullOrEmpty(url))
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://")) url = "https://" + url;
                ProcessUtils.StartProcess(url, "", "", true, false);
            }
        }

        private void MenuItem_ShowMainWin_Click(object sender, RoutedEventArgs e) => ShowMainFromTray();

        private async void MenuItem_StartService_Click(object sender, RoutedEventArgs e)
        {
            bool success = await ExecuteStartServiceAsync(false);
            if (success) TaskbarIcon.ShowBalloonTip("启动成功", "服务已启动，正在后台运行。", BalloonIcon.Info);
            else TaskbarIcon.ShowBalloonTip("启动失败", "服务启动失败，请打开主界面查看详情。", BalloonIcon.Error);
        }

        private async void MenuItem_StopService_Click(object sender, RoutedEventArgs e)
        {
            bool success = await ExecuteStopServiceAsync(false);
            if (success) TaskbarIcon.ShowBalloonTip("停止成功", "服务已成功停止。", BalloonIcon.Info);
            else TaskbarIcon.ShowBalloonTip("停止失败", "服务停止失败，请打开主界面查看详情。", BalloonIcon.Error);
        }

        private void MenuItem_ExitTool_Click(object sender, RoutedEventArgs e) =>
            ExitBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        public async Task TaskbarIcon_LeftClick()
        {
            MenuItem_ShowMainWin.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await UpdateYiyan();
        }

        private void MinimizeToTrayBtn_Click(object sender, RoutedEventArgs e)
        {
            MinimizeToTrayBtn.IsEnabled = false;
            AnimateWindow(1.0, 0.0, () =>
            {
                Hide();
                HideContent();
                TaskbarIcon.ShowBalloonTip("已最小化", "程序已最小化到托盘，点击图标显示主界面。", BalloonIcon.Info);
                MinimizeToTrayBtn.IsEnabled = true;
            });
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            MinimizeToTrayBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        private void MenuItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is MenuItem item)
            {
                string color = item.Header.ToString() switch
                {
                    "停止服务" => "#FFFF0000",
                    "启动服务" => "#FF2BFF00",
                    "退出工具" => "#FFFF00C7",
                    _ => "#00A2FF"
                };
                item.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                item.Header = $"『{item.Header}』";
                item.FontSize += 2.0;
            }
        }

        private void MenuItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is MenuItem item)
            {
                item.Foreground = new SolidColorBrush(Colors.White);
                item.FontSize -= 2.0;
                string header = item.Header.ToString();
                if (header.StartsWith("『") && header.EndsWith("』"))
                    item.Header = header.Substring(1, header.Length - 2);
            }
        }

        private void DebugModeBtn_Click(object sender, RoutedEventArgs e)
        {
            bool current = ConfigManager.Instance.Settings.Advanced.DebugMode;
            if (!current && MessageBox.Show("调试模式仅用于开发和问题诊断，不当启用可能导致非预期行为。建议仅在开发者指导下开启。\n是否继续启用？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
                ConfigManager.Instance.Settings.Advanced.DebugMode = true;
            else
            {
                ConfigManager.Instance.Settings.Advanced.DebugMode = false;
                ConfigManager.Instance.Settings.Advanced.GUIDebug = false;
                ConfigManager.Instance.Settings.Advanced.AcrylicDebug = false;
            }
            ConfigManager.Instance.Save();
            ApplySettings();
        }

        private async void AcrylicDebugBtn_Click(object sender, RoutedEventArgs e)
        {
            bool current = ConfigManager.Instance.Settings.Advanced.AcrylicDebug;
            bool newState = !current;

            string message = newState
                ? "开启 DNS 服务调试功能，可帮助诊断网络流量走向相关问题。\n如果服务正在运行，将自动重启服务以应用更改。\n是否确认开启？"
                : "是否关闭 DNS 服务调试功能？\n如果服务正在运行，将自动重启服务以应用更改。";

            if (MessageBox.Show(message, "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ConfigManager.Instance.Settings.Advanced.AcrylicDebug = newState;
                await ConfigManager.Instance.SaveNowAsync();

                ApplySettings();

                bool isServiceRunning = AcrylicUtils.IsAcrylicServiceRunning();

                if (newState) AcrylicUtils.EnableAcrylicServiceHitLog();
                else AcrylicUtils.DisableAcrylicServiceHitLog();

                if (isServiceRunning)
                {
                    SetBusyState(true);
                    ServiceStatusText.Text = "DNS服务重启中";

                    try
                    {
                        await TailUtils.StopTracking(AcrylicUtils.GetLogPath());

                        await AcrylicUtils.StopAcrylicService();
                        await AcrylicUtils.StartAcrylicService();

                        if (newState)
                        {
                            await Task.Delay(500);
                            TailUtils.StartTracking(AcrylicUtils.GetLogPath(), "HitLog");
                        }

                        await Task.Run(NetworkUtils.FlushDNS);
                    }
                    catch (Exception ex)
                    {
                        WriteLog("Error applying DNS debug configuration.", LogLevel.Error, ex);
                        MessageBox.Show($"应用更改失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        SetBusyState(false);
                        UpdateUiState();
                    }
                }
                else if (!newState) await TailUtils.StopTracking(AcrylicUtils.GetLogPath());
            }
        }

        private async void GUIDebugBtn_Click(object sender, RoutedEventArgs e)
        {
            bool current = ConfigManager.Instance.Settings.Advanced.GUIDebug;

            if (!current && MessageBox.Show("开启 GUI 调试模式有助于更精准地定位问题，但生成日志会增加一定的性能开销，建议在不使用时及时关闭。\n开启后程序将自动退出，重启后生效。\n请您在重启并复现问题后，将相关信息提交给开发者。\n是否确认开启 GUI 调试模式并重启程序？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                GUIDebugBtn.IsEnabled = false;

                ConfigManager.Instance.Settings.Advanced.GUIDebug = true;
                await ConfigManager.Instance.SaveNowAsync();

                ProcessUtils.StartProcess(PathConsts.CurrentExe, $"{AppConsts.WaitForParentArgument} {Process.GetCurrentProcess().Id}", "", false, false);
                Exit(false);
            }
            else if (current)
            {
                ConfigManager.Instance.Settings.Advanced.GUIDebug = false;
                ConfigManager.Instance.Save();
                ApplySettings();
            }
        }

        private void EditHostsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileUtils.EnsureFileExists(PathConsts.SystemHosts);
                ProcessUtils.StartProcess("notepad.exe", PathConsts.SystemHosts, useShellExecute:true);
            }
            catch (Exception ex)
            {
                WriteLog($"Exception opening Hosts file: {PathConsts.SystemHosts}", LogLevel.Error, ex);
                MessageBox.Show($"打开 {PathConsts.SystemHosts} 时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FeedbackBtn_Click(object sender, RoutedEventArgs e)
        {
            string message = "如果您在使用过程中遇到问题或有任何建议，欢迎通过以下方式联系我们：\n" +
                                    "● QQ 交流群：946813204\n" +
                                    "● 电子邮件：hi@racpast.com 或 racpast@gmail.com\n" +
                                    "是否立即跳转加入 QQ 群？";

            if (MessageBox.Show(message, "反馈与建议", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                ProcessUtils.StartProcess(LinksConsts.QqGroupJoinUrl, useShellExecute:true);
        }

        private void HelpBtn_HowToFindActiveAdapter_Click(object sender, RoutedEventArgs e) =>
            ProcessUtils.StartProcess(LinksConsts.AdapterUncertaintyHelpLink, useShellExecute: true);

        private void AutoCheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            bool current = ConfigManager.Instance.Settings.Program.AutoCheckUpdate;
            ConfigManager.Instance.Settings.Program.AutoCheckUpdate = !current;
            ConfigManager.Instance.Save();
            ApplySettings();
        }

        private void ThemeSwitchTB_Checked(object sender, RoutedEventArgs e)
        {
            SwitchTheme(true);
            ConfigManager.Instance.Settings.Program.ThemeMode = ConfigConsts.DarkMode;
            ConfigManager.Instance.Save();
        }

        private void ThemeSwitchTB_Unchecked(object sender, RoutedEventArgs e)
        {
            SwitchTheme(false);
            ConfigManager.Instance.Settings.Program.ThemeMode = ConfigConsts.LightMode;
            ConfigManager.Instance.Save();
        }

        public void SwitchTheme(bool isNightMode)
        {
            Color targetBackground = isNightMode ? Color.FromArgb(112, 97, 97, 97) : Color.FromArgb(112, 255, 255, 255);
            Color targetBorder = isNightMode ? Colors.Black : Colors.White;
            Color targetSwitchText = isNightMode ? (Color)ColorConverter.ConvertFromString("#C4C9D4") : (Color)ColorConverter.ConvertFromString("#F3C62B");

            Application.Current.Resources["BackgroundColor"] = targetBackground;
            Application.Current.Resources["BorderColor"] = targetBorder;

            TimeSpan duration = TimeSpan.FromSeconds(0.8);
            QuadraticEase easing = new() { EasingMode = EasingMode.EaseInOut };

            if (Application.Current.Resources["BackgroundBrush"] is SolidColorBrush bgBrush)
                bgBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(targetBackground, duration) { EasingFunction = easing });

            if (Application.Current.Resources["BorderBrush"] is SolidColorBrush borderBrush)
                borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(targetBorder, duration) { EasingFunction = easing });

            if (ThemeSwitchTBText.Foreground is SolidColorBrush textBrush)
                textBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(targetSwitchText, duration) { EasingFunction = easing });
        }

        private void OnBackgroundChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "CurrentImage") return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                NextImage.Opacity = 0.0;
                NextImage.Source = BackgroundService.CurrentImage;

                DoubleAnimation fadeOut = new(1.0, 0.0, TimeSpan.FromSeconds(1.0));
                DoubleAnimation fadeIn = new(0.0, 1.0, TimeSpan.FromSeconds(1.0));

                CurrentImage.BeginAnimation(OpacityProperty, fadeOut);
                NextImage.BeginAnimation(OpacityProperty, fadeIn);

                (NextImage, CurrentImage) = (CurrentImage, NextImage);
            }));
        }

        protected override void OnClosed(EventArgs e)
        {
            BackgroundService.Cleanup();
            BackgroundService.PropertyChanged -= OnBackgroundChanged;
            base.OnClosed(e);
        }

        private static bool IsAnyDialogOpen()
        {
            foreach (Window window in Application.Current.Windows)
                if (window is CustomBackgroundWindow) return true;
            return false;
        }

        private void ShowMainFromTray()
        {
            _isSilentStartup = false;
            ShowInTaskbar = true;

            if (Visibility == Visibility.Visible && Opacity > 0.9)
            {
                Activate();
                return;
            }

            Visibility = Visibility.Visible;
            Show();
            AnimateWindow(0.0, 1.0, () => {
                Activate();
                ShowContent();
                UpdateUiState();
                ProcessPendingStates();
            });
        }

        private async void ProcessPendingStates()
        {
            if (_pendingPortConflict)
            {
                _pendingPortConflict = false;
                await ExecuteStartServiceAsync(false);
            }

            if (_pendingUpdateManifest != null)
            {
                var manifest = _pendingUpdateManifest;
                _pendingUpdateManifest = null;

                string tipMessage = manifest.Version != AppConsts.CurrentVersion
                    ? $"发现主程序新版本 {manifest.Version}"
                    : "发现新的配置或数据文件更新";

                if (MessageBox.Show(tipMessage + "！\n是否立即更新？", "发现更新", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    UpdateBtn.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }
        }

        private void AnimateWindow(double from, double to, Action onCompleted = null)
        {
            DoubleAnimation animation = new()
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(0.8),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            if (onCompleted != null) animation.Completed += (s, e) => onCompleted();
            BeginAnimation(OpacityProperty, animation);
        }

        private void HideContent()
        {
            TransitioningContentControlA.Hide();
            TransitioningContentControlB.Hide();
            TransitioningContentControlC.Hide();
        }

        private void ShowContent()
        {
            TransitioningContentControlA.Show();
            TransitioningContentControlB.Show();
            TransitioningContentControlC.Show();
        }

        private void EnableAutoStartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _startupService.CreateTask(AppConsts.TaskName, "开机启动 SNIBypassGUI 并启动服务。", "SNIBypassGUI", PathConsts.CurrentExe, AppConsts.AutoStartArgument);
                MessageBox.Show("已成功设置 SNIBypassGUI 开机启动。\n开机后，程序将自动运行至系统托盘并启动服务。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            catch (Exception ex)
            {
                WriteLog("Failed to enable auto-start task.", LogLevel.Error, ex);
                MessageBox.Show($"设置开机启动时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        private void DisableAutoStartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _startupService.CreateTask(AppConsts.TaskName, "开机启动 SNIBypassGUI 并自动清理。", "SNIBypassGUI", PathConsts.CurrentExe, AppConsts.CleanUpArgument);
                MessageBox.Show("已成功取消 SNIBypassGUI 的开机启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            catch (Exception ex)
            {
                WriteLog("Failed to disable auto-start task.", LogLevel.Error, ex);
                MessageBox.Show($"取消开机启动时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
        }

        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            ExitBtn.IsEnabled = false;
            Exit(true);
        }

        private void Exit(bool stopTail = true)
        {
            AnimateWindow(1, 0, async () =>
            {
                Hide();
                TaskbarIcon.Visibility = Visibility.Collapsed;
                TrayIconUtils.RefreshNotification();

                await _proxyService.RemoveHostsRecordsAsync();
                await _proxyService.StopAsync(null);

                if (stopTail) await TailUtils.StopTracking();
                NetworkUtils.FlushDNS();

                Environment.Exit(0);
            });
        }

        #endregion
    }
}
