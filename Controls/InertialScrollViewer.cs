using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SNIBypassGUI.Common.Numerics;

namespace SNIBypassGUI.Controls
{
    /// <summary>
    /// A custom <see cref="ScrollViewer"/> that enhances the user experience with smooth, inertial scrolling,
    /// easing animations, and extensive customization options for modern WPF applications.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>InertialScrollViewer</c> replaces the default abrupt scrolling with a fluid,
    /// physics-based animation, providing a more polished and responsive feel to content navigation.
    /// It is designed to be a drop-in replacement for the standard <see cref="ScrollViewer"/>.
    /// </para>
    /// <para><b>Key Features:</b></para>
    /// <list type="bullet">
    ///   <item>
    ///     <term>Smooth Inertial Scrolling</term>
    ///     <description>
    ///       Provides a fluid scrolling experience with a customizable easing out animation.
    ///       This core feature can be toggled using the <see cref="IsInertiaEnabled"/> property.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Configurable Orientation and Speed</term>
    ///     <description>
    ///       Supports both vertical and horizontal scrolling via the <see cref="ScrollOrientation"/> property.
    ///       The scroll distance per mouse wheel tick can be adjusted with <see cref="ScrollFactor"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Customizable Animation</term>
    ///     <description>
    ///       Allows full control over the animation's length with the <see cref="AnimationDuration"/> property.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Advanced Nested Scrolling</term>
    ///     <description>
    ///       Intelligently forwards mouse wheel events to parent scrollable controls when a boundary is reached.
    ///       This behavior is enabled with <see cref="ForwardScrollAtBoundaries"/> and can be fine-tuned 
    ///       using the <see cref="BoundaryToleranceRatio"/> property to define the edge sensitivity.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Mouse Wheel Control</term>
    ///     <description>
    ///       Mouse wheel scrolling can be explicitly enabled or disabled at any time using the
    ///       <see cref="CanScrollWithMouseWheel"/> property.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Programmatic Scrolling API</term>
    ///     <description>
    ///       Includes helper methods like <see cref="ScrollToTopWithAnimation"/> for common animated scrolling tasks.
    ///     </description>
    ///   </item>
    /// </list>
    /// <para><b>Implementation Note:</b></para>
    /// <para>
    /// To achieve per-pixel smooth scrolling, this control automatically sets the base <c>CanContentScroll</c> property to <c>false</c>
    /// via metadata coercion whenever <see cref="IsInertiaEnabled"/> is <c>true</c>. When inertia is disabled, it respects the original value.
    /// </para>
    /// <para>Copyright (c) 2025 Racpast. All rights reserved.</para>
    /// </remarks>
    public class InertialScrollViewer : ScrollViewer
    {
        public enum ScrollOrientationMode { Vertical, Horizontal }

        #region Fields & State
        private double _targetVerticalOffset;
        private double _targetHorizontalOffset;
        private bool _isAnimating;
        private DateTime _animationStartTime;
        private double _fromOffset;
        private double _toOffset;
        private double _animationDuration;
        private ScrollOrientationMode _animatingDirection;
        private EventHandler _renderHandler;
        #endregion

        #region Dependency Properties
        public static readonly DependencyProperty ScrollOrientationProperty =
            DependencyProperty.Register(nameof(ScrollOrientation), typeof(ScrollOrientationMode), typeof(InertialScrollViewer),
                new PropertyMetadata(ScrollOrientationMode.Vertical));

        public ScrollOrientationMode ScrollOrientation
        {
            get => (ScrollOrientationMode)GetValue(ScrollOrientationProperty);
            set => SetValue(ScrollOrientationProperty, value);
        }

        public static readonly DependencyProperty CanScrollWithMouseWheelProperty =
            DependencyProperty.Register(nameof(CanScrollWithMouseWheel), typeof(bool), typeof(InertialScrollViewer),
                new PropertyMetadata(true));

        public bool CanScrollWithMouseWheel
        {
            get => (bool)GetValue(CanScrollWithMouseWheelProperty);
            set => SetValue(CanScrollWithMouseWheelProperty, value);
        }

        public static readonly DependencyProperty IsInertiaEnabledProperty =
            DependencyProperty.Register(nameof(IsInertiaEnabled), typeof(bool), typeof(InertialScrollViewer),
                new PropertyMetadata(true, OnInertiaEnabledChanged));

