using System;
using System.IO;
using System.Windows;
using static SNIBypassGUI.LogHelper;
using System.Security.Cryptography.X509Certificates;
using RpNet.FileHelper;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Windows.Documents;


namespace SNIBypassGUI
{
    public class PublicHelper
    {
        // 是否输出日志
        public static bool OutputLog = false;

        // 证书指纹
        public const string Thumbprint = "BF19E93137660E4A517DDBF4DDC015CDC8760E37";

        public static FilesINI ConfigINI = new FilesINI();

        // 既定版本号，更新时需要修改
        public const string PresetGUIVersion = "V3.4";

        // 用于判断是否需要禁用适配器IPv6的域名
        public const string DomainForIPv6DisableDecision = "pixiv.net";

        // 默认一言
        public const string PresetYiyan = "行远自迩，登高自卑。";
        public const string PresetYiyanForm = "—— 戴圣「礼记」";

        // 字符串转换为布尔值的类
        public class StringBoolConverter
        {
            /// <summary>
            /// 将字符串转换为布尔值。
            /// 支持 "true" 和 "false"（不区分大小写），其他值返回 false。
            /// </summary>
            /// <param name="input">要转换的字符串</param>
            /// <returns>转换后的布尔值</returns>
            public static bool StringToBool(string input)
            {
                if (string.IsNullOrEmpty(input))
                {
                    return false; // 空字符串或null返回false
                }
                string booleanString = input.Trim().ToLower(); // 去除空格并转换为小写

                return booleanString == "true";
            }

            public static string BoolToYesNo(bool? input)
            {
                if (input == true)
                {
                    return "是";
                }
                else if (input == false)
                {
                    return "否";
                }
                else
                {
                    return "未知";
                }
            }

            // 扩展方法，支持处理null值，null值返回false
            public static bool? StringToBoolNullable(string input)
            {
                if (input == null)
                {
                    return null; // null值返回null（可空布尔值）
                }
                return StringToBool(input); // 调用非可空版本的方法
            }
        }

        // 用于安装证书
        public static bool InstallCertificate()
        {
            WriteLog("进入InstallCertificate。", LogLevel.Debug);

            // 创建一个指向当前用户根证书存储的X509Store对象
            // StoreName.Root表示根证书存储，StoreLocation.CurrentUser表示当前用户的证书存储
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            try
            {
                // 以最大权限打开证书存储，以便进行添加、删除等操作
                store.Open(OpenFlags.MaxAllowed);
                // 获取证书存储中的所有证书
                X509Certificate2Collection collection = store.Certificates;
                // 在证书存储中查找具有指定指纹的证书
                // X509FindType.FindByThumbprint 表示按指纹查找，false 表示不区分大小写（对于指纹查找无效，因为指纹是唯一的）
                X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);

                // 检查是否找到了具有该指纹的证书
                if (fcollection != null)
                {
                    // 如果找到了证书，则检查证书的数量
                    if (fcollection.Count > 0)
                    {
                        WriteLog($"检测到证书数量为{fcollection.Count}，进行移除。", LogLevel.Info);

                        // 从存储中移除找到的证书（如果存在多个相同指纹的证书，将移除所有）
                        store.RemoveRange(fcollection);
                    }
                    // 检查指定的证书文件是否存在
                    if (File.Exists(PathsSet.CERFile))
                    {
                        WriteLog($"证书文件{PathsSet.CERFile}存在，进行安装。", LogLevel.Info);

                        // 从文件中加载证书
                        X509Certificate2 x509 = new X509Certificate2(PathsSet.CERFile);
                        // 将证书添加到存储中
                        store.Add(x509);
                    }
                }
                // 如果没有找到证书集合（理论上不应该发生，除非Thumbprint为空或格式错误）

                WriteLog("完成InstallCertificate，返回true。", LogLevel.Debug);

                return true;
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常。", LogLevel.Error, ex);

                // 如果在安装证书过程中发生异常，则显示错误消息框
                HandyControl.Controls.MessageBox.Show($"安装证书失败！\r\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);

                WriteLog("完成InstallCertificate，返回false。", LogLevel.Debug);

                return false;
            }
            finally
            {
                // 无论是否发生异常，都关闭证书存储
                store.Close();

                WriteLog($"证书存储关闭。", LogLevel.Debug);
            }
        }

