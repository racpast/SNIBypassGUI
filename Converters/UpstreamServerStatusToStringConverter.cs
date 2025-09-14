using System;
using System.Globalization;
using System.Windows.Data;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Converters
{
    public class UpstreamServerStatusToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UpstreamServerStatus status)
            {
                return status switch
                {
                    UpstreamServerStatus.Active => "活动",
                    UpstreamServerStatus.Backup => "备份",
                    UpstreamServerStatus.Down => "停用",
                    _ => string.Empty
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
                    "活动" => UpstreamServerStatus.Active,
                    "备份" => UpstreamServerStatus.Backup,
                    "停用" => UpstreamServerStatus.Down,
                    _ => UpstreamServerStatus.Active,
                };
            }
            return UpstreamServerStatus.Active;
        }
    }
}
