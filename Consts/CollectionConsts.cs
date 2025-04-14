using System.Collections.Generic;
using System.Collections.ObjectModel;
using SNIBypassGUI.Models;
using static SNIBypassGUI.Consts.ConfigConsts;
using static SNIBypassGUI.Consts.PathConsts;

namespace SNIBypassGUI.Consts
{
    public static class CollectionConsts
    {
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
            {"125838182.jpg", Properties.Resources._125838182},
            {"7pv9go.jpg", Properties.Resources._7pv9go},
            {"5go1w8.jpg", Properties.Resources._5go1w8}
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
            { $"{TemporaryData}:{PreviousIPv4DNS}", "" },
            { $"{TemporaryData}:{PreviousIPv6DNS}", "" },
            { $"{TemporaryData}:{IsPreviousIPv4DnsAutomatic}", "true" },
            { $"{TemporaryData}:{IsPreviousIPv6DnsAutomatic}", "true" }
        });

        public static ObservableCollection<SwitchItem> Switchs = [];

        // 历史版本对应的背景图片哈希值
        public static readonly ReadOnlyDictionary<string, string[]> VersionToBackgroundHash = new(new Dictionary<string, string[]>
        {
            {"4.1", ["81619888d140f831cf4ffe881b32bc80e66443a313ab57cb35d11be009b9cc31","ba70cb9d6049c40163ec8c49371107e843d6d875dd557fbf6d2507f7feef679f","8003d9afdff15c983ce6fc6ef05448a9012f3f7dcb5a74844360657b0088cd3d","c7b7c75f72570426d548fb7ab1711128fc25928dacedbcb855e70cbf3fd17597","6cd5bc2375fea3419fe128e91a58dbbf68d1c9a67963bd215a22526685d524f1"]},
            {"4.2", ["249d31873a6b26d9b8b6b358e810b26cc2a73c6e87f654063f5cd9edfa2d1346","37d6e190877c218e05f5bab23b7666e75f5e8a4c12f6f074c23c88773b534459","3209cbd2f8d98c3fd5a7dcb5745a43da8281162851d58cc474a73534358d8df0"]},
            {"4.3", ["b98894442e928faac11653e3ccc12a80298dc985cc1b07ea055688cb8e542091","e36c5276b7afc2795bdf92b1985e24ef76bd43757d97cce3dac80b86d64697f5","a3c2ce3f05b500b2e8074cf67aa06253ec3a57897ac67782b71fff483c42e4b7","f85e65e9a1ac5457c08fb96cec87d3deeeefd98334170e275bdc901c89423935"]},
            {"4.4", ["bc8c2784278f94ab1e5ac9ee32ceceb218bc3cd6566a1daa66a16751055df1bc","273efe95ff01da6ecb990327e84e15899d360c85bcf09e0f4c72a0054083d870","a60e63127408a97491253822276d5302f5e2acf347ee015225d4a253707f5a43"]},
            {"4.5", ["9e7868a49fe88c1677a60f21f7d8ee7d97df567273826d96db4a438ccbe53c95", "94f8689a96d28ec30c08d747b582906a588dfbb0d1744ec9faa2e132e82ca4cd", "5f9ac233e67261858d2aed0f50acdac5ccda89ab89bc8ad77481a5a6a622ed5b"]}
        });
    }
}