        public static Dictionary<string, byte[]> PathToResourceDic = new Dictionary<string, byte[]>
        {
            {PathsSet.nginxPath, Properties.Resources.SNIBypass},
            {PathsSet.nginxConfigFile_A, Properties.Resources.nginx},
            {PathsSet.nginxConfigFile_B, Properties.Resources.bypass},
            {PathsSet.nginxConfigFile_C, Properties.Resources.shared_proxy_params_1},
            {PathsSet.nginxConfigFile_D, Properties.Resources.shared_proxy_params_2},
            {PathsSet.nginxConfigFile_E, Properties.Resources.cert},
            {PathsSet.CERFile,Properties.Resources.ca},
            {PathsSet.CRTFile,Properties.Resources.SNIBypassCrt},
            {PathsSet.KeyFile,Properties.Resources.SNIBypassKey},
            {PathsSet.AcrylicServiceExeFilePath,Properties.Resources.AcrylicService},
            {PathsSet.AcrylicHostsPath,Properties.Resources.AcrylicHosts},
            {PathsSet.AcrylicConfigurationPath,Properties.Resources.AcrylicConfiguration},
            {PathsSet.HelpVideo_如何寻找活动适配器_Path, Properties.Resources.如何寻找活动适配器 },
            {PathsSet.HelpVideo_如何手动设置适配器_Path, Properties.Resources.如何手动设置适配器 },
            {PathsSet.HelpVideo_如何手动还原适配器_Path, Properties.Resources.如何手动还原适配器 },
            {PathsSet.HelpVideo_自定义背景操作_Path, Properties.Resources.自定义背景操作 }
        };

        public static Dictionary<string, string> InitialConfigurations = new Dictionary<string, string>
        {
            { "程序设置:Background", "Preset" },
            { "程序设置:ActiveAdapter", "" },
            { "高级设置:DebugMode", "false" },
            { "高级设置:GUIDebug", "false" },
            { "高级设置:DomainNameResolutionMethod", "DnsService" },
            { "高级设置:AcrylicDebug", "false" },
            { "暂存数据:PreviousDNS1", "" },
            { "暂存数据:PreviousDNS2", "" },
            { "暂存数据:IsPreviousDnsAutomatic", "true" }
        };

        public class SwitchItem
        {
            public string FaviconImageSource { get; set; }
            public string SwitchTitle { get; set; }
            public string LinksText { get; set; }
            public string ToggleButtonName { get; set; }
            public string SectionName { get; set; }
            public string[] HostsRecord { get; set; }
            public string[] OldHostsRecord { get; set; }
        }

