using System.Collections.Generic;
using System.Collections.ObjectModel;
using SNIBypassGUI.Models;
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

        public readonly static ReadOnlyDictionary<string, string> InitialConfigurations = new(new Dictionary<string, string>
        {
            { "程序设置:Background", "Default" },
            { "程序设置:ThemeMode", "Light" },
            { "程序设置:SpecifiedAdapter", "" },
            { "程序设置:PixivIPPreference", "false" },
            { "高级设置:DebugMode", "false" },
            { "高级设置:GUIDebug", "false" },
            { "高级设置:DomainNameResolutionMethod", "DnsService" },
            { "高级设置:AcrylicDebug", "false" },
            { "暂存数据:PreviousDNS1", "" },
            { "暂存数据:PreviousDNS2", "" },
            { "暂存数据:IsPreviousDnsAutomatic", "true" }
        });

        public static ObservableCollection<SwitchItem> Switchs = [];
    }
}