        public bool IsInertiaEnabled
        {
            get => (bool)GetValue(IsInertiaEnabledProperty);
            set => SetValue(IsInertiaEnabledProperty, value);
        }

        public static readonly DependencyProperty ScrollFactorProperty =
            DependencyProperty.Register(nameof(ScrollFactor), typeof(double), typeof(InertialScrollViewer),
                new PropertyMetadata(1.0, null, CoercePositive));

        public double ScrollFactor
        {
            get => (double)GetValue(ScrollFactorProperty);
            set => SetValue(ScrollFactorProperty, value);
        }

        public static readonly DependencyProperty AnimationDurationProperty =
            DependencyProperty.Register(nameof(AnimationDuration), typeof(TimeSpan), typeof(InertialScrollViewer),
                new PropertyMetadata(TimeSpan.FromMilliseconds(400)));

        public TimeSpan AnimationDuration
        {
            get => (TimeSpan)GetValue(AnimationDurationProperty);
            set => SetValue(AnimationDurationProperty, value);
        }

        public static readonly DependencyProperty ForwardScrollAtBoundariesProperty =
            DependencyProperty.Register(nameof(ForwardScrollAtBoundaries), typeof(bool), typeof(InertialScrollViewer),
                new PropertyMetadata(false));

        public bool ForwardScrollAtBoundaries
        {
            get => (bool)GetValue(ForwardScrollAtBoundariesProperty);
            set => SetValue(ForwardScrollAtBoundariesProperty, value);
        }

        public static readonly DependencyProperty BoundaryToleranceRatioProperty =
            DependencyProperty.Register(nameof(BoundaryToleranceRatio), typeof(double), typeof(InertialScrollViewer),
                new PropertyMetadata(0.03, null, CoerceRatio));

        public double BoundaryToleranceRatio
        {
            get => (double)GetValue(BoundaryToleranceRatioProperty);
            set => SetValue(BoundaryToleranceRatioProperty, value);
        }

        private static object CoercePositive(DependencyObject d, object baseValue)
        {
            double v = (double)baseValue;
            return Math.Max(0, v);
        }

        private static object CoerceRatio(DependencyObject d, object baseValue)
        {
            double v = (double)baseValue;
            return Math.Max(0, Math.Min(1, v));
        }
        #endregion

        #region Initialization
        static InertialScrollViewer()
        {
            // 重写 CanContentScroll 的元数据，以便在 IsInertiaEnabled 变化时强制其值
            CanContentScrollProperty.OverrideMetadata(typeof(InertialScrollViewer),
                new FrameworkPropertyMetadata(false, null, CoerceCanContentScroll));
        }

        public InertialScrollViewer()
        {
            Loaded += (s, e) =>
            {
                _targetVerticalOffset = VerticalOffset;
                _targetHorizontalOffset = HorizontalOffset;
                CoerceValue(CanContentScrollProperty);
            };
        }
        #endregion

        #region Public API
        public void ScrollToTopWithAnimation()
        {
            _targetVerticalOffset = 0;
            StartInertialAnimation(ScrollOrientationMode.Vertical, 0, AnimationDuration.TotalMilliseconds);
        }
        #endregion

        #region Input Handling
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (!CanScrollWithMouseWheel || e.Delta == 0)
                return;

            bool useVertical = (ScrollOrientation == ScrollOrientationMode.Vertical && ScrollableHeight > 0);
            bool useHorizontal = (ScrollOrientation == ScrollOrientationMode.Horizontal && ScrollableWidth > 0);

            if (ForwardScrollAtBoundaries)
            {
                double tolV = ScrollableHeight * BoundaryToleranceRatio;
                double tolH = ScrollableWidth * BoundaryToleranceRatio;

                bool nearTop = DoubleUtil.LessThanOrClose(VerticalOffset, tolV);
                bool nearBottom = DoubleUtil.GreaterThanOrClose(VerticalOffset, ScrollableHeight - tolV);
                bool nearLeft = DoubleUtil.LessThanOrClose(HorizontalOffset, tolH);
                bool nearRight = DoubleUtil.GreaterThanOrClose(HorizontalOffset, ScrollableWidth - tolH);

                bool shouldForward =
                    (e.Delta > 0 && (useVertical ? nearTop : nearLeft)) ||
                    (e.Delta < 0 && (useVertical ? nearBottom : nearRight));

                if (shouldForward)
                {
                    var parent = FindVisualParent<UIElement>(this);
                    if (parent != null)
                    {
                        if (IsInertiaEnabled && _isAnimating)
                        {
                            double boundaryTarget = e.Delta > 0 ? 0
                                : (ScrollOrientation == ScrollOrientationMode.Vertical ? ScrollableHeight : ScrollableWidth);

                            StartInertialAnimation(_animatingDirection, boundaryTarget, AnimationDuration.TotalMilliseconds * 0.6);
                        }

                        var newEventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                        {
                            RoutedEvent = MouseWheelEvent,
                            Source = e.Source
                        };
                        parent.RaiseEvent(newEventArgs);
                        e.Handled = true;
                        return;
                    }
                }

            }

