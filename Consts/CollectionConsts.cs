using System.Collections.Generic;
using System.Collections.ObjectModel;
using SNIBypassGUI.Models;
using static SNIBypassGUI.Consts.ConfigConsts;
using static SNIBypassGUI.Consts.PathConsts;

namespace SNIBypassGUI.Consts
{
    public static class CollectionConsts
    {
        public static readonly string[] pximgIP =
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

        public static readonly ReadOnlyDictionary<string, byte[]> PathToResourceDic = new(new Dictionary<string, byte[]>
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

        public static readonly ReadOnlyDictionary<string, byte[]> DefaultBackgrounds = new(new Dictionary<string, byte[]>
        {
            {"117397102.jpg", Properties.Resources._117397102},
            {"95215205.jpg", Properties.Resources._95215205},
            {"116538352.jpg", Properties.Resources._116538352},
            {"122289031.jpg", Properties.Resources._122289031}
        });

        public static readonly ReadOnlyDictionary<string, string> InitialConfigurations = new(new Dictionary<string, string>
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

        // 历史版本对应的背景图片哈希值
        public static readonly ReadOnlyDictionary<string, string[]> VersionToBackgroundHash = new(new Dictionary<string, string[]>
        {
            {"4.1", ["81619888d140f831cf4ffe881b32bc80e66443a313ab57cb35d11be009b9cc31","ba70cb9d6049c40163ec8c49371107e843d6d875dd557fbf6d2507f7feef679f","8003d9afdff15c983ce6fc6ef05448a9012f3f7dcb5a74844360657b0088cd3d","c7b7c75f72570426d548fb7ab1711128fc25928dacedbcb855e70cbf3fd17597","6cd5bc2375fea3419fe128e91a58dbbf68d1c9a67963bd215a22526685d524f1"]},
            {"4.2", ["249d31873a6b26d9b8b6b358e810b26cc2a73c6e87f654063f5cd9edfa2d1346","37d6e190877c218e05f5bab23b7666e75f5e8a4c12f6f074c23c88773b534459","3209cbd2f8d98c3fd5a7dcb5745a43da8281162851d58cc474a73534358d8df0"]}
        });
    }
}
