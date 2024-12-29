using System;
using System.IO;
using System.Windows;
using static SNIBypassGUI.LogHelper;
using System.Security.Cryptography.X509Certificates;
using RpNet.FileHelper;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;


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

        public static string[] SectionNamesSet = new string[]
        {
            "Amazon（日本）",
            "Archive of Our Own",
            "APKMirror",
            "BBC",
            "E-Hentai",
            "Etsy",
            "F-Droid",
            "Google",
            "Nyaa",
            "OK",
            "OKX.COM",
            "Pixiv",
            "Pornhub",
            "Proton",
            "Steam Community",
            "Telegram",
            "The New York Times",
            "Wallhaven",
            "Wikimedia Foundation",
            "YouTube",
            "Z-Library"
        };

        public static Dictionary<string, string[]> SectionNameToHostsRecordDic = new Dictionary<string, string[]>
        {
            {"Amazon（日本）",HostsSet.AmazoncojpSection},
            {"Archive of Our Own",HostsSet.ArchiveofOurOwnSection},
            {"APKMirror",HostsSet.APKMirrorSection},
            {"BBC",HostsSet.BBCSection},
            {"E-Hentai",HostsSet.EHentaiSection},
            {"Etsy",HostsSet.EtsySection},
            {"F-Droid",HostsSet.FDroidSection},
            {"Google",HostsSet.GoogleSection},
            {"Nyaa",HostsSet.NyaaSection},
            {"OK",HostsSet.OKSection},
            {"OKX.COM",HostsSet.OKXCOMSection},
            {"Pixiv",HostsSet.PixivSection},
            {"Pornhub",HostsSet.PornhubSection},
            {"Proton",HostsSet.ProtonSection},
            {"Steam Community",HostsSet.SteamCommunitySection},
            {"Telegram",HostsSet.TelegramSection},
            {"The New York Times",HostsSet.TheNewYorkTimesSection},
            {"Wallhaven",HostsSet.WallhavenSection},
            {"Wikimedia Foundation",HostsSet.WikimediaFoundationSection},
            {"YouTube",HostsSet.YoutubeSection},
            {"Z-Library",HostsSet.ZLibrarySection},
        };

        public static Dictionary<string, string[]> SectionNameToOldHostsRecordDic = new Dictionary<string, string[]>
        {
            {"Amazon（日本）",HostsSet_Old.AmazoncojpSection},
            {"Archive of Our Own",HostsSet_Old.ArchiveofOurOwnSection},
            {"APKMirror",HostsSet_Old.APKMirrorSection},
            {"BBC",HostsSet_Old.BBCSection},
            {"E-Hentai",HostsSet_Old.EHentaiSection},
            {"Etsy",HostsSet_Old.EtsySection},
            {"F-Droid",HostsSet_Old.FDroidSection},
            {"Google",HostsSet_Old.GoogleSection},
            {"Nyaa",HostsSet_Old.NyaaSection},
            {"OK",HostsSet_Old.OKSection},
            {"OKX.COM",HostsSet_Old.OKXCOMSection},
            {"Pixiv",HostsSet_Old.PixivSection},
            {"Pornhub",HostsSet_Old.PornhubSection},
            {"Proton",HostsSet_Old.ProtonSection},
            {"Steam Community",HostsSet_Old.SteamCommunitySection},
            {"Telegram",HostsSet_Old.TelegramSection},
            {"The New York Times",HostsSet_Old.TheNewYorkTimesSection},
            {"Wallhaven",HostsSet_Old.WallhavenSection},
            {"Wikimedia Foundation",HostsSet_Old.WikimediaFoundationSection},
            {"YouTube",HostsSet_Old.YoutubeSection},
            {"Z-Library",HostsSet_Old.ZLibrarySection},
        };

        public static Dictionary<ToggleButton, string> ToggleButtonToSectionNamedDic = new Dictionary<ToggleButton, string>();

        // 该方法可以在 MainWindow 的 Loaded 事件中调用，用来初始化字典
        public static void InitializeToggleButtonDictionary(MainWindow mainWindow)
        {
            ToggleButtonToSectionNamedDic = new Dictionary<ToggleButton, string>
            {
                {mainWindow.amazoncojpTB,"Amazon（日本）"},
                {mainWindow.archiveofourownTB,"Archive of Our Own"},
                {mainWindow.apkmirrorTB, "APKMirror" },
                {mainWindow.bbcTB, "BBC" },
                {mainWindow.ehentaiTB, "E-Hentai" },
                {mainWindow.etsyTB, "Etsy" },
                {mainWindow.fdroidTB, "F-Droid" },
                {mainWindow.googleTB,"Google" },
                {mainWindow.nyaaTB, "Nyaa" },
                {mainWindow.okTB, "OK" },
                {mainWindow.okxTB, "OKX.COM" },
                {mainWindow.pixivTB, "Pixiv" },
                {mainWindow.pornhubTB, "Pornhub" },
                {mainWindow.protonTB, "Proton" },
                {mainWindow.steamcommunityTB,"Steam Community" },
                {mainWindow.telegramTB,"Telegram" },
                {mainWindow.thenewyorktimesTB, "The New York Times" },
                {mainWindow.wallhavenTB, "Wallhaven" },
                {mainWindow.wikimediafoundationTB, "Wikimedia Foundation" },
                {mainWindow.youtubeTB, "YouTube" },
                {mainWindow.zlibraryTB, "Z-Library" },
            };
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
            {PathsSet.HelpVideo_如何手动还原适配器_Path, Properties.Resources.如何手动还原适配器 }
        };

        public static Dictionary<string, string> InitialConfigurations = new Dictionary<string, string>
        {
            { "程序设置:IsFirst", "true" },
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
    }
}