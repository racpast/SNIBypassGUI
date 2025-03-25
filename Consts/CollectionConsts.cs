using System.Collections.Generic;
using System.Collections.ObjectModel;
using SNIBypassGUI.Models;
using static SNIBypassGUI.Consts.ConfigConsts;
using static SNIBypassGUI.Consts.PathConsts;

namespace SNIBypassGUI.Consts
{
    public static class CollectionConsts
    {
        public readonly static string[] pximgIP =
        [
            "210.140.139.135",
            "210.140.139.132",
            "210.140.139.137",
            "210.140.139.134",
            "210.140.139.131",
            "210.140.139.133",
            "210.140.139.130",
            "210.140.139.129",
            "210.140.139.136"
        ];

        public readonly static ReadOnlyDictionary<string, byte[]> PathToResourceDic = new(new Dictionary<string, byte[]>
        {
            {nginxPath, Properties.Resources.SNIBypass},
            {nginxConfigFile, Properties.Resources.nginx},
            {CERFile,Properties.Resources.ca},
            {CRTFile,Properties.Resources.SNIBypassCrt},
            {KeyFile,Properties.Resources.SNIBypassKey},
            {AcrylicServiceExeFilePath,Properties.Resources.AcrylicService},
            {AcrylicHostsPath,Properties.Resources.AcrylicHosts},
            {AcrylicConfigurationPath,Properties.Resources.AcrylicConfiguration},
            {AcrylicHostsAll,Properties.Resources.AcrylicHosts_All},
            {SystemHostsAll,Properties.Resources.SystemHosts_All},
            {SwitchData,Properties.Resources.SwitchData},
        });

        public readonly static ReadOnlyDictionary<string, byte[]> DefaultBackgrounds = new(new Dictionary<string, byte[]>
        {
            {"101462661.jpg", Properties.Resources._101462661},
            {"95945966.jpg", Properties.Resources._95945966},
            {"98797564.jpg", Properties.Resources._98797564}
        });

        public readonly static ReadOnlyDictionary<string, string> InitialConfigurations = new(new Dictionary<string, string>
        {
            { $"{BackgroundSettings}:{ChangeInterval}", "15" },
            { $"{BackgroundSettings}:{ChangeMode}", $"{SequentialMode}" },
            { $"{ProgramSettings}:{ThemeMode}", $"{LightMode}" },
            { $"{ProgramSettings}:{SpecifiedAdapter}", "" },
            { $"{ProgramSettings}:{PixivIPPreference}", "false" },
            { $"{AdvancedSettings}:{DebugMode}", "false" },
            { $"{AdvancedSettings}:{GUIDebug}", "false" },
            { $"{AdvancedSettings}:{DomainNameResolutionMethod}", $"{DnsServiceMode}" },
            { $"{AdvancedSettings}:{AcrylicDebug}", "false" },
            { $"{TemporaryData}:{PreviousPrimaryDNS}", "" },
            { $"{TemporaryData}:{PreviousAlternativeDNS}", "" },
            { $"{TemporaryData}:{IsPreviousDnsAutomatic}", "true" }
        });

        public static ObservableCollection<SwitchItem> Switchs = [];
    }
}