        public static ObservableCollection<SwitchItem> Switchs = new ObservableCollection<SwitchItem>
        {
            new SwitchItem {FaviconImageSource = "Resources/favicons/amazoncojp.ico", SwitchTitle = "Amazon（日本）", LinksText = "amazon.co.jp", ToggleButtonName="amazoncojpTB", SectionName = "Amazon（日本）", HostsRecord = HostsSet.AmazoncojpSection, OldHostsRecord = HostsSet_Old.AmazoncojpSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/archiveofourown.ico", SwitchTitle = "Archive of Our Own", LinksText = "archiveofourown.org", ToggleButtonName="archiveofourownTB", SectionName = "Archive of Our Own", HostsRecord = HostsSet.ArchiveofOurOwnSection, OldHostsRecord = HostsSet_Old.ArchiveofOurOwnSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/apkmirror.png", SwitchTitle = "APKMirror", LinksText = "apkmirror.com", ToggleButtonName="apkmirrorTB", SectionName = "APKMirror", HostsRecord = HostsSet.APKMirrorSection, OldHostsRecord = HostsSet_Old.APKMirrorSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/bbc.png", SwitchTitle = "BBC（未完整支持）", LinksText = "bbc.com", ToggleButtonName="bbcTB", SectionName = "BBC", HostsRecord = HostsSet.BBCSection, OldHostsRecord = HostsSet_Old.BBCSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/e-hentai.ico", SwitchTitle = "E-Hentai（含里站）", LinksText = "e-hentai.org|、|exhentai.org", ToggleButtonName="ehentaiTB", SectionName = "E-Hentai", HostsRecord = HostsSet.EHentaiSection, OldHostsRecord = HostsSet_Old.EHentaiSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/etsy.png", SwitchTitle = "Etsy", LinksText = "etsy.com", ToggleButtonName="etsyTB", SectionName = "Etsy", HostsRecord = HostsSet.EtsySection, OldHostsRecord = HostsSet_Old.EtsySection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/fdroid.png", SwitchTitle = "F-Droid（未完整支持）", LinksText = "f-droid.org", ToggleButtonName="fdroidTB", SectionName = "F-Droid", HostsRecord = HostsSet.FDroidSection, OldHostsRecord = HostsSet_Old.FDroidSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/google.png", SwitchTitle = "谷歌搜索", LinksText = "google.com", ToggleButtonName="googleTB", SectionName = "Google", HostsRecord = HostsSet.GoogleSection, OldHostsRecord = HostsSet_Old.GoogleSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/nyaa.png", SwitchTitle = "Nyaa（含里站）", LinksText = "nyaa.si|、|sukebei.nyaa.si", ToggleButtonName="nyaaTB", SectionName = "Nyaa", HostsRecord = HostsSet.NyaaSection, OldHostsRecord = HostsSet_Old.NyaaSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/ok.png", SwitchTitle = "OK", LinksText = "ok.ru", ToggleButtonName="okTB", SectionName = "OK", HostsRecord = HostsSet.OKSection, OldHostsRecord = HostsSet_Old.OKSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/okx.png", SwitchTitle = "OKX.COM", LinksText = "okx.com", ToggleButtonName="okxTB", SectionName = "OKX.COM", HostsRecord = HostsSet.OKXCOMSection, OldHostsRecord = HostsSet_Old.OKXCOMSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/pixiv.ico", SwitchTitle = "Pixiv", LinksText = "pixiv.net", ToggleButtonName="pixivTB", SectionName = "Pixiv", HostsRecord = HostsSet.PixivSection, OldHostsRecord = HostsSet_Old.PixivSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/pixivFANBOX.ico", SwitchTitle = "pixivFANBOX", LinksText = "fanbox.cc", ToggleButtonName="fanboxTB", SectionName = "pixivFANBOX", HostsRecord = HostsSet.pixivFANBOXSection, OldHostsRecord = HostsSet_Old.pixivFANBOXSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/pornhub.ico", SwitchTitle = "Pornhub（不稳定）", LinksText = "pornhub.com", ToggleButtonName="pornhubTB", SectionName = "Pornhub", HostsRecord = HostsSet.PornhubSection, OldHostsRecord = HostsSet_Old.PornhubSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/proton.png", SwitchTitle = "Proton", LinksText = "proton.me", ToggleButtonName="protonTB", SectionName = "Proton", HostsRecord = HostsSet.ProtonSection, OldHostsRecord = HostsSet_Old.ProtonSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/steamcommunity.ico", SwitchTitle = "Steam Community", LinksText = "steamcommunity.com", ToggleButtonName="steamcommunityTB", SectionName = "Steam Community", HostsRecord = HostsSet.SteamCommunitySection, OldHostsRecord = HostsSet_Old.SteamCommunitySection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/telegram.png", SwitchTitle = "Telegram", LinksText = "telegram.org", ToggleButtonName="telegramTB", SectionName = "Telegram", HostsRecord = HostsSet.TelegramSection, OldHostsRecord = HostsSet_Old.TelegramSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/thenewyorktimes.png", SwitchTitle = "The New York Times", LinksText = "nytimes.com", ToggleButtonName="thenewyorktimesTB", SectionName = "The New York Times", HostsRecord = HostsSet.TheNewYorkTimesSection, OldHostsRecord = HostsSet_Old.TheNewYorkTimesSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/wallhaven.ico", SwitchTitle = "Wallhaven（未完整支持）", LinksText = "wallhaven.cc", ToggleButtonName="wallhavenTB", SectionName = "Wallhaven", HostsRecord = HostsSet.WallhavenSection, OldHostsRecord = HostsSet_Old.WallhavenSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/wikimediafoundation.ico", SwitchTitle = "Wikimedia 全项目", LinksText = "wikipedia.org|、|wiktionary.org|等", ToggleButtonName="wikimediafoundationTB", SectionName = "Wikimedia Foundation", HostsRecord = HostsSet.WikimediaFoundationSection, OldHostsRecord = HostsSet_Old.WikimediaFoundationSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/youtube.png", SwitchTitle = "YouTube（未完整支持）", LinksText = "www.youtube.com", ToggleButtonName="youtubeTB", SectionName = "YouTube", HostsRecord = HostsSet.YoutubeSection, OldHostsRecord = HostsSet_Old.YoutubeSection},
            new SwitchItem {FaviconImageSource = "Resources/favicons/zlibrary.png", SwitchTitle = "Z-Library", LinksText = "1lib.sk|、|z-lib.fm", ToggleButtonName="zlibraryTB", SectionName = "Z-Library", HostsRecord = HostsSet.ZLibrarySection, OldHostsRecord = HostsSet_Old.ZLibrarySection}
        };
    }
}