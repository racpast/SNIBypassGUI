using System.Collections.Generic;
using System.Collections.ObjectModel;
using static SNIBypassGUI.Consts.PathConsts;
using SNIBypassGUI.Models;
using Microsoft.PowerShell.Commands;

namespace SNIBypassGUI.Consts
{
    public static class CollectionConsts
    {
        public static string[] pximgIP =
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

        public static Dictionary<string, byte[]> PathToResourceDic = new()
        {
            {nginxPath, Properties.Resources.SNIBypass},
            {nginxConfigFile, Properties.Resources.nginx},
            {CERFile,Properties.Resources.ca},
            {CRTFile,Properties.Resources.SNIBypassCrt},
            {KeyFile,Properties.Resources.SNIBypassKey},
            {AcrylicServiceExeFilePath,Properties.Resources.AcrylicService},
            {AcrylicHostsPath,Properties.Resources.AcrylicHosts},
            {AcrylicConfigurationPath,Properties.Resources.AcrylicConfiguration}
        };

        public static Dictionary<string, string> InitialConfigurations = new()
        {
            { "程序设置:Background", "Default" },
            { "程序设置:SpecifiedAdapter", "" },
            { "程序设置:PixivIPPreference", "false" },
            { "高级设置:DebugMode", "false" },
            { "高级设置:GUIDebug", "false" },
            { "高级设置:DomainNameResolutionMethod", "DnsService" },
            { "高级设置:AcrylicDebug", "false" },
            { "暂存数据:PreviousDNS1", "" },
            { "暂存数据:PreviousDNS2", "" },
            { "暂存数据:IsPreviousDnsAutomatic", "true" }
        };



