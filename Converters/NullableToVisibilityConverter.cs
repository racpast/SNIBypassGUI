using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SNIBypassGUI.Converters
{
    public class NullableToVisibilityConverter : IValueConverter
    {
        // 有值则显示，无值则折叠
        public static readonly NullableToVisibilityConverter CollapsedInstance = new() { NullValue = Visibility.Collapsed, NotNullValue = Visibility.Visible };

        // 无值则显示，有值则折叠
        public static readonly NullableToVisibilityConverter NotCollapsedInstance = new() { NullValue = Visibility.Visible, NotNullValue = Visibility.Collapsed };

        // 有值则显示，无值则隐藏
        public static readonly NullableToVisibilityConverter HiddenInstance = new() { NullValue = Visibility.Hidden, NotNullValue = Visibility.Visible };

        // 无值则显示，有值则隐藏
        public static readonly NullableToVisibilityConverter NotHiddenInstance = new() { NullValue = Visibility.Visible, NotNullValue = Visibility.Hidden };

        public Visibility NullValue { get; set; } = Visibility.Collapsed;
        public Visibility NotNullValue { get; set; } = Visibility.Visible;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? NullValue : NotNullValue;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
