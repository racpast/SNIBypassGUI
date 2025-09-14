using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    public class DnsMappingRuleActionToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DnsMappingRuleAction action)
            {
                return action switch
                {
                    DnsMappingRuleAction.IP => "指定地址",
                    DnsMappingRuleAction.Forward => "转发上游",
                    DnsMappingRuleAction.Block => "NXDOMAIN",
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
                    "指定地址" => DnsMappingRuleAction.IP,
                    "转发上游" => DnsMappingRuleAction.Forward,
                    "NXDOMAIN" => DnsMappingRuleAction.Block,
                    _ => DnsMappingRuleAction.IP,
                };
            }
            return DnsMappingRuleAction.IP;
        }
    }
}
