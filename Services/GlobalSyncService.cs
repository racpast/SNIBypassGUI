using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using SNIBypassGUI.Behaviors;
using static SNIBypassGUI.Utils.LogManager;

namespace SNIBypassGUI.Services
{
    public sealed class GlobalPropertyService
    {
        public static readonly GlobalPropertyService Instance = new();

        private readonly Dictionary<string, object> _channels = [];
        public event EventHandler<GlobalPropertyChangedEventArgs> GlobalPropertyChanged;

        public T GetValue<T>(string channel)
        {
            if (_channels.TryGetValue(channel, out var value))
            {
                if (value == null) return default;
                if (value is T typedValue) return typedValue;

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
            return default;
        }

        public void SetValue(string channel, object value, string propertyPath = null)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                // 设置整个频道对象
                _channels[channel] = value;
                OnGlobalPropertyChanged(channel, null);
                return;
            }

            // 设置嵌套属性
            if (!_channels.TryGetValue(channel, out var targetObj) || targetObj == null)
                return;

            var parts = propertyPath.Split('.');
            object current = targetObj;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                var prop = GetProperty(current.GetType(), parts[i]);
                if (prop == null) return;

                current = prop.GetValue(current);
                if (current == null) return;
            }

            var finalProp = GetProperty(current.GetType(), parts.Last());
            if (finalProp != null && finalProp.CanWrite)
            {
                try
                {
                    var convertedValue = ConvertValue(value, finalProp.PropertyType);
                    finalProp.SetValue(current, convertedValue);
                    OnGlobalPropertyChanged(channel, propertyPath);
                }
                catch (Exception ex)
                {
                    WriteLog($"设置属性失败，属性：{propertyPath}，值：{value}。", LogLevel.Warning, ex);
                }
            }
        }

        private object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsInstanceOfType(value)) return value;

            // 尝试使用类型转换器
            var converter = TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(value.GetType()))
                return converter.ConvertFrom(value);

            // 尝试基本类型转换
            if (value is IConvertible)
                return Convert.ChangeType(value, targetType);

            return value;
        }

        private void OnGlobalPropertyChanged(string channel, string propertyPath)
        {
            GlobalPropertyChanged?.Invoke(this,
                new GlobalPropertyChangedEventArgs(channel, propertyPath));
        }

        private PropertyInfo GetProperty(Type type, string name)
        {
            return type.GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
