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
using static SNIBypassGUI.Utils.LogManager;
using MessageBox = HandyControl.Controls.MessageBox;

namespace SNIBypassGUI.Views
{
    public partial class FeedbackWindow : Window
    {
        private int _remainingSeconds;
        private bool _isFirstImage = true;
        public ImageSwitcherService BackgroundService => MainWindow.BackgroundService;
        private HttpClient _client;
        private DispatcherTimer _timer;

        /// <summary>
        /// 窗口构造函数
        /// </summary>
        public FeedbackWindow()
        {
            InitializeComponent();
            Opacity = 0;
            _client = new HttpClient();
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;

            // 窗口可拖动
            TopBar.MouseLeftButtonDown += (o, e) => { DragMove(); };

            DataContext = this;
            BackgroundService.PropertyChanged += OnBackgroundChanged;
            BackgroundService._currentIndex = -1;
        }

        /// <summary>
        /// 发送验证码按钮点击事件
        /// </summary>
        private async void SendCodeBtn_Click(object sender, RoutedEventArgs e)
        {
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
        }

        /// <summary>
        /// 定时器每秒调用一次，更新按钮文本
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
            _remainingSeconds--;
            SendCodeBtn.Content = $"发送验证码 ({_remainingSeconds})";

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
            OKBtn.IsEnabled = false;
            OKBtn.Content = "提交中…";

            string email = EmailTextBox.Text;
            string enteredCode = CodeTextBox.Text;
            string message = FeedbackTextBox.Text;

            if (string.IsNullOrEmpty(message))
            {
                MessageBox.Show("反馈内容不能为空，请重新输入！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                OKBtn.IsEnabled = true;
                OKBtn.Content = "提交";
                return;
            }

            if (string.IsNullOrEmpty(enteredCode))
            {
                MessageBox.Show("请输入验证码！", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                OKBtn.IsEnabled = true;
                OKBtn.Content = "提交";
                return;
            }

            var postData = new Dictionary<string, string>
                {
                    { "email", email },
                    { "code", enteredCode }
                };
            var content = new FormUrlEncodedContent(postData);
            HttpResponseMessage response = await _client.PostAsync(VerifyLink, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                JObject jsonResponse = JObject.Parse(responseBody);
                string status = jsonResponse["status"]?.ToString() ?? "error";
                string messageResp = jsonResponse["message"]?.ToString() ?? "未知错误";
                if (status != "success")
                {
                    MessageBox.Show($"验证码错误，原因：{messageResp}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    OKBtn.IsEnabled = true;
                    OKBtn.Content = "提交";
                    return;
                }
                var finalresponse = await _client.GetAsync(FeedbackLink + $"?email={email}&message={message}");
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
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private async void CancelBtn_Click(object sender, RoutedEventArgs e) => await FadeOut();

        /// <summary>
        /// 验证邮箱地址是否合法
        /// </summary>
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 窗口加载完成事件
        /// </summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            FadeIn();

            /// <summary>
            /// 暂时停用反馈功能
            /// </summary>
            MessageBox.Show("反馈功能已暂时停用，如果需要反馈请加群 946813204 或发送邮件反馈！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            await FadeOut();
        }

        /// <summary>
        /// 渐入动画
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
        /// 渐出动画
        /// </summary>
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

        /// <summary>
        /// 窗口动画逻辑
        /// </summary>
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
