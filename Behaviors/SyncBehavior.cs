using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using SNIBypassGUI.Services;
using static SNIBypassGUI.Common.LogManager;

namespace SNIBypassGUI.Behaviors
{
    public enum SyncDirection { OneWay, OneWayToSource, TwoWay }

    public class GlobalPropertyChangedEventArgs(string channel, string propertyPath) : EventArgs
    {
        public string Channel { get; } = channel;
        public string PropertyPath { get; } = propertyPath;
    }

    public class SyncRule
    {
        private DependencyObject _target;
        private object _currentChannelObject;
        private bool _isUpdating;

        public string Channel { get; set; }
        public DependencyProperty TargetProperty { get; set; }
        public SyncDirection Direction { get; set; } = SyncDirection.TwoWay;
        public string SourcePropertyPath { get; set; }
        public IValueConverter Converter { get; set; }

        /// <summary>
        /// 将规则附加到 UI 元素上，并开始同步。
        /// </summary>
        public void Attach(DependencyObject target)
        {
            _target = target;
            GlobalPropertyService.Instance.GlobalPropertyChanged += OnGlobalChanged;

            // 订阅 UI 属性的变化（用于 TwoWay 和 OneWayToSource）
            if (Direction != SyncDirection.OneWay && TargetProperty != null)
            {
                DependencyPropertyDescriptor
                    .FromProperty(TargetProperty, _target.GetType())
                    .AddValueChanged(_target, OnLocalChanged);
            }

            // 延迟加载，确保初始值能正确同步
            _target.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                UpdateSubscription(); // 订阅数据源对象
                UpdateTarget();       // 更新UI的初始值
            }));
        }

        /// <summary>
        /// 将规则从 UI 元素上分离，并清理所有订阅。
        /// </summary>
        public void Detach()
        {
            if (_target == null) return;

            GlobalPropertyService.Instance.GlobalPropertyChanged -= OnGlobalChanged;
            UnsubscribeFromChannelObject();

            if (Direction != SyncDirection.OneWay && TargetProperty != null)
            {
                DependencyPropertyDescriptor
                    .FromProperty(TargetProperty, _target.GetType())
                    .RemoveValueChanged(_target, OnLocalChanged);
            }

            _target = null;
        }

        /// <summary>
        /// 处理来自 GlobalPropertyService 的全局通知。
        /// </summary>
        private void OnGlobalChanged(object sender, GlobalPropertyChangedEventArgs e)
        {
            if (e.Channel != Channel) return;

            // 如果整个频道对象被替换，需要更新我们的订阅
            if (string.IsNullOrEmpty(e.PropertyPath))
                UpdateSubscription();

            // 更新 UI 目标
            UpdateTarget();
        }

        /// <summary>
        /// 处理来自 UI 元素依赖属性的变化，并推送到数据源。
        /// </summary>
        private void OnLocalChanged(object sender, EventArgs e)
        {
            if (_isUpdating) return;

            try
            {
                var rawValue = _target.GetValue(TargetProperty);
                var convertedValue = Converter != null
                    ? Converter.ConvertBack(rawValue, typeof(object), null, CultureInfo.CurrentCulture)
                    : rawValue;

                GlobalPropertyService.Instance.SetValue(Channel, convertedValue, SourcePropertyPath);
            }
            catch (Exception ex)
            {
                WriteLog($"本地变更处理失败，频道：{Channel}，属性：{TargetProperty?.Name}。", LogLevel.Warning, ex);
            }
        }

        /// <summary>
        /// 处理来自数据源对象的属性变化通知。
        /// </summary>
        private void OnChannelObjectPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 如果变化的是我们关心的属性，并且允许从数据源到 UI 的同步
            if (e.PropertyName == SourcePropertyPath && Direction != SyncDirection.OneWayToSource)
            {
                try
                {
                    // 使用反射从发送方获取新值
                    var propInfo = sender.GetType().GetProperty(e.PropertyName);
                    if (propInfo != null)
                    {
                        var newValue = propInfo.GetValue(sender);
                        // 将此变化重新发布到全局服务，以通知所有订阅者
                        GlobalPropertyService.Instance.SetValue(Channel, newValue, SourcePropertyPath);
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"处理数据源变更失败，频道：{Channel}，属性：{SourcePropertyPath}。", LogLevel.Warning, ex);
                }
            }
        }

        /// <summary>
        /// 更新对频道数据对象的事件订阅。
        /// </summary>
        private void UpdateSubscription()
        {
            UnsubscribeFromChannelObject(); // 先清理旧的

            var channelObject = GlobalPropertyService.Instance.GetValue<object>(Channel);
            if (channelObject is INotifyPropertyChanged newContext)
            {
                _currentChannelObject = newContext;
                newContext.PropertyChanged += OnChannelObjectPropertyChanged;
            }
        }

        /// <summary>
        /// 取消对当前频道数据对象的事件订阅。
        /// </summary>
        private void UnsubscribeFromChannelObject()
        {
            if (_currentChannelObject is INotifyPropertyChanged oldContext)
                oldContext.PropertyChanged -= OnChannelObjectPropertyChanged;
            _currentChannelObject = null;
        }

        /// <summary>
        /// 从 GlobalPropertyService 拉取最新值并更新到 UI 目标。
        /// </summary>
        private void UpdateTarget()
        {
            if (_isUpdating || Direction == SyncDirection.OneWayToSource || _target == null) return;

            _isUpdating = true;
            try
            {
                object sourceValue = GlobalPropertyService.Instance.GetValue<object>(Channel);

                // 如果有 SourcePropertyPath，则深入获取属性值
                if (!string.IsNullOrEmpty(SourcePropertyPath) && sourceValue != null)
                {
                    var propInfo = sourceValue.GetType().GetProperty(SourcePropertyPath, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (propInfo != null)
                        sourceValue = propInfo.GetValue(sourceValue);
                    else sourceValue = null; // 路径无效
                }

                // 使用转换器
                if (Converter != null)
                    sourceValue = Converter.Convert(sourceValue, TargetProperty.PropertyType, null, CultureInfo.CurrentCulture);

                // 在 UI 线程上安全地设置值
                _target.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        if (DependencyProperty.UnsetValue.Equals(sourceValue)) return;

                        // 检查类型兼容性，避免不必要的异常
                        var targetType = TargetProperty.PropertyType;
                        if (sourceValue == null && targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                        {
                            // 避免将 null 设置给非可空值类型
                            return;
                        }
                        else _target.SetValue(TargetProperty, sourceValue);
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"设置 UI 属性值失败，属性：{TargetProperty?.Name}，值：{sourceValue}。", LogLevel.Warning, ex);
                    }
                });
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }

    public class GlobalBehaviorCollection : ObservableCollection<SyncRule>
    {
        private DependencyObject _target;

        public void Attach(DependencyObject target)
        {
            _target = target;
            foreach (var rule in this)
                rule.Attach(target);
        }

        public void Detach()
        {
            foreach (var rule in this)
                rule.Detach();
            _target = null;
        }
    }

    public static class GlobalProperty
    {
        public static readonly DependencyProperty BehaviorsProperty =
            DependencyProperty.RegisterAttached(
                "Behaviors",
                typeof(GlobalBehaviorCollection),
                typeof(GlobalProperty),
                new PropertyMetadata(null, OnBehaviorsChanged));

        public static void SetBehaviors(DependencyObject obj, GlobalBehaviorCollection value) =>
            obj.SetValue(BehaviorsProperty, value);

        public static GlobalBehaviorCollection GetBehaviors(DependencyObject obj) =>
            (GlobalBehaviorCollection)obj.GetValue(BehaviorsProperty);

        private static void OnBehaviorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is GlobalBehaviorCollection oldCollection)
                oldCollection.Detach();

            if (e.NewValue is GlobalBehaviorCollection newCollection)
                newCollection.Attach(d);
        }
    }
}