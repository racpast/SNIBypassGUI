using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    public class AffinityRuleMatchModeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AffinityRuleMatchMode mode)
            {
                return mode switch
                {
                    AffinityRuleMatchMode.Exclude => "排除",
                    AffinityRuleMatchMode.Include => "包含",
                    _ => string.Empty,
                };
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "排除" => AffinityRuleMatchMode.Exclude,
                    "包含" => AffinityRuleMatchMode.Include,
                    _ => AffinityRuleMatchMode.Include,
                };
            }
            return AffinityRuleMatchMode.Include;
        }
    }
}
