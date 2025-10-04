using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using MaterialDialogHost = MaterialDesignThemes.Wpf.DialogHost;

namespace SNIBypassGUI.Controls
{
    /// <summary>
    /// A high-performance and robust custom dialog host that enhances the <see cref="MaterialDesignThemes.Wpf.DialogHost"/>.
    /// It implements a novel screenshot-based blur technique for efficiency, provides fully customizable animations, and ensures predictable behavior in complex UI scenarios.
    /// </summary>
    /// <remarks>
    /// <para>This control was created to provide enhanced performance and address certain design limitations of the standard MaterialDesignInXAML DialogHost:</para>
    /// <list type="number">
    /// <item><description>Performance degradation when applying a live <c>BlurEffect</c> to a visually complex background.</description></item>
    /// <item><description>The hardcoded semi-transparent overlay, which limited design choices for opaque backgrounds.</description></item>
    /// <item><description>Lack of built-in synchronization for rapid, sequential dialog presentations, which could lead to visual inconsistencies.</description></item>
    /// </list>
    /// 
    /// <para><b>Key Features:</b></para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <b>High-Performance Blur:</b> Applies a background blur efficiently by taking a static snapshot of the <see cref="BlurTarget"/> element. This avoids the heavy performance cost of real-time blurring on a complex visual tree.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Customizable Animations:</b> Offers full control over the open/close animation speed for the dialog, overlay, and blur effect via the <see cref="AnimationDuration"/> property.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Flexible Overlay:</b> Overrides the default Material Design style to allow for fully opaque or custom-styled <see cref="OverlayBackground"/> colors, removing the original hardcoded transparency.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <b>Seamless Integration:</b> Designed as a wrapper that mirrors the essential API of the original component, including the static <see cref="Show(object, object)"/> method. It serves as a familiar, drop-in replacement that provides stable and consistent behavior across various use cases.
    /// </description>
    /// </item>
    /// </list>
    /// 
    /// <para>
    /// <b>Usage:</b> Recommended for applications with visually rich interfaces where performance is critical, or in scenarios where dialogs may be triggered in quick succession by user actions or application logic.
    /// </para>
    /// <para>
    /// <b>Pro Tip:</b> For the best visual alignment and to prevent rendering artifacts, it is highly recommended to set <c>UseLayoutRounding="True"</c> on the parent <c>Window</c>. This ensures the captured snapshot perfectly overlaps with the underlying UI.
    /// </para>
    /// <para>Copyright (c) 2025 Racpast. All rights reserved.</para>
    /// </remarks>
    [TemplatePart(Name = "PART_DialogHost", Type = typeof(MaterialDialogHost))]
    [TemplatePart(Name = "PART_BlurImage", Type = typeof(Image))]
    public class DialogHost : ContentControl
    {
        #region Fields & Constants
        /// <summary>
        /// The base duration of the animations defined in the XAML control template.
        /// This value MUST match the KeyTime in the Storyboard.
        /// </summary>
        /// <remarks>
        /// <i>Dependency property? C’mon, that’s overkill — we’re not launching rockets here.
        /// (If it runs, it ships.)</i>
        /// </remarks>
        private static readonly TimeSpan StoryboardBaseDuration = TimeSpan.FromSeconds(0.3);

        private static readonly HashSet<WeakReference<DialogHost>> LoadedInstances = [];

        private static readonly Dictionary<object, SemaphoreSlim> DialogLocks = [];
        private static readonly object LocksDictionarySync = new();

        private Image _blurImage;
        private MaterialDialogHost _internalDialogHost;

        private Task _closingAnimationTask = Task.CompletedTask;
        private CancellationTokenSource _animationTokenSource;
        #endregion

        #region Dependency Properties
        // --- Blur Related ---
        public UIElement BlurTarget { get => (UIElement)GetValue(BlurTargetProperty); set => SetValue(BlurTargetProperty, value); }
        public static readonly DependencyProperty BlurTargetProperty =
            DependencyProperty.Register(nameof(BlurTarget), typeof(UIElement), typeof(DialogHost));

