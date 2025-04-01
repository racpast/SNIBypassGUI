using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using static SNIBypassGUI.Consts.LinksConsts;
using SNIBypassGUI.Utils;
using static SNIBypassGUI.Utils.LogManager;
using static SNIBypassGUI.Utils.StringUtils;
using MessageBox = HandyControl.Controls.MessageBox;

namespace SNIBypassGUI.Views
{
    public partial class FeedbackWindow : Window
    {
        // 剩余秒数
        private int _remainingSeconds;

        // 暴露 BackgroundService 以便访问
        public ImageSwitcherService BackgroundService => MainWindow.BackgroundService;

        private HttpClient _client = new();
        private DispatcherTimer _timer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        /// <summary>
        /// 窗口构造函数
        /// </summary>
        public FeedbackWindow()
        {
            WriteLog("进入 FeedbackWindow。", LogLevel.Debug);

            InitializeComponent();

            // 使窗口可拖动
            TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };

            // 设置 DataContext
            DataContext = this;

            // 初始化定时器
            _timer.Tick += Timer_Tick;
            BackgroundService.PropertyChanged += OnBackgroundChanged;
            CurrentImage.Source = BackgroundService.CurrentImage;

            WriteLog("完成 FeedbackWindow。", LogLevel.Debug);
        }

        /// <summary>
        /// 发送验证码按钮点击事件
        /// </summary>
        private async void SendCodeBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 SendCodeBtn_Click。", LogLevel.Debug);

            // 禁用按钮
            SendCodeBtn.IsEnabled = false;
            SendCodeBtn.Content = "发送中…";

            string email = EmailTextBox.Text;

            if (!IsValidEmail(email))
            {
                MessageBox.Show("请输入正确的邮箱地址！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                SendCodeBtn.Content = "发送验证码";
                SendCodeBtn.IsEnabled = true;
                return;
            }

            try
            {
                var response = await _client.GetAsync(VerifyLink + $"?email={email}");
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("验证码已发送到邮箱，请留意查收！", "验证码", MessageBoxButton.OK, MessageBoxImage.Information);
                    _remainingSeconds = 60;
                    _timer.Start();
                }
                else
                {
                    MessageBox.Show("验证码发送失败，请稍后重试！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SendCodeBtn.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                WriteLog("发送请求时遇到异常。", LogLevel.Error, ex);
                MessageBox.Show($"发送请求时遇到异常。\r\n{ex}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog("完成 SendCodeBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 定时器每秒调用一次，更新按钮文本
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
            _remainingSeconds--;
            SendCodeBtn.Content = $"发送验证码 ({_remainingSeconds})";

            // 倒计时结束
            if (_remainingSeconds <= 0)
            {
                _timer.Stop();
                SendCodeBtn.IsEnabled = true;
                SendCodeBtn.Content = "发送验证码";
            }
        }

        /// <summary>
        /// 提交反馈按钮点击事件
        /// </summary>
        private async void OKBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 OKBtn_Click。", LogLevel.Debug);

            // 禁用按钮
            OKBtn.IsEnabled = false;
            OKBtn.Content = "提交中…";

            string email = EmailTextBox.Text;
            string enteredCode = CodeTextBox.Text;
            string message = FeedbackTextBox.Text;

            // 验证邮箱地址
            if (string.IsNullOrEmpty(message))
            {
                MessageBox.Show("反馈内容不能为空，请重新输入！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                OKBtn.IsEnabled = true;
                OKBtn.Content = "提交";
                return;
            }

            // 验证验证码
            if (string.IsNullOrEmpty(enteredCode))
            {
                MessageBox.Show("请输入验证码！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                OKBtn.IsEnabled = true;
                OKBtn.Content = "提交";
                return;
            }

            var postData = new Dictionary<string, string>
            {
                {"email", email},
                {"code", enteredCode}
            };

            var content = new FormUrlEncodedContent(postData);
            HttpResponseMessage response = await _client.PostAsync(VerifyLink, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            // 验证验证码是否正确
            if (response.IsSuccessStatusCode)
            {
                JObject jsonResponse = JObject.Parse(responseBody);
                string status = jsonResponse["status"]?.ToString() ?? "error";
                string messageResp = jsonResponse["message"]?.ToString() ?? "未知错误";

                // 验证码错误
                if (status != "success")
                {
                    MessageBox.Show($"验证码错误，原因：{messageResp}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    OKBtn.IsEnabled = true;
                    OKBtn.Content = "提交";
                    return;
                }

                // 提交反馈
                var finalresponse = await _client.GetAsync(FeedbackLink + $"?email={email}&message={message}");

                // 提交成功
                if (finalresponse.IsSuccessStatusCode)
                {
                    MessageBox.Show("反馈提交成功！感谢使用 SNIBypassGUI ！", "反馈", MessageBoxButton.OK, MessageBoxImage.Information);
                    await FadeOut(true);
                }
                else
                {
                    MessageBox.Show("反馈提交失败，请稍后重试！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    OKBtn.IsEnabled = true;
                }
            }
            else
            {
                MessageBox.Show("验证码验证失败，请稍后重试！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                OKBtn.IsEnabled = true;
            }

            WriteLog("完成 OKBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private async void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 CancelBtn_Click。", LogLevel.Debug);
            await FadeOut();
            WriteLog("完成 CancelBtn_Click。", LogLevel.Debug);
        }

        /// <summary>
        /// 窗口加载完成事件
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WriteLog("进入 Window_Loaded。", LogLevel.Debug);

            FadeIn();

            /// <summary>
            /// 暂时停用反馈功能
            /// </summary>
            /*
                * MessageBox.Show("反馈功能已暂时停用，如果需要反馈请加群 946813204 或发送邮件反馈！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                * await FadeOut();
            */

            WriteLog("完成 Window_Loaded。", LogLevel.Debug);
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

            // 设置对话框结果并关闭窗口
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