        public static ObservableCollection<SwitchItem> Switchs =
        [
            new() {FaviconImageSource = "Resources/favicons/amazoncojp.ico", SwitchTitle = "Amazon（日本）", LinksText = ["amazon.co.jp"], ToggleButtonName="amazoncojpTB", SectionName = "Amazon（日本）", AcrylicHostsRecord = AcrylicHostsConsts.AmazoncojpSection, SystemHostsRecord = SystemHostsConsts.AmazoncojpSection},
            new() {FaviconImageSource = "Resources/favicons/archiveofourown.ico", SwitchTitle = "Archive of Our Own", LinksText = ["archiveofourown.org"], ToggleButtonName="archiveofourownTB", SectionName = "Archive of Our Own", AcrylicHostsRecord = AcrylicHostsConsts.ArchiveofOurOwnSection, SystemHostsRecord = SystemHostsConsts.ArchiveofOurOwnSection},
            new() {FaviconImageSource = "Resources/favicons/apkmirror.png", SwitchTitle = "APKMirror", LinksText = ["apkmirror.com"], ToggleButtonName="apkmirrorTB", SectionName = "APKMirror", AcrylicHostsRecord = AcrylicHostsConsts.APKMirrorSection, SystemHostsRecord = SystemHostsConsts.APKMirrorSection},
            new() {FaviconImageSource = "Resources/favicons/bbc.png", SwitchTitle = "BBC（未完整支持）", LinksText = ["bbc.com"], ToggleButtonName="bbcTB", SectionName = "BBC", AcrylicHostsRecord = AcrylicHostsConsts.BBCSection, SystemHostsRecord = SystemHostsConsts.BBCSection},
            new() {FaviconImageSource = "Resources/favicons/duckduckgo.ico", SwitchTitle = "DuckDuckGo", LinksText = ["duckduckgo.com"], ToggleButtonName="duckduckgoTB", SectionName = "DuckDuckGo", AcrylicHostsRecord = AcrylicHostsConsts.DuckDuckGoSection, SystemHostsRecord = SystemHostsConsts.DuckDuckGoSection},
            new() {FaviconImageSource = "Resources/favicons/e-hentai.ico", SwitchTitle = "E-Hentai（含里站）", LinksText = ["e-hentai.org", "、", "exhentai.org"], ToggleButtonName="ehentaiTB", SectionName = "E-Hentai", AcrylicHostsRecord = AcrylicHostsConsts.EHentaiSection, SystemHostsRecord = SystemHostsConsts.EHentaiSection},
            new() {FaviconImageSource = "Resources/favicons/etsy.png", SwitchTitle = "Etsy", LinksText = ["etsy.com"], ToggleButtonName="etsyTB", SectionName = "Etsy", AcrylicHostsRecord = AcrylicHostsConsts.EtsySection, SystemHostsRecord = SystemHostsConsts.EtsySection},
            new() {FaviconImageSource = "Resources/favicons/fdroid.png", SwitchTitle = "F-Droid", LinksText = ["f-droid.org"], ToggleButtonName="fdroidTB", SectionName = "F-Droid", AcrylicHostsRecord = AcrylicHostsConsts.FDroidSection, SystemHostsRecord = SystemHostsConsts.FDroidSection},
            new() {FaviconImageSource = "Resources/favicons/gelbooru.png", SwitchTitle = "Gelbooru", LinksText = ["gelbooru.com"], ToggleButtonName="gelbooruTB", SectionName = "Gelbooru", AcrylicHostsRecord = AcrylicHostsConsts.GelbooruSection, SystemHostsRecord = SystemHostsConsts.GelbooruSection },
            new() {FaviconImageSource = "Resources/favicons/github.png", SwitchTitle = "Github", LinksText = ["github.com"], ToggleButtonName="githubTB", SectionName = "Github", AcrylicHostsRecord = AcrylicHostsConsts.GithubSection, SystemHostsRecord = SystemHostsConsts.GithubSection},
            new() {FaviconImageSource = "Resources/favicons/google.png", SwitchTitle = "谷歌搜索", LinksText = ["google.com"], ToggleButtonName="googleTB", SectionName = "Google", AcrylicHostsRecord = AcrylicHostsConsts.GoogleSection, SystemHostsRecord = SystemHostsConsts.GoogleSection},
            new() {FaviconImageSource = "Resources/favicons/greasyfork.png", SwitchTitle = "Greasy Fork", LinksText = ["greasyfork.org"], ToggleButtonName="greasyforkTB", SectionName = "Greasy Fork", AcrylicHostsRecord = AcrylicHostsConsts.GreasyForkSection, SystemHostsRecord = SystemHostsConsts.GreasyForkSection},
            new() {FaviconImageSource = "Resources/favicons/iwara.png", SwitchTitle = "Iwara", LinksText = ["iwara.tv"], ToggleButtonName= "iwaraTB", SectionName = "Iwara", AcrylicHostsRecord = AcrylicHostsConsts.IwaraSection, SystemHostsRecord = SystemHostsConsts.IwaraSection},
            new() {FaviconImageSource = "Resources/favicons/nyaa.png", SwitchTitle = "Nyaa（含里站）", LinksText = ["nyaa.si", "、", "sukebei.nyaa.si"], ToggleButtonName="nyaaTB", SectionName = "Nyaa", AcrylicHostsRecord = AcrylicHostsConsts.NyaaSection, SystemHostsRecord = SystemHostsConsts.NyaaSection},
            new() {FaviconImageSource = "Resources/favicons/ok.png", SwitchTitle = "OK", LinksText = ["ok.ru"], ToggleButtonName="okTB", SectionName = "OK", AcrylicHostsRecord = AcrylicHostsConsts.OKSection, SystemHostsRecord = SystemHostsConsts.OKSection},
            new() {FaviconImageSource = "Resources/favicons/okx.png", SwitchTitle = "OKX.COM", LinksText = ["okx.com"], ToggleButtonName="okxTB", SectionName = "OKX.COM", AcrylicHostsRecord = AcrylicHostsConsts.OKXCOMSection, SystemHostsRecord = SystemHostsConsts.OKXCOMSection},
            new() {FaviconImageSource = "Resources/favicons/pixiv.ico", SwitchTitle = "Pixiv", LinksText = ["pixiv.net"], ToggleButtonName="pixivTB", SectionName = "Pixiv", AcrylicHostsRecord = AcrylicHostsConsts.PixivSection, SystemHostsRecord = SystemHostsConsts.PixivSection},
            new() {FaviconImageSource = "Resources/favicons/pixivFANBOX.ico", SwitchTitle = "pixivFANBOX", LinksText = ["fanbox.cc"], ToggleButtonName="fanboxTB", SectionName = "pixivFANBOX", AcrylicHostsRecord = AcrylicHostsConsts.pixivFANBOXSection, SystemHostsRecord = SystemHostsConsts.pixivFANBOXSection},
            new() {FaviconImageSource = "Resources/favicons/pornhub.ico", SwitchTitle = "Pornhub", LinksText = ["pornhub.com"], ToggleButtonName="pornhubTB", SectionName = "Pornhub", AcrylicHostsRecord = AcrylicHostsConsts.PornhubSection, SystemHostsRecord = SystemHostsConsts.PornhubSection},
            new() {FaviconImageSource = "Resources/favicons/proton.png", SwitchTitle = "Proton", LinksText = ["proton.me"], ToggleButtonName="protonTB", SectionName = "Proton", AcrylicHostsRecord = AcrylicHostsConsts.ProtonSection, SystemHostsRecord = SystemHostsConsts.ProtonSection},
            new() {FaviconImageSource = "Resources/favicons/rule34video.png", SwitchTitle = "Rule34Video", LinksText = ["rule34video.com"], ToggleButtonName="rule34videoTB", SectionName = "Rule34Video", AcrylicHostsRecord = AcrylicHostsConsts.Rule34VideoSection, SystemHostsRecord = SystemHostsConsts.Rule34VideoSection},
            new() {FaviconImageSource = "Resources/favicons/sankakucomplex.ico", SwitchTitle = "Sankaku Complex", LinksText = ["sankakucomplex.com"], ToggleButtonName="sankakucomplexTB", SectionName = "Sankaku Complex", AcrylicHostsRecord = AcrylicHostsConsts.SankakuComplexSection, SystemHostsRecord = SystemHostsConsts.SankakuComplexSection},
            new() {FaviconImageSource = "Resources/favicons/steamcommunity.ico", SwitchTitle = "Steam Community", LinksText = ["steamcommunity.com"], ToggleButtonName="steamcommunityTB", SectionName = "Steam Community", AcrylicHostsRecord = AcrylicHostsConsts.SteamCommunitySection, SystemHostsRecord = SystemHostsConsts.SteamCommunitySection},
            new() {FaviconImageSource = "Resources/favicons/telegram.png", SwitchTitle = "Telegram", LinksText = ["telegram.org"], ToggleButtonName="telegramTB", SectionName = "Telegram", AcrylicHostsRecord = AcrylicHostsConsts.TelegramSection, SystemHostsRecord = SystemHostsConsts.TelegramSection},
            new() {FaviconImageSource = "Resources/favicons/thenewyorktimes.png", SwitchTitle = "The New York Times", LinksText = ["nytimes.com"], ToggleButtonName="thenewyorktimesTB", SectionName = "The New York Times", AcrylicHostsRecord = AcrylicHostsConsts.TheNewYorkTimesSection, SystemHostsRecord = SystemHostsConsts.TheNewYorkTimesSection},
            new() {FaviconImageSource = "Resources/favicons/wallhaven.ico", SwitchTitle = "Wallhaven（未完整支持）", LinksText = ["wallhaven.cc"], ToggleButtonName="wallhavenTB", SectionName = "Wallhaven", AcrylicHostsRecord = AcrylicHostsConsts.WallhavenSection, SystemHostsRecord = SystemHostsConsts.WallhavenSection},
            new() {FaviconImageSource = "Resources/favicons/wikimediafoundation.ico", SwitchTitle = "Wikimedia 全项目", LinksText = ["wikipedia.org", "、", "wiktionary.org", "等"], ToggleButtonName="wikimediafoundationTB", SectionName = "Wikimedia Foundation", AcrylicHostsRecord = AcrylicHostsConsts.WikimediaFoundationSection, SystemHostsRecord = SystemHostsConsts.WikimediaFoundationSection},
            new() {FaviconImageSource = "Resources/favicons/youtube.png", SwitchTitle = "YouTube（IPv6 完全支持）", LinksText = ["www.youtube.com"], ToggleButtonName="youtubeTB", SectionName = "YouTube", AcrylicHostsRecord = AcrylicHostsConsts.YoutubeSection, SystemHostsRecord = SystemHostsConsts.YoutubeSection},
            new() {FaviconImageSource = "Resources/favicons/zlibrary.png", SwitchTitle = "Z-Library", LinksText = ["1lib.sk", "、", "z-lib.fm"], ToggleButtonName="zlibraryTB", SectionName = "Z-Library", AcrylicHostsRecord = AcrylicHostsConsts.ZLibrarySection, SystemHostsRecord = SystemHostsConsts.ZLibrarySection}
        ];
    }
}
