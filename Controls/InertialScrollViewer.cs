using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SNIBypassGUI.Utils.Numerics;

namespace SNIBypassGUI.Controls
{
    /// <summary>
    /// A custom <see cref="ScrollViewer"/> that provides an inertial scrolling experience 
    /// with easing effects, suitable for modern WPF applications.
    /// </summary>
    /// <remarks>
    /// <para><b>Features:</b></para>
    /// <list type="bullet">
    /// <item>
    /// <description>Smooth, inertial scrolling with an easing animation.</description>
    /// </item>
    /// <item>
    /// <description>Supports both vertical and horizontal orientations via the <see cref="ScrollOrientation"/> property.</description>
    /// </item>
    /// <item>
    /// <description>Adjustable scroll speed using the <see cref="ScrollFactor"/> property.</description>
    /// </item>
    /// <item>
    /// <description>Customizable animation length through the <see cref="AnimationDuration"/> property.</description>
    /// </item>
    /// <item>
    /// <description>The inertia effect can be toggled with <see cref="IsInertiaEnabled"/>, falling back to the default system scrolling behavior.</description>
    /// </item>
    /// <item>
    /// <description>Supports forwarding scroll events to parent controls at boundaries (<see cref="ForwardScrollAtBoundaries"/>), ideal for nested scrolling scenarios.</description>
    /// </item>
    /// <item>
    /// <description>Provides helper methods like <see cref="ScrollToTopWithAnimation"/> for common scrolling tasks.</description>
    /// </item>
    /// </list>
    /// <para><b>Usage:</b> Recommended for WPF user interfaces requiring a more fluid and modern scrolling interaction, such as lists, articles, or image galleries.</para>
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
                new PropertyMetadata(true));

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

        private static object CoercePositive(DependencyObject d, object baseValue)
        {
            double v = (double)baseValue;
            return Math.Max(0, v);
        }
        #endregion

        #region Initialization
        public InertialScrollViewer()
        {
            Loaded += (s, e) =>
            {
                _targetVerticalOffset = VerticalOffset;
                _targetHorizontalOffset = HorizontalOffset;
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

            if (ForwardScrollAtBoundaries)
            {
                bool shouldForward = false;

                if (e.Delta > 0 && DoubleUtil.IsZero(VerticalOffset))
                    shouldForward = true;
                else if (e.Delta < 0 && DoubleUtil.GreaterThanOrClose(VerticalOffset, ScrollableHeight))
                    shouldForward = true;
                if (shouldForward)
                {
                    var parent = FindVisualParent<UIElement>(this);
                    if (parent != null)
                    {
                        var newEventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                        {
                            RoutedEvent = UIElement.MouseWheelEvent,
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

            bool canScrollVertically = ScrollableHeight > 0;
            bool canScrollHorizontally = ScrollableWidth > 0;

            bool useVertical = (ScrollOrientation == ScrollOrientationMode.Vertical && canScrollVertically) ||
                               (canScrollVertically && !canScrollHorizontally);

            bool useHorizontal = (ScrollOrientation == ScrollOrientationMode.Horizontal && canScrollHorizontally) ||
                                 (canScrollHorizontally && !canScrollVertically);

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
            StopAnimation();

            _animatingDirection = direction;
            _fromOffset = direction == ScrollOrientationMode.Vertical ? VerticalOffset : HorizontalOffset;
            _toOffset = toOffset;
            _animationDuration = duration;
            _animationStartTime = DateTime.Now;

            if (Math.Abs(_fromOffset - _toOffset) < 0.5)
            {
                // 起始点和终点距离很近的话直接滚动到精确位置以便触发边界滚动传递
                if (direction == ScrollOrientationMode.Vertical) ScrollToVerticalOffset(toOffset);
                else ScrollToHorizontalOffset(toOffset);
                return;
            }

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