        public double BlurRadius { get => (double)GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }
        public static readonly DependencyProperty BlurRadiusProperty =
            DependencyProperty.Register(nameof(BlurRadius), typeof(double), typeof(DialogHost), new PropertyMetadata(10.0));

        public TimeSpan AnimationDuration { get => (TimeSpan)GetValue(AnimationDurationProperty); set => SetValue(AnimationDurationProperty, value); }
        public static readonly DependencyProperty AnimationDurationProperty =
            DependencyProperty.Register(nameof(AnimationDuration), typeof(TimeSpan), typeof(DialogHost),
                new PropertyMetadata(TimeSpan.FromMilliseconds(300), OnAnimationDurationChanged));

        // --- Mirrored MaterialDialogHost Properties ---
        public object Identifier { get => GetValue(IdentifierProperty); set => SetValue(IdentifierProperty, value); }
        public static readonly DependencyProperty IdentifierProperty =
            DependencyProperty.Register(nameof(Identifier), typeof(object), typeof(DialogHost));

        public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(DialogHost),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public object DialogContent { get => GetValue(DialogContentProperty); set => SetValue(DialogContentProperty, value); }
        public static readonly DependencyProperty DialogContentProperty =
            DependencyProperty.Register(nameof(DialogContent), typeof(object), typeof(DialogHost));

        public DataTemplate DialogContentTemplate { get => (DataTemplate)GetValue(DialogContentTemplateProperty); set => SetValue(DialogContentTemplateProperty, value); }
        public static readonly DependencyProperty DialogContentTemplateProperty =
            DependencyProperty.Register(nameof(DialogContentTemplate), typeof(DataTemplate), typeof(DialogHost));

        public bool CloseOnClickAway { get => (bool)GetValue(CloseOnClickAwayProperty); set => SetValue(CloseOnClickAwayProperty, value); }
        public static readonly DependencyProperty CloseOnClickAwayProperty =
            DependencyProperty.Register(nameof(CloseOnClickAway), typeof(bool), typeof(DialogHost));

        public object CloseOnClickAwayParameter { get => GetValue(CloseOnClickAwayParameterProperty); set => SetValue(CloseOnClickAwayParameterProperty, value); }
        public static readonly DependencyProperty CloseOnClickAwayParameterProperty =
            DependencyProperty.Register(nameof(CloseOnClickAwayParameter), typeof(object), typeof(DialogHost));

        public Brush OverlayBackground { get => (Brush)GetValue(OverlayBackgroundProperty); set => SetValue(OverlayBackgroundProperty, value); }
        public static readonly DependencyProperty OverlayBackgroundProperty =
            DependencyProperty.Register(nameof(OverlayBackground), typeof(Brush), typeof(DialogHost));

        public Brush DialogBackground { get => (Brush)GetValue(DialogBackgroundProperty); set => SetValue(DialogBackgroundProperty, value); }
        public static readonly DependencyProperty DialogBackgroundProperty =
            DependencyProperty.Register(nameof(DialogBackground), typeof(Brush), typeof(DialogHost));

        public Thickness DialogMargin { get => (Thickness)GetValue(DialogMarginProperty); set => SetValue(DialogMarginProperty, value); }
        public static readonly DependencyProperty DialogMarginProperty =
            DependencyProperty.Register(nameof(DialogMargin), typeof(Thickness), typeof(DialogHost));

        public CornerRadius CornerRadius { get => (CornerRadius)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(DialogHost));

        public BaseTheme DialogTheme { get => (BaseTheme)GetValue(DialogThemeProperty); set => SetValue(DialogThemeProperty, value); }
        public static readonly DependencyProperty DialogThemeProperty =
            DependencyProperty.Register(nameof(DialogTheme), typeof(BaseTheme), typeof(DialogHost));
        #endregion