            if (!IsInertiaEnabled)
            {
                base.OnMouseWheel(e);
                return;
            }

            e.Handled = true;

            double factor = ScrollFactor;
            double duration = AnimationDuration.TotalMilliseconds;
            double pixelDelta = -e.Delta * factor;

            if (useVertical)
            {
                _targetVerticalOffset = Clamp(_targetVerticalOffset + pixelDelta, 0, ScrollableHeight);
                StartInertialAnimation(ScrollOrientationMode.Vertical, _targetVerticalOffset, duration);
            }
            else if (useHorizontal)
            {
                _targetHorizontalOffset = Clamp(_targetHorizontalOffset + pixelDelta, 0, ScrollableWidth);
                StartInertialAnimation(ScrollOrientationMode.Horizontal, _targetHorizontalOffset, duration);
            }
            else base.OnMouseWheel(e);
        }

        protected override void OnScrollChanged(ScrollChangedEventArgs e)
        {
            base.OnScrollChanged(e);

            if (!_isAnimating)
            {
                _targetVerticalOffset = e.VerticalOffset;
                _targetHorizontalOffset = e.HorizontalOffset;
            }
        }
        #endregion

        #region Animation Logic
        private void StartInertialAnimation(ScrollOrientationMode direction, double toOffset, double duration)
        {
            // 更新动画的目标方向和最终位置
            _animatingDirection = direction;
            _toOffset = toOffset;

            if (_isAnimating)
            {
                _fromOffset = direction == ScrollOrientationMode.Vertical ? VerticalOffset : HorizontalOffset;
                _animationStartTime = DateTime.Now;
                _animationDuration = duration;
                return;
            }

            // 设置新动画的起点
            _fromOffset = direction == ScrollOrientationMode.Vertical ? VerticalOffset : HorizontalOffset;
            _animationDuration = duration;
            _animationStartTime = DateTime.Now;

            // 如果滚动的距离非常小，就直接跳到目标位置，这样更精确
            if (Math.Abs(_fromOffset - _toOffset) < 0.5)
            {
                if (direction == ScrollOrientationMode.Vertical) ScrollToVerticalOffset(toOffset);
                else ScrollToHorizontalOffset(toOffset);
                return;
            }

            // 挂载渲染事件
            _renderHandler ??= new EventHandler(OnFrame);
            _isAnimating = true;
            CompositionTarget.Rendering += _renderHandler;
        }

        private void StopAnimation()
        {
            CompositionTarget.Rendering -= _renderHandler;
            _isAnimating = false;
        }

        private void OnFrame(object sender, EventArgs e)
        {
            double elapsed = (DateTime.Now - _animationStartTime).TotalMilliseconds;
            double t = Math.Min(1.0, elapsed / _animationDuration);
            double eased = 1 - Math.Pow(1 - t, 3); // EaseOutCubic
            double value = _fromOffset + (_toOffset - _fromOffset) * eased;

            if (_animatingDirection == ScrollOrientationMode.Vertical)
                ScrollToVerticalOffset(value);
            else
                ScrollToHorizontalOffset(value);

            if (t >= 1.0)
                StopAnimation();
        }
        #endregion

        #region Property System Callbacks
        private static void OnInertiaEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            d.CoerceValue(CanContentScrollProperty);

        private static object CoerceCanContentScroll(DependencyObject d, object baseValue)
        {
            var scrollViewer = (InertialScrollViewer)d;
            if (scrollViewer.IsInertiaEnabled) return false;
            return baseValue;
        }
        #endregion

        #region Helpers
        private static double Clamp(double v, double min, double max)
            => Math.Max(min, Math.Min(v, max));

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            return parentObject is T parent ? parent : FindVisualParent<T>(parentObject);
        }
        #endregion
    }
}