        #region Constructors & Lifecycle
        static DialogHost()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DialogHost), new FrameworkPropertyMetadata(typeof(DialogHost)));
        }

        public DialogHost()
        {
            Loaded += (_, _) => AddInstance(this);
            Unloaded += (_, _) => RemoveInstance(this);
        }
        #endregion

        #region Static Helpers
        private static SemaphoreSlim GetOrCreateLock(object identifier)
        {
            lock (LocksDictionarySync)
            {
                if (!DialogLocks.TryGetValue(identifier, out var dialogLock))
                {
                    dialogLock = new SemaphoreSlim(1, 1);
                    DialogLocks[identifier] = dialogLock;
                }
                return dialogLock;
            }
        }

        public static async Task<object> Show(object content, object dialogIdentifier)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (dialogIdentifier == null) throw new ArgumentNullException(nameof(dialogIdentifier));

            var dialogLock = GetOrCreateLock(dialogIdentifier);
            await dialogLock.WaitAsync();

            try
            {
                var instance = GetInstance(dialogIdentifier);
                return await instance.ShowDialog(content);
            }
            finally
            {
                dialogLock.Release();
            }
        }

        private static void AddInstance(DialogHost instance) =>
            LoadedInstances.Add(new WeakReference<DialogHost>(instance));

        private static void RemoveInstance(DialogHost instance)
        {
            foreach (var weakRef in LoadedInstances.ToList())
            {
                if (!weakRef.TryGetTarget(out var dialogHost) || ReferenceEquals(dialogHost, instance))
                {
                    LoadedInstances.Remove(weakRef);
                    break;
                }
            }
        }

        private static DialogHost GetInstance(object dialogIdentifier)
        {
            if (LoadedInstances.Count == 0)
                throw new InvalidOperationException("No loaded SNIBypassGUI.Controls.DialogHost instances found.");

            var matches = LoadedInstances
                .Select(r => r.TryGetTarget(out var t) ? t : null)
                .Where(t => t != null && Equals(t.Identifier, dialogIdentifier))
                .ToList();

            return matches.Count switch
            {
                0 => throw new InvalidOperationException($"No loaded DialogHost has an Identifier property matching '{dialogIdentifier}'."),
                > 1 => throw new InvalidOperationException("Multiple DialogHosts found. Specify a unique Identifier."),
                _ => matches[0]
            };
        }
        #endregion

        #region Template & Visual Tree Handling
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _internalDialogHost = GetTemplateChild("PART_DialogHost") as MaterialDialogHost;
            _blurImage = GetTemplateChild("PART_BlurImage") as Image;

            if (_internalDialogHost != null)
            {
                _internalDialogHost.ApplyTemplate();
                UpdateStoryboardSpeedRatios();
            }
        }
        #endregion

        #region Dialog API
        public async Task<object> ShowDialog(object content)
        {
            if (_internalDialogHost == null || _blurImage == null)
                return await MaterialDialogHost.Show(content, Identifier);

            if (BlurTarget is not FrameworkElement target || BlurRadius <= 0)
                return await MaterialDialogHost.Show(content, _internalDialogHost.Identifier);

            // 取消任何可能还在运行的旧动画任务
            _animationTokenSource?.Cancel();
            _animationTokenSource = new CancellationTokenSource();
            var token = _animationTokenSource.Token;

            void onClosing(object s, DialogClosingEventArgs e) => _closingAnimationTask = AnimateOutAsync(target);
            void onOpened(object s, DialogOpenedEventArgs e) => _ = AnimateInAsync(target, token);

            try
            {
                _internalDialogHost.DialogOpened += onOpened;
                _internalDialogHost.DialogClosing += onClosing;

                var result = await MaterialDialogHost.Show(content, _internalDialogHost.Identifier);

                await _closingAnimationTask;

                return result;
            }
            finally
            {
                _internalDialogHost.DialogOpened -= onOpened;
                _internalDialogHost.DialogClosing -= onClosing;
                _animationTokenSource?.Cancel(); // 确保最后总是清理干净
            }
        }
        #endregion

        #region Storyboard Timing Management
        private static void OnAnimationDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DialogHost host)
                host.UpdateStoryboardSpeedRatios();
        }

        private void UpdateStoryboardSpeedRatios()
        {
            if (_internalDialogHost == null) return;
            if (_internalDialogHost.Template?.FindName("DialogHostRoot", _internalDialogHost) is not FrameworkElement templateRoot) return;

            var popupStatesGroup = VisualStateManager
                .GetVisualStateGroups(templateRoot)?
                .OfType<VisualStateGroup>()
                .FirstOrDefault(g => g.Name == "PopupStates");

            if (popupStatesGroup == null) return;

            var openTransition = popupStatesGroup.Transitions
                .OfType<VisualTransition>()
                .FirstOrDefault(t => t.From == "Closed" && t.To == "Open");
            var closeTransition = popupStatesGroup.Transitions
                .OfType<VisualTransition>()
                .FirstOrDefault(t => t.From == "Open" && t.To == "Closed");

            double desiredDurationSeconds = AnimationDuration.TotalSeconds;
            if (desiredDurationSeconds <= 0) return;

            double speedRatio = StoryboardBaseDuration.TotalSeconds / desiredDurationSeconds;
            if (openTransition?.Storyboard != null) openTransition.Storyboard.SpeedRatio = speedRatio;
            if (closeTransition?.Storyboard != null) closeTransition.Storyboard.SpeedRatio = speedRatio;
        }
        #endregion

        #region Blur Animation
        private async Task AnimateInAsync(FrameworkElement target, CancellationToken token)
        {
            await Application.Current.Dispatcher.InvokeAsync(target.UpdateLayout, DispatcherPriority.Render);

            int width = (int)Math.Max(1, Math.Round(target.ActualWidth));
            int height = (int)Math.Max(1, Math.Round(target.ActualHeight));

            var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(target);
            _blurImage.Source = bmp;
            _blurImage.Effect = new BlurEffect { Radius = BlurRadius };
            _blurImage.Opacity = 0;
            _blurImage.Visibility = Visibility.Visible;

            target.Opacity = 1;
            target.Visibility = Visibility.Visible;

            var animDuration = new Duration(AnimationDuration);
            var blurAnim = new DoubleAnimation(0, 1, animDuration) { EasingFunction = new QuadraticEase() };
            var targetAnim = new DoubleAnimation(1, 0, animDuration) { EasingFunction = new QuadraticEase() };

            _blurImage.BeginAnimation(OpacityProperty, blurAnim);
            target.BeginAnimation(OpacityProperty, targetAnim);

            try
            {
                await Task.Delay(AnimationDuration, token);
            }
            catch (OperationCanceledException)
            {
                // 如果被取消，就说明退出动画接管了，直接返回就好
                return;
            }

            // 只有在动画正常完成时，才隐藏目标控件
            target.Visibility = Visibility.Hidden;
        }

        private async Task AnimateOutAsync(FrameworkElement target)
        {
            // 快停下！
            _animationTokenSource?.Cancel();

            target.Visibility = Visibility.Visible;
            _blurImage.Visibility = Visibility.Visible;

            var animDuration = new Duration(AnimationDuration);
            var blurAnim = new DoubleAnimation(1, 0, animDuration) { EasingFunction = new QuadraticEase() };
            var targetAnim = new DoubleAnimation(0, 1, animDuration) { EasingFunction = new QuadraticEase() };

            _blurImage.BeginAnimation(OpacityProperty, blurAnim);
            target.BeginAnimation(OpacityProperty, targetAnim);

            await Task.Delay(AnimationDuration);

            _blurImage.Visibility = Visibility.Collapsed;
            _blurImage.Source = null;
            target.BeginAnimation(OpacityProperty, null);
            target.Opacity = 1;
        }
        #endregion
    }
}