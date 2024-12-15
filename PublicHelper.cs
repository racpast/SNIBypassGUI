using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Net.NetworkInformation;
using static SNIBypassGUI.LogHelper;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Media.Imaging;


namespace SNIBypassGUI
{
    public class PublicHelper
    {
        // 定义基本路径
        public static string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static string dataDirectory = Path.Combine(currentDirectory, "data");
        public static string NginxDirectory = Path.Combine(dataDirectory, "core");
        public static string nginxPath = Path.Combine(NginxDirectory, "SNIBypass.exe");
        public static string nginxConfigDirectory = Path.Combine(NginxDirectory, "conf");
        public static string nginxConfigFile_A = Path.Combine(nginxConfigDirectory, "nginx.conf");
        public static string nginxConfigFile_B = Path.Combine(nginxConfigDirectory, "bypass.conf");
        public static string nginxConfigFile_C = Path.Combine(nginxConfigDirectory, "shared-proxy-params-1.conf");
        public static string nginxConfigFile_D = Path.Combine(nginxConfigDirectory, "shared-proxy-params-2.conf");
        public static string nginxConfigFile_E = Path.Combine(nginxConfigDirectory, "cert.conf");
        public static string CADirectory = Path.Combine(nginxConfigDirectory, "ca");
        public static string CERFile = Path.Combine(CADirectory, "ca.cer");
        public static string CRTFile = Path.Combine(CADirectory, "SNIBypassCrt.crt");
        public static string KeyFile = Path.Combine(CADirectory, "SNIBypassKey.key");
        public static string nginxLogDirectory = Path.Combine(NginxDirectory, "logs");
        public static string nginxTempDirectory = Path.Combine(NginxDirectory, "temp");
        public static string nginxLogFile_A = Path.Combine(nginxLogDirectory, "access.log");
        public static string nginxLogFile_B = Path.Combine(nginxLogDirectory, "error.log");
        public static List<string> LogfilePaths = new List<String>{ nginxLogFile_A, nginxLogFile_B };
        public static string INIPath = Path.Combine(dataDirectory, "config.ini");
        public static string GUILogDirectory = Path.Combine(dataDirectory, "logs");
        public static string GUILogPath = Path.Combine(GUILogDirectory, "GUI.log");
        public static string SystemHosts = "C:\\Windows\\System32\\drivers\\etc\\hosts";
        // 是否输出日志
        public static bool OutputLog = false;
        // 证书指纹
        public static string Thumbprint = "BF19E93137660E4A517DDBF4DDC015CDC8760E37";

        public static FilesINI ConfigINI = new FilesINI();

        // 既定版本号，更新时需要修改
        public static string PresetGUIVersion = "V2.0";

        /// <summary>
        /// 下面是 hosts 部分
        /// </summary>
        public static string[] ArchiveofOurOwnSection =
        {
            "#\tArchive of Our Own Start",
            "127.0.0.1       archiveofourown.org",
            "127.0.0.1       www.archiveofourown.org",
            "#\tArchive of Our Own End"
        };

        public static string[] EHentaiSection =
        {
            "#\tE-Hentai Start",
            "127.0.0.1		e-hentai.org",
            "127.0.0.1		www.e-hentai.org",
            "127.0.0.1		r.e-hentai.org",
            "127.0.0.1		g.e-hentai.org",
            "127.0.0.1		exhentai.org",
            "127.0.0.1		s.exhentai.org",
            "127.0.0.1		www.exhentai.org",
            "127.0.0.1		forums.e-hentai.org",
            "127.0.0.1		api.e-hentai.org",
            "127.0.0.1		upld.e-hentai.org",
            "127.0.0.1		upload.e-hentai.org",
            "127.0.0.1		ehgt.org",
            "127.0.0.1		www.ehgt.org",
            "#\tE-Hentai End"
        };

        public static string[] NyaaSection =
        {
            "#\tNyaa Start",
            "127.0.0.1       sukebei.nyaa.si",
            "127.0.0.1       nyaa.si",
            "127.0.0.1       www.nyaa.si",
            "#\tNyaa End"
        };

        public static string[] PixivSection =
        {
            "#\tPixiv Start",
            "127.0.0.1       pixiv.net",
            "127.0.0.1       www.pixiv.net",
            "127.0.0.1       ssl.pixiv.net",
            "127.0.0.1       accounts.pixiv.net",
            "127.0.0.1       myaccount.pixiv.net",
            "127.0.0.1       touch.pixiv.net",
            "127.0.0.1       oauth.secure.pixiv.net",
            "127.0.0.1       dic.pixiv.net",
            "127.0.0.1       en-dic.pixiv.net",
            "127.0.0.1       sketch.pixiv.net",
            "127.0.0.1       payment.pixiv.net",
            "127.0.0.1       factory.pixiv.net",
            "127.0.0.1       comic.pixiv.net",
            "127.0.0.1       novel.pixiv.net",
            "127.0.0.1       imgaz.pixiv.net",
            "127.0.0.1       imp.pixiv.net",
            "127.0.0.1       sensei.pixiv.net",
            "127.0.0.1       fanbox.pixiv.net",
            "127.0.0.1       source.pixiv.net",
            "127.0.0.1       i1.pixiv.net",
            "127.0.0.1       i2.pixiv.net",
            "127.0.0.1       i3.pixiv.net",
            "127.0.0.1       i4.pixiv.net",
            "127.0.0.1       app-api.pixiv.net",
            "127.0.0.1       fanbox.cc",
            "127.0.0.1       www.fanbox.cc",
            "127.0.0.1       downloads.fanbox.cc",
            "127.0.0.1       api.fanbox.cc",
            "127.0.0.1       i.pximg.net",
            "127.0.0.1       pixiv.pximg.net",
            "127.0.0.1       a.pixiv.org",
            "127.0.0.1       d.pixiv.org",
            "#\tPixiv End"
        };

        public static string[] PornhubSection =
        {
            "#\tPornhub Start",
            "127.0.0.1       pornhub.com",
            "127.0.0.1       www.pornhub.com",
            "127.0.0.1       cn.pornhub.com",
            "127.0.0.1       gstatic.com",
            "127.0.0.1       www.gstatic.com",
            "127.0.0.1       cdn.jsdelivr.net",
            "127.0.0.1       jsdelivr.net",
            "#\tPornhub End"
        };

        public static string[] SteamCommunitySection =
        {
            "#\tSteam Community Start",
            "127.0.0.1       steamcommunity.com",
            "127.0.0.1       www.steamcommunity.com",
            "#\tSteam Community End"
        };

        public static string[] WikimediaFoundationSection =
        {
            "#\tWikimedia Foundation Start",
            "127.0.0.1       wikipedia.org",
            "127.0.0.1       www.wikipedia.org",
            "127.0.0.1       ar.wikipedia.org",
            "127.0.0.1       es.wikipedia.org",
            "127.0.0.1       it.wikipedia.org",
            "127.0.0.1       ja.wikipedia.org",
            "127.0.0.1       ceb.wikipedia.org",
            "127.0.0.1       uk.wikipedia.org",
            "127.0.0.1       zh.wikipedia.org",
            "127.0.0.1       de.wikipedia.org",
            "127.0.0.1       fa.wikipedia.org",
            "127.0.0.1       arz.wikipedia.org",
            "127.0.0.1       pl.wikipedia.org",
            "127.0.0.1       vi.wikipedia.org",
            "127.0.0.1       ru.wikipedia.org",
            "127.0.0.1       en.wikipedia.org",
            "127.0.0.1       fr.wikipedia.org",
            "127.0.0.1       nl.wikipedia.org",
            "127.0.0.1       pt.wikipedia.org",
            "127.0.0.1       sv.wikipedia.org",
            "127.0.0.1       war.wikipedia.org",
            "127.0.0.1       zh-yue.wikipedia.org",
            "127.0.0.1       wuu.wikipedia.org",
            "127.0.0.1       ug.wikipedia.org",
            "127.0.0.1       m.wikipedia.org",
            "127.0.0.1       ar.m.wikipedia.org",
            "127.0.0.1       es.m.wikipedia.org",
            "127.0.0.1       it.m.wikipedia.org",
            "127.0.0.1       ja.m.wikipedia.org",
            "127.0.0.1       ceb.m.wikipedia.org",
            "127.0.0.1       uk.m.wikipedia.org",
            "127.0.0.1       zh.m.wikipedia.org",
            "127.0.0.1       de.m.wikipedia.org",
            "127.0.0.1       fa.m.wikipedia.org",
            "127.0.0.1       arz.m.wikipedia.org",
            "127.0.0.1       pl.m.wikipedia.org",
            "127.0.0.1       vi.m.wikipedia.org",
            "127.0.0.1       ru.m.wikipedia.org",
            "127.0.0.1       en.m.wikipedia.org",
            "127.0.0.1       fr.m.wikipedia.org",
            "127.0.0.1       nl.m.wikipedia.org",
            "127.0.0.1       pt.m.wikipedia.org",
            "127.0.0.1       sv.m.wikipedia.org",
            "127.0.0.1       war.m.wikipedia.org",
            "127.0.0.1       zh-yue.m.wikipedia.org",
            "127.0.0.1       wuu.m.wikipedia.org",
            "127.0.0.1       ug.m.wikipedia.org",
            "127.0.0.1       wikimedia.org",
            "127.0.0.1       www.wikimedia.org",
            "127.0.0.1       commons.wikimedia.org",
            "127.0.0.1       upload.wikimedia.org",
            "127.0.0.1       meta.wikimedia.org",
            "127.0.0.1       foundation.wikimedia.org",
            "127.0.0.1       incubator.wikimedia.org",
            "127.0.0.1       wikitech.wikimedia.org",
            "127.0.0.1       donate.wikimedia.org",
            "127.0.0.1       stats.wikimedia.org",
            "127.0.0.1       species.wikimedia.org",
            "127.0.0.1       developer.wikimedia.org",
            "127.0.0.1       wikimania.wikimedia.org",
            "127.0.0.1       wikimania2005.wikimedia.org",
            "127.0.0.1       wikimania2006.wikimedia.org",
            "127.0.0.1       wikimania2007.wikimedia.org",
            "127.0.0.1       wikimania2008.wikimedia.org",
            "127.0.0.1       wikimania2009.wikimedia.org",
            "127.0.0.1       wikimania2010.wikimedia.org",
            "127.0.0.1       wikimania2011.wikimedia.org",
            "127.0.0.1       wikimania2012.wikimedia.org",
            "127.0.0.1       wikimania2013.wikimedia.org",
            "127.0.0.1       wikimania2014.wikimedia.org",
            "127.0.0.1       wikimania2015.wikimedia.org",
            "127.0.0.1       wikimania2016.wikimedia.org",
            "127.0.0.1       wikimania2017.wikimedia.org",
            "127.0.0.1       wikimania2018.wikimedia.org",
            "127.0.0.1       m.wikimedia.org",
            "127.0.0.1       commons.m.wikimedia.org",
            "127.0.0.1       meta.m.wikimedia.org",
            "127.0.0.1       foundation.m.wikimedia.org",
            "127.0.0.1       incubator.m.wikimedia.org",
            "127.0.0.1       donate.m.wikimedia.org",
            "127.0.0.1       species.m.wikimedia.org",
            "127.0.0.1       wikimania.m.wikimedia.org",
            "127.0.0.1       wikimania2005.m.wikimedia.org",
            "127.0.0.1       wikimania2006.m.wikimedia.org",
            "127.0.0.1       wikimania2007.m.wikimedia.org",
            "127.0.0.1       wikimania2008.m.wikimedia.org",
            "127.0.0.1       wikimania2009.m.wikimedia.org",
            "127.0.0.1       wikimania2010.m.wikimedia.org",
            "127.0.0.1       wikimania2011.m.wikimedia.org",
            "127.0.0.1       wikimania2012.m.wikimedia.org",
            "127.0.0.1       wikimania2013.m.wikimedia.org",
            "127.0.0.1       wikimania2014.m.wikimedia.org",
            "127.0.0.1       wikimania2015.m.wikimedia.org",
            "127.0.0.1       wikimania2016.m.wikimedia.org",
            "127.0.0.1       wikimania2017.m.wikimedia.org",
            "127.0.0.1       wikimania2018.m.wikimedia.org",
            "127.0.0.1       wikivoyage.org",
            "127.0.0.1       www.wikivoyage.org",
            "127.0.0.1       de.wikivoyage.org",
            "127.0.0.1       en.wikivoyage.org",
            "127.0.0.1       it.wikivoyage.org",
            "127.0.0.1       pl.wikivoyage.org",
            "127.0.0.1       bn.wikivoyage.org",
            "127.0.0.1       es.wikivoyage.org",
            "127.0.0.1       fa.wikivoyage.org",
            "127.0.0.1       he.wikivoyage.org",
            "127.0.0.1       ja.wikivoyage.org",
            "127.0.0.1       fi.wikivoyage.org",
            "127.0.0.1       uk.wikivoyage.org",
            "127.0.0.1       zh.wikivoyage.org",
            "127.0.0.1       el.wikivoyage.org",
            "127.0.0.1       eo.wikivoyage.org",
            "127.0.0.1       fr.wikivoyage.org",
            "127.0.0.1       nl.wikivoyage.org",
            "127.0.0.1       pt.wikivoyage.org",
            "127.0.0.1       sv.wikivoyage.org",
            "127.0.0.1       vi.wikivoyage.org",
            "127.0.0.1       ru.wikivoyage.org",
            "127.0.0.1       m.wikivoyage.org",
            "127.0.0.1       de.m.wikivoyage.org",
            "127.0.0.1       en.m.wikivoyage.org",
            "127.0.0.1       it.m.wikivoyage.org",
            "127.0.0.1       pl.m.wikivoyage.org",
            "127.0.0.1       bn.m.wikivoyage.org",
            "127.0.0.1       es.m.wikivoyage.org",
            "127.0.0.1       fa.m.wikivoyage.org",
            "127.0.0.1       he.m.wikivoyage.org",
            "127.0.0.1       ja.m.wikivoyage.org",
            "127.0.0.1       fi.m.wikivoyage.org",
            "127.0.0.1       uk.m.wikivoyage.org",
            "127.0.0.1       zh.m.wikivoyage.org",
            "127.0.0.1       el.m.wikivoyage.org",
            "127.0.0.1       eo.m.wikivoyage.org",
            "127.0.0.1       fr.m.wikivoyage.org",
            "127.0.0.1       nl.m.wikivoyage.org",
            "127.0.0.1       pt.m.wikivoyage.org",
            "127.0.0.1       sv.m.wikivoyage.org",
            "127.0.0.1       vi.m.wikivoyage.org",
            "127.0.0.1       ru.m.wikivoyage.org",
            "127.0.0.1       wiktionary.org",
            "127.0.0.1       www.wiktionary.org",
            "127.0.0.1       de.wiktionary.org",
            "127.0.0.1       en.wiktionary.org",
            "127.0.0.1       ku.wiktionary.org",
            "127.0.0.1       zh.wiktionary.org",
            "127.0.0.1       el.wiktionary.org",
            "127.0.0.1       fr.wiktionary.org",
            "127.0.0.1       mg.wiktionary.org",
            "127.0.0.1       ru.wiktionary.org",
            "127.0.0.1       ca.wiktionary.org",
            "127.0.0.1       id.wiktionary.org",
            "127.0.0.1       no.wiktionary.org",
            "127.0.0.1       sh.wiktionary.org",
            "127.0.0.1       cs.wiktionary.org",
            "127.0.0.1       it.wiktionary.org",
            "127.0.0.1       or.wiktionary.org",
            "127.0.0.1       et.wiktionary.org",
            "127.0.0.1       kn.wiktionary.org",
            "127.0.0.1       uz.wiktionary.org",
            "127.0.0.1       fi.wiktionary.org",
            "127.0.0.1       es.wiktionary.org",
            "127.0.0.1       lt.wiktionary.org",
            "127.0.0.1       sv.wiktionary.org",
            "127.0.0.1       eo.wiktionary.org",
            "127.0.0.1       li.wiktionary.org",
            "127.0.0.1       pl.wiktionary.org",
            "127.0.0.1       ta.wiktionary.org",
            "127.0.0.1       fa.wiktionary.org",
            "127.0.0.1       hu.wiktionary.org",
            "127.0.0.1       pt.wiktionary.org",
            "127.0.0.1       te.wiktionary.org",
            "127.0.0.1       ko.wiktionary.org",
            "127.0.0.1       ml.wiktionary.org",
            "127.0.0.1       ro.wiktionary.org",
            "127.0.0.1       th.wiktionary.org",
            "127.0.0.1       hy.wiktionary.org",
            "127.0.0.1       my.wiktionary.org",
            "127.0.0.1       skr.wiktionary.org",
            "127.0.0.1       tr.wiktionary.org",
            "127.0.0.1       hi.wiktionary.org",
            "127.0.0.1       nl.wiktionary.org",
            "127.0.0.1       sr.wiktionary.org",
            "127.0.0.1       vi.wiktionary.org",
            "127.0.0.1       io.wiktionary.org",
            "127.0.0.1       ja.wiktionary.org",
            "127.0.0.1       m.wiktionary.org",
            "127.0.0.1       de.m.wiktionary.org",
            "127.0.0.1       en.m.wiktionary.org",
            "127.0.0.1       ku.m.wiktionary.org",
            "127.0.0.1       zh.m.wiktionary.org",
            "127.0.0.1       el.m.wiktionary.org",
            "127.0.0.1       fr.m.wiktionary.org",
            "127.0.0.1       mg.m.wiktionary.org",
            "127.0.0.1       ru.m.wiktionary.org",
            "127.0.0.1       ca.m.wiktionary.org",
            "127.0.0.1       id.m.wiktionary.org",
            "127.0.0.1       no.m.wiktionary.org",
            "127.0.0.1       sh.m.wiktionary.org",
            "127.0.0.1       cs.m.wiktionary.org",
            "127.0.0.1       it.m.wiktionary.org",
            "127.0.0.1       or.m.wiktionary.org",
            "127.0.0.1       et.m.wiktionary.org",
            "127.0.0.1       kn.m.wiktionary.org",
            "127.0.0.1       uz.m.wiktionary.org",
            "127.0.0.1       fi.m.wiktionary.org",
            "127.0.0.1       es.m.wiktionary.org",
            "127.0.0.1       lt.m.wiktionary.org",
            "127.0.0.1       sv.m.wiktionary.org",
            "127.0.0.1       eo.m.wiktionary.org",
            "127.0.0.1       li.m.wiktionary.org",
            "127.0.0.1       pl.m.wiktionary.org",
            "127.0.0.1       ta.m.wiktionary.org",
            "127.0.0.1       fa.m.wiktionary.org",
            "127.0.0.1       hu.m.wiktionary.org",
            "127.0.0.1       pt.m.wiktionary.org",
            "127.0.0.1       te.m.wiktionary.org",
            "127.0.0.1       ko.m.wiktionary.org",
            "127.0.0.1       ml.m.wiktionary.org",
            "127.0.0.1       ro.m.wiktionary.org",
            "127.0.0.1       th.m.wiktionary.org",
            "127.0.0.1       hy.m.wiktionary.org",
            "127.0.0.1       my.m.wiktionary.org",
            "127.0.0.1       skr.m.wiktionary.org",
            "127.0.0.1       tr.m.wiktionary.org",
            "127.0.0.1       hi.m.wiktionary.org",
            "127.0.0.1       nl.m.wiktionary.org",
            "127.0.0.1       sr.m.wiktionary.org",
            "127.0.0.1       vi.m.wiktionary.org",
            "127.0.0.1       io.m.wiktionary.org",
            "127.0.0.1       ja.m.wiktionary.org",
            "127.0.0.1       wikibooks.org",
            "127.0.0.1       www.wikibooks.org",
            "127.0.0.1       de.wikibooks.org",
            "127.0.0.1       fr.wikibooks.org",
            "127.0.0.1       hu.wikibooks.org",
            "127.0.0.1       pt.wikibooks.org",
            "127.0.0.1       en.wikibooks.org",
            "127.0.0.1       it.wikibooks.org",
            "127.0.0.1       ja.wikibooks.org",
            "127.0.0.1       vi.wikibooks.org",
            "127.0.0.1       az.wikibooks.org",
            "127.0.0.1       eu.wikibooks.org",
            "127.0.0.1       lt.wikibooks.org",
            "127.0.0.1       th.wikibooks.org",
            "127.0.0.1       bn.wikibooks.org",
            "127.0.0.1       fa.wikibooks.org",
            "127.0.0.1       nl.wikibooks.org",
            "127.0.0.1       uk.wikibooks.org",
            "127.0.0.1       ba.wikibooks.org",
            "127.0.0.1       gl.wikibooks.org",
            "127.0.0.1       pl.wikibooks.org",
            "127.0.0.1       zh.wikibooks.org",
            "127.0.0.1       ca.wikibooks.org",
            "127.0.0.1       ko.wikibooks.org",
            "127.0.0.1       sq.wikibooks.org",
            "127.0.0.1       ru.wikibooks.org",
            "127.0.0.1       cs.wikibooks.org",
            "127.0.0.1       hi.wikibooks.org",
            "127.0.0.1       sr.wikibooks.org",
            "127.0.0.1       da.wikibooks.org",
            "127.0.0.1       id.wikibooks.org",
            "127.0.0.1       fi.wikibooks.org",
            "127.0.0.1       es.wikibooks.org",
            "127.0.0.1       he.wikibooks.org",
            "127.0.0.1       sv.wikibooks.org",
            "127.0.0.1       m.wikibooks.org",
            "127.0.0.1       de.m.wikibooks.org",
            "127.0.0.1       fr.m.wikibooks.org",
            "127.0.0.1       hu.m.wikibooks.org",
            "127.0.0.1       pt.m.wikibooks.org",
            "127.0.0.1       en.m.wikibooks.org",
            "127.0.0.1       it.m.wikibooks.org",
            "127.0.0.1       ja.m.wikibooks.org",
            "127.0.0.1       vi.m.wikibooks.org",
            "127.0.0.1       az.m.wikibooks.org",
            "127.0.0.1       eu.m.wikibooks.org",
            "127.0.0.1       lt.m.wikibooks.org",
            "127.0.0.1       th.m.wikibooks.org",
            "127.0.0.1       bn.m.wikibooks.org",
            "127.0.0.1       fa.m.wikibooks.org",
            "127.0.0.1       nl.m.wikibooks.org",
            "127.0.0.1       uk.m.wikibooks.org",
            "127.0.0.1       ba.m.wikibooks.org",
            "127.0.0.1       gl.m.wikibooks.org",
            "127.0.0.1       pl.m.wikibooks.org",
            "127.0.0.1       zh.m.wikibooks.org",
            "127.0.0.1       ca.m.wikibooks.org",
            "127.0.0.1       ko.m.wikibooks.org",
            "127.0.0.1       sq.m.wikibooks.org",
            "127.0.0.1       ru.m.wikibooks.org",
            "127.0.0.1       cs.m.wikibooks.org",
            "127.0.0.1       hi.m.wikibooks.org",
            "127.0.0.1       sr.m.wikibooks.org",
            "127.0.0.1       da.m.wikibooks.org",
            "127.0.0.1       id.m.wikibooks.org",
            "127.0.0.1       fi.m.wikibooks.org",
            "127.0.0.1       es.m.wikibooks.org",
            "127.0.0.1       he.m.wikibooks.org",
            "127.0.0.1       sv.m.wikibooks.org",
            "127.0.0.1       wikinews.org",
            "127.0.0.1       www.wikinews.org",
            "127.0.0.1       de.wikinews.org",
            "127.0.0.1       fr.wikinews.org",
            "127.0.0.1       pt.wikinews.org",
            "127.0.0.1       en.wikinews.org",
            "127.0.0.1       it.wikinews.org",
            "127.0.0.1       sr.wikinews.org",
            "127.0.0.1       es.wikinews.org",
            "127.0.0.1       pl.wikinews.org",
            "127.0.0.1       zh.wikinews.org",
            "127.0.0.1       ar.wikinews.org",
            "127.0.0.1       eo.wikinews.org",
            "127.0.0.1       ja.wikinews.org",
            "127.0.0.1       ta.wikinews.org",
            "127.0.0.1       ca.wikinews.org",
            "127.0.0.1       fa.wikinews.org",
            "127.0.0.1       ro.wikinews.org",
            "127.0.0.1       tr.wikinews.org",
            "127.0.0.1       cs.wikinews.org",
            "127.0.0.1       li.wikinews.org",
            "127.0.0.1       fi.wikinews.org",
            "127.0.0.1       uk.wikinews.org",
            "127.0.0.1       el.wikinews.org",
            "127.0.0.1       nl.wikinews.org",
            "127.0.0.1       sv.wikinews.org",
            "127.0.0.1       m.wikinews.org",
            "127.0.0.1       de.m.wikinews.org",
            "127.0.0.1       fr.m.wikinews.org",
            "127.0.0.1       pt.m.wikinews.org",
            "127.0.0.1       en.m.wikinews.org",
            "127.0.0.1       it.m.wikinews.org",
            "127.0.0.1       sr.m.wikinews.org",
            "127.0.0.1       es.m.wikinews.org",
            "127.0.0.1       pl.m.wikinews.org",
            "127.0.0.1       zh.m.wikinews.org",
            "127.0.0.1       ar.m.wikinews.org",
            "127.0.0.1       eo.m.wikinews.org",
            "127.0.0.1       ja.m.wikinews.org",
            "127.0.0.1       ta.m.wikinews.org",
            "127.0.0.1       ca.m.wikinews.org",
            "127.0.0.1       fa.m.wikinews.org",
            "127.0.0.1       ro.m.wikinews.org",
            "127.0.0.1       tr.m.wikinews.org",
            "127.0.0.1       cs.m.wikinews.org",
            "127.0.0.1       li.m.wikinews.org",
            "127.0.0.1       fi.m.wikinews.org",
            "127.0.0.1       uk.m.wikinews.org",
            "127.0.0.1       el.m.wikinews.org",
            "127.0.0.1       nl.m.wikinews.org",
            "127.0.0.1       sv.m.wikinews.org",
            "127.0.0.1       wikidata.org",
            "127.0.0.1       www.wikidata.org",
            "127.0.0.1       m.wikidata.org",
            "127.0.0.1       wikiversity.org",
            "127.0.0.1       www.wikiversity.org",
            "127.0.0.1       de.wikiversity.org",
            "127.0.0.1       en.wikiversity.org",
            "127.0.0.1       fr.wikiversity.org",
            "127.0.0.1       cs.wikiversity.org",
            "127.0.0.1       it.wikiversity.org",
            "127.0.0.1       zh.wikiversity.org",
            "127.0.0.1       es.wikiversity.org",
            "127.0.0.1       pt.wikiversity.org",
            "127.0.0.1       ru.wikiversity.org",
            "127.0.0.1       ar.wikiversity.org",
            "127.0.0.1       hi.wikiversity.org",
            "127.0.0.1       fi.wikiversity.org",
            "127.0.0.1       el.wikiversity.org",
            "127.0.0.1       ja.wikiversity.org",
            "127.0.0.1       sv.wikiversity.org",
            "127.0.0.1       ko.wikiversity.org",
            "127.0.0.1       sl.wikiversity.org",
            "127.0.0.1       m.wikiversity.org",
            "127.0.0.1       de.m.wikiversity.org",
            "127.0.0.1       en.m.wikiversity.org",
            "127.0.0.1       fr.m.wikiversity.org",
            "127.0.0.1       cs.m.wikiversity.org",
            "127.0.0.1       it.m.wikiversity.org",
            "127.0.0.1       zh.m.wikiversity.org",
            "127.0.0.1       es.m.wikiversity.org",
            "127.0.0.1       pt.m.wikiversity.org",
            "127.0.0.1       ru.m.wikiversity.org",
            "127.0.0.1       ar.m.wikiversity.org",
            "127.0.0.1       hi.m.wikiversity.org",
            "127.0.0.1       fi.m.wikiversity.org",
            "127.0.0.1       el.m.wikiversity.org",
            "127.0.0.1       ja.m.wikiversity.org",
            "127.0.0.1       sv.m.wikiversity.org",
            "127.0.0.1       ko.m.wikiversity.org",
            "127.0.0.1       sl.m.wikiversity.org",
            "127.0.0.1       wikiquote.org",
            "127.0.0.1       www.wikiquote.org",
            "127.0.0.1       cs.wikiquote.org",
            "127.0.0.1       it.wikiquote.org",
            "127.0.0.1       uk.wikiquote.org",
            "127.0.0.1       et.wikiquote.org",
            "127.0.0.1       pl.wikiquote.org",
            "127.0.0.1       ru.wikiquote.org",
            "127.0.0.1       en.wikiquote.org",
            "127.0.0.1       pt.wikiquote.org",
            "127.0.0.1       ar.wikiquote.org",
            "127.0.0.1       ko.wikiquote.org",
            "127.0.0.1       as.wikiquote.org",
            "127.0.0.1       az.wikiquote.org",
            "127.0.0.1       hy.wikiquote.org",
            "127.0.0.1       sah.wikiquote.org",
            "127.0.0.1       bg.wikiquote.org",
            "127.0.0.1       hr.wikiquote.org",
            "127.0.0.1       sk.wikiquote.org",
            "127.0.0.1       bn.wikiquote.org",
            "127.0.0.1       ig.wikiquote.org",
            "127.0.0.1       sl.wikiquote.org",
            "127.0.0.1       bs.wikiquote.org",
            "127.0.0.1       id.wikiquote.org",
            "127.0.0.1       sr.wikiquote.org",
            "127.0.0.1       ca.wikiquote.org",
            "127.0.0.1       he.wikiquote.org",
            "127.0.0.1       su.wikiquote.org",
            "127.0.0.1       de.wikiquote.org",
            "127.0.0.1       la.wikiquote.org",
            "127.0.0.1       fi.wikiquote.org",
            "127.0.0.1       el.wikiquote.org",
            "127.0.0.1       lt.wikiquote.org",
            "127.0.0.1       sv.wikiquote.org",
            "127.0.0.1       es.wikiquote.org",
            "127.0.0.1       li.wikiquote.org",
            "127.0.0.1       tr.wikiquote.org",
            "127.0.0.1       eo.wikiquote.org",
            "127.0.0.1       hu.wikiquote.org",
            "127.0.0.1       zh.wikiquote.org",
            "127.0.0.1       fa.wikiquote.org",
            "127.0.0.1       nl.wikiquote.org",
            "127.0.0.1       fr.wikiquote.org",
            "127.0.0.1       ja.wikiquote.org",
            "127.0.0.1       m.wikiquote.org",
            "127.0.0.1       cs.m.wikiquote.org",
            "127.0.0.1       it.m.wikiquote.org",
            "127.0.0.1       uk.m.wikiquote.org",
            "127.0.0.1       et.m.wikiquote.org",
            "127.0.0.1       pl.m.wikiquote.org",
            "127.0.0.1       ru.m.wikiquote.org",
            "127.0.0.1       en.m.wikiquote.org",
            "127.0.0.1       pt.m.wikiquote.org",
            "127.0.0.1       ar.m.wikiquote.org",
            "127.0.0.1       ko.m.wikiquote.org",
            "127.0.0.1       as.m.wikiquote.org",
            "127.0.0.1       az.m.wikiquote.org",
            "127.0.0.1       hy.m.wikiquote.org",
            "127.0.0.1       sah.m.wikiquote.org",
            "127.0.0.1       bg.m.wikiquote.org",
            "127.0.0.1       hr.m.wikiquote.org",
            "127.0.0.1       sk.m.wikiquote.org",
            "127.0.0.1       bn.m.wikiquote.org",
            "127.0.0.1       ig.m.wikiquote.org",
            "127.0.0.1       sl.m.wikiquote.org",
            "127.0.0.1       bs.m.wikiquote.org",
            "127.0.0.1       id.m.wikiquote.org",
            "127.0.0.1       sr.m.wikiquote.org",
            "127.0.0.1       ca.m.wikiquote.org",
            "127.0.0.1       he.m.wikiquote.org",
            "127.0.0.1       su.m.wikiquote.org",
            "127.0.0.1       de.m.wikiquote.org",
            "127.0.0.1       la.m.wikiquote.org",
            "127.0.0.1       fi.m.wikiquote.org",
            "127.0.0.1       el.m.wikiquote.org",
            "127.0.0.1       lt.m.wikiquote.org",
            "127.0.0.1       sv.m.wikiquote.org",
            "127.0.0.1       es.m.wikiquote.org",
            "127.0.0.1       li.m.wikiquote.org",
            "127.0.0.1       tr.m.wikiquote.org",
            "127.0.0.1       eo.m.wikiquote.org",
            "127.0.0.1       hu.m.wikiquote.org",
            "127.0.0.1       zh.m.wikiquote.org",
            "127.0.0.1       fa.m.wikiquote.org",
            "127.0.0.1       nl.m.wikiquote.org",
            "127.0.0.1       fr.m.wikiquote.org",
            "127.0.0.1       ja.m.wikiquote.org",
            "127.0.0.1       mediawiki.org",
            "127.0.0.1       www.mediawiki.org",
            "127.0.0.1       m.mediawiki.org",
            "127.0.0.1       wikisource.org",
            "127.0.0.1       www.wikisource.org",
            "127.0.0.1       de.wikisource.org",
            "127.0.0.1       en.wikisource.org",
            "127.0.0.1       es.wikisource.org",
            "127.0.0.1       fr.wikisource.org",
            "127.0.0.1       he.wikisource.org",
            "127.0.0.1       it.wikisource.org",
            "127.0.0.1       pl.wikisource.org",
            "127.0.0.1       ru.wikisource.org",
            "127.0.0.1       ta.wikisource.org",
            "127.0.0.1       uk.wikisource.org",
            "127.0.0.1       zh.wikisource.org",
            "127.0.0.1       ar.wikisource.org",
            "127.0.0.1       bn.wikisource.org",
            "127.0.0.1       be.wikisource.org",
            "127.0.0.1       cs.wikisource.org",
            "127.0.0.1       el.wikisource.org",
            "127.0.0.1       fa.wikisource.org",
            "127.0.0.1       hy.wikisource.org",
            "127.0.0.1       gu.wikisource.org",
            "127.0.0.1       ko.wikisource.org",
            "127.0.0.1       hu.wikisource.org",
            "127.0.0.1       ja.wikisource.org",
            "127.0.0.1       ml.wikisource.org",
            "127.0.0.1       kn.wikisource.org",
            "127.0.0.1       la.wikisource.org",
            "127.0.0.1       nap.wikisource.org",
            "127.0.0.1       nl.wikisource.org",
            "127.0.0.1       pt.wikisource.org",
            "127.0.0.1       ro.wikisource.org",
            "127.0.0.1       sa.wikisource.org",
            "127.0.0.1       sl.wikisource.org",
            "127.0.0.1       sr.wikisource.org",
            "127.0.0.1       fi.wikisource.org",
            "127.0.0.1       sv.wikisource.org",
            "127.0.0.1       te.wikisource.org",
            "127.0.0.1       tr.wikisource.org",
            "127.0.0.1       vi.wikisource.org",
            "127.0.0.1       m.wikisource.org",
            "127.0.0.1       de.m.wikisource.org",
            "127.0.0.1       en.m.wikisource.org",
            "127.0.0.1       es.m.wikisource.org",
            "127.0.0.1       fr.m.wikisource.org",
            "127.0.0.1       he.m.wikisource.org",
            "127.0.0.1       it.m.wikisource.org",
            "127.0.0.1       pl.m.wikisource.org",
            "127.0.0.1       ru.m.wikisource.org",
            "127.0.0.1       ta.m.wikisource.org",
            "127.0.0.1       uk.m.wikisource.org",
            "127.0.0.1       zh.m.wikisource.org",
            "127.0.0.1       ar.m.wikisource.org",
            "127.0.0.1       bn.m.wikisource.org",
            "127.0.0.1       be.m.wikisource.org",
            "127.0.0.1       cs.m.wikisource.org",
            "127.0.0.1       el.m.wikisource.org",
            "127.0.0.1       fa.m.wikisource.org",
            "127.0.0.1       hy.m.wikisource.org",
            "127.0.0.1       gu.m.wikisource.org",
            "127.0.0.1       ko.m.wikisource.org",
            "127.0.0.1       hu.m.wikisource.org",
            "127.0.0.1       ja.m.wikisource.org",
            "127.0.0.1       ml.m.wikisource.org",
            "127.0.0.1       kn.m.wikisource.org",
            "127.0.0.1       la.m.wikisource.org",
            "127.0.0.1       nap.m.wikisource.org",
            "127.0.0.1       nl.m.wikisource.org",
            "127.0.0.1       pt.m.wikisource.org",
            "127.0.0.1       ro.m.wikisource.org",
            "127.0.0.1       sa.m.wikisource.org",
            "127.0.0.1       sl.m.wikisource.org",
            "127.0.0.1       sr.m.wikisource.org",
            "127.0.0.1       fi.m.wikisource.org",
            "127.0.0.1       sv.m.wikisource.org",
            "127.0.0.1       te.m.wikisource.org",
            "127.0.0.1       tr.m.wikisource.org",
            "127.0.0.1       vi.m.wikisource.org",
            "127.0.0.1       wikifunctions.org",
            "127.0.0.1       www.wikifunctions.org",
            "127.0.0.1       m.wikifunctions.org",
            "#\tWikimedia Foundation End"
        };

        public static string[] WallhavenSection =
        {
            "#\tWallhaven Start",
            "127.0.0.1       wallhaven.cc",
            "127.0.0.1       www.wallhaven.cc",
            "127.0.0.1       w.wallhaven.cc",
            "127.0.0.1       th.wallhaven.cc",
            "127.0.0.1       static.wallhaven.cc",
            "127.0.0.1       alpha.wallhaven.cc",
            "#\tWallhaven End"
        };

        // 该方法用于确保指定路径的目录存在，如果目录不存在，则创建它
        public static void EnsureDirectoryExists(string path)
        {
            WriteLog($"EnsureDirectoryExists(string path)被调用，参数path：{path}。", LogLevel.Debug);

            // 如果目录不存在
            if (!Directory.Exists(path))
            {
                WriteLog($"目录{path}不存在，创建目录。", LogLevel.Info);

                // 创建目录
                Directory.CreateDirectory(path);
            }
            // 如果目录已存在，则不执行任何操作

            WriteLog($"EnsureDirectoryExists(string path)完成。", LogLevel.Debug);
        }

        // 用于杀死所有名为 "SNIBypass" 的进程的异步方法
        public static async Task KillSNIBypass()
        {
            WriteLog($"KillSNIBypass()被调用", LogLevel.Debug);

            // 获取所有名为 "SNIBypass" 的进程
            Process[] processes = Process.GetProcessesByName("SNIBypass");
            // 如果没有找到名为 "SNIBypass" 的进程，则直接返回
            if (processes.Length == 0)
            {
                WriteLog($"未找到名为\"SNIBypass\"的进程，返回。", LogLevel.Info);

                return;
            }
            // 创建一个任务列表，用于存储每个杀死进程任务的任务对象
            List<Task> tasks = new List<Task>();
            // 遍历所有找到的 "SNIBypass" 进程
            foreach (Process process in processes)
            {
                // 为每个进程创建一个异步任务，该任务尝试杀死进程并处理可能的异常
                Task task = Task.Run(() =>
                {
                    try
                    {
                        // 尝试杀死当前遍历到的进程
                        process.Kill();
                        // 等待进程退出，最多等待5000毫秒（5秒）
                        bool exited = process.WaitForExit(5000);
                        // 如果进程在超时时间内没有退出，则显示警告消息框
                        if (!exited)
                        {
                            WriteLog($"进程{process.ProcessName}在超时时间内没有退出。", LogLevel.Warning);

                            HandyControl.Controls.MessageBox.Show($"进程 {process.ProcessName} 在超时时间内没有退出。", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"杀死进程{process.ProcessName}时遇到错误：{ex}", LogLevel.Error);

                        // 如果在杀死进程的过程中发生异常，则显示错误消息框
                        HandyControl.Controls.MessageBox.Show($"无法杀死进程 {process.ProcessName}: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
                // 将创建的任务添加到任务列表中
                tasks.Add(task);
            }
            // 等待所有杀死进程的任务完成
            await Task.WhenAll(tasks);

            WriteLog($"KillSNIBypass()完成。", LogLevel.Debug);
        }

        // 用于计算给定文件路径列表中的文件总大小（以MB为单位）的静态方法
        public static double GetTotalFileSizeInMB(List<string> filePaths)
        {
            WriteLog($"GetTotalFileSizeInMB(List<string> filePaths)被调用，参数filePaths：{filePaths}。", LogLevel.Debug);

            // 定义一个变量来存储文件总大小（以字节为单位）
            long totalSizeInBytes = 0;
            // 遍历给定的文件路径列表
            foreach (string filePath in filePaths)
            {
                if (File.Exists(filePath))
                {
                    // 使用文件路径创建一个FileInfo对象，该对象提供有关文件的详细信息
                    FileInfo fileInfo = new FileInfo(filePath);
                    // 将当前文件的长度（以字节为单位）添加到总大小中
                    totalSizeInBytes += fileInfo.Length;
                }
            }
            // 将总大小（以字节为单位）转换为MB，并保留两位小数
            double totalSizeInMB = Math.Round((double)totalSizeInBytes / (1024 * 1024), 2);

            WriteLog($"GetTotalFileSizeInMB(List<string> filePaths)完成，返回{totalSizeInMB}。", LogLevel.Debug);

            // 返回文件总大小（以MB为单位）
            return totalSizeInMB;
        }

        // 运行 CMD 命令的方法
        public static void RunCMD(string command,string workingdirectory = "")
        {
            WriteLog($"RunCMD(string command,string workingdirectory = \"\")被调用，参数command：{command}，参数workingdirectory：{workingdirectory}。", LogLevel.Debug);

            // 创建一个ProcessStartInfo对象，用于配置如何启动一个进程
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                // 指定要启动的进程的文件名
                FileName = "cmd.exe",
                // 指定传递给cmd.exe的参数，/k表示执行完命令后保持窗口打开，\"{command}\"是要执行的命令
                Arguments = $"/k \"{command}\"",
                // 设置进程的工作目录
                WorkingDirectory = workingdirectory,
                // 设置为true，表示使用操作系统shell来启动进程（默认行为）
                UseShellExecute = true,
                // 设置为false，表示不将进程的标准输出重定向到调用进程的输出流中
                RedirectStandardOutput = false,
                // 设置为false，表示不将进程的标准错误输出重定向到调用进程的错误输出流中
                RedirectStandardError = false,
                // 设置为false，表示启动进程时创建一个新窗口
                CreateNoWindow = false
            };
            try
            {
                // 尝试启动进程
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                WriteLog($"遇到异常: {ex}。", LogLevel.Error);

                // 如果启动进程时发生异常，显示错误消息
                HandyControl.Controls.MessageBox.Show($"遇到异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            WriteLog($"RunCMD(string command,string workingdirectory = \"\")完成。", LogLevel.Debug);
        }

        // Github 文件下载加速代理列表
        public static readonly List<string> proxies = new List<string>{
            "gh.tryxd.cn",
            "cccccccccccccccccccccccccccccccccccccccccccccccccccc.cc",
            "gh.222322.xyz",
            "ghproxy.cc",
            "gh.catmak.name",
            "gh.nxnow.top",
            "ghproxy.cn",
            "ql.133.info",
            "cf.ghproxy.cc",
            "ghproxy.imciel.com",
            "g.blfrp.cn",
            "gh-proxy.ygxz.in",
            "ghp.keleyaa.com",
            "gh.pylas.xyz",
            "githubapi.jjchizha.com",
            "ghp.arslantu.xyz",
            "githubapi.jjchizha.com",
            "ghp.arslantu.xyz",
            "git.40609891.xyz",
            "firewall.lxstd.org",
            "gh.monlor.com",
            "slink.ltd",
            "github.geekery.cn",
            "gh.jasonzeng.dev",
            "github.tmby.shop",
            "gh.sixyin.com",
            "liqiu.love",
            "git.886.be",
            "github.xxlab.tech",
            "github.ednovas.xyz",
            "gh.xx9527.cn",
            "gh-proxy.linioi.com",
            "gitproxy.mrhjx.cn",
            "github.wuzhij.com",
            "git.speed-ssr.tech"
            };

        // （暂时保留）寻找最优代理的方法
        public static async Task<string> FindFastestProxy(List<string> proxies, string targetUrl)
        {
            WriteLog($"FindFastestProxy(List<string> proxies, string targetUrl)被调用，参数proxies：{proxies}，参数targetUrl：{targetUrl}。", LogLevel.Debug);

            long MirrorRpms = -1;
            // 逐个测试代理延迟
            var proxyTasks = proxies.Select(async proxy =>
            {
                var proxyUri = new Uri($"https://{proxy}");
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    try
                    {
                        var response = await client.GetAsync(proxyUri + targetUrl, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        WriteLog(proxy + " —— " + response.Content.Headers, LogLevel.Debug);
                        Console.WriteLine(proxy + " —— " + response.Content.Headers);
                        return (proxy, stopwatch.ElapsedMilliseconds, null);
                    }
                    catch (Exception ex)
                    {
                        WriteLog(proxyUri + targetUrl + " —— " + ex, LogLevel.Debug);
                        Console.WriteLine(proxyUri + targetUrl + " —— " + ex);
                        return (proxy, stopwatch.ElapsedMilliseconds, ex);
                    }
                    finally
                    {
                        stopwatch.Stop();
                    }
                }
            }).ToList();
            // 测试镜像站延迟
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                try
                {
                    var response = await client.GetAsync("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip", HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    WriteLog("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip ——" + response.Content.Headers, LogLevel.Debug);
                    Console.WriteLine("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip ——" + response.Content.Headers);
                    MirrorRpms = stopwatch.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    WriteLog("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip ——" + ex, LogLevel.Debug);
                    Console.WriteLine("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip ——" + ex);
                }
                finally
                {
                    stopwatch.Stop();
                }
            }
            // 等待测试全部完成
            var proxyResults = await Task.WhenAll(proxyTasks);
            // 输出到控制台，调试用
            foreach (var (proxy, ElapsedMilliseconds, ex) in proxyResults)
            {
                if (ex is TaskCanceledException)
                {
                    WriteLog(proxy + " —— 超时", LogLevel.Debug);
                    Console.WriteLine(proxy + " —— 超时");
                }
                else if (ex != null)
                {
                    WriteLog(proxy + " —— 错误", LogLevel.Debug);
                    Console.WriteLine(proxy + " —— 错误");
                }
                else
                {
                    WriteLog(proxy + " —— " + ElapsedMilliseconds + "ms", LogLevel.Debug);
                    Console.WriteLine(proxy + " —— " + ElapsedMilliseconds + "ms");
                }
            }
            WriteLog("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip —— " + MirrorRpms + "ms", LogLevel.Debug);
            Console.WriteLine("https://git.moezx.cc/mirrors/Pixiv-Nginx/archive/main.zip —— " + MirrorRpms + "ms");

            // 排除有错误的结果并排序
            var fastestProxy = proxyResults
                .Where(result => result.ex == null)
                .OrderBy(result => result.ElapsedMilliseconds)
                .First();

            string Output = (MirrorRpms != -1 && fastestProxy.ElapsedMilliseconds > MirrorRpms) ? "Mirror" : fastestProxy.proxy;

            WriteLog($"FindFastestProxy(List<string> proxies, string targetUrl)完成，返回{Output}。", LogLevel.Debug);

            // 如果镜像站延迟有效且比代理低，则返回 Mirror ，否则返回延迟最低的代理地址
            return Output;
        }

        // 定义一个静态的HttpClient实例，用于HTTP请求
        private static readonly HttpClient _httpClient = new HttpClient
        {
            // 设置超时时间为10秒
            Timeout = TimeSpan.FromSeconds(10)
        };

        // 异步获取URL内容的方法
        public static async Task<string> GetAsync(string url)
        {
            WriteLog($"GetAsync(string url)被调用，参数url：{url}。", LogLevel.Debug);

            try
            {
                // 设置用户代理
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

                // 发起HTTP GET请求
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                // 确保请求成功
                response.EnsureSuccessStatusCode();

                string Output = await response.Content.ReadAsStringAsync();

                WriteLog($"GetAsync(string url)完成，返回{Output}。", LogLevel.Debug);

                // 返回响应内容
                return Output;
            }
            catch(Exception ex)
            {
                WriteLog($"遇到异常：{ex}。", LogLevel.Error);

                // 抛出异常
                throw ex;
            }
        }

        // 操作配置文件的类
        public class FilesINI
        {
            // 声明INI文件的写操作函数 WritePrivateProfileString()
            [System.Runtime.InteropServices.DllImport("kernel32")]
            private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

            // 声明INI文件的读操作函数 GetPrivateProfileString()
            [System.Runtime.InteropServices.DllImport("kernel32")]
            private static extern int GetPrivateProfileString(string section, string key, string def, System.Text.StringBuilder retVal, int size, string filePath);


            // 写入INI的方法
            public void INIWrite(string section, string key, string value, string path)
            {
                WriteLog($"INIWrite(string section, string key, string value, string path)被调用，参数section：{section}，参数key：{key}，参数value：{value}，参数path：{path}。", LogLevel.Debug);

                // section=配置节点名称，key=键名，value=返回键值，path=路径
                WritePrivateProfileString(section, key, value, path);

                WriteLog($"INIWrite(string section, string key, string value, string path)完成。", LogLevel.Debug);
            }

            //读取INI的方法
            public string INIRead(string section, string key, string path)
            {
                WriteLog($"INIRead(string section, string key, string path)被调用，参数section：{section}，参数key：{key}，参数path：{path}。", LogLevel.Debug);

                // 每次从ini中读取多少字节
                System.Text.StringBuilder temp = new System.Text.StringBuilder(255);

                // section=配置节点名称，key = 键名，temp = 上面，path = 路径
                GetPrivateProfileString(section, key, "", temp, 255, path);

                WriteLog($"INIRead(string section, string key, string path)完成，返回{temp}。", LogLevel.Debug);

                return temp.ToString();
            }

            //删除一个INI文件
            public void INIDelete(string FilePath)
            {
                WriteLog($"INIDelete(string FilePath)被调用，参数FilePath：{FilePath}。", LogLevel.Debug);

                File.Delete(FilePath);

                WriteLog($"INIDelete(string FilePath)完成。", LogLevel.Debug);
            }

        }

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
                WriteLog($"StringToBool(string input)被调用，参数input：{input}。", LogLevel.Debug);

                if (string.IsNullOrEmpty(input))
                {
                    return false; // 空字符串或null返回false
                }
                string booleanString = input.Trim().ToLower(); // 去除空格并转换为小写

                WriteLog($"StringToBool(string input)完成，返回{booleanString == "true"}。", LogLevel.Debug);

                // 检查字符串是否为 "true"
                return booleanString == "true";
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

        /// <summary>
        /// 释放resx里面的普通类型文件
        /// </summary>
        /// <param name="resource">resx里面的资源</param>
        /// <param name="path">释放到的路径</param>
        public static void ExtractNormalFileInResx(byte[] resource, String path)
        {
            WriteLog($"ExtractNormalFileInResx(byte[] resource, String path)被调用，参数resource：{resource}，参数path：{path}。", LogLevel.Debug);

            FileStream file = new FileStream(path, FileMode.Create);
            file.Write(resource, 0, resource.Length);
            file.Flush();
            file.Close();

            WriteLog($"ExtractNormalFileInResx(byte[] resource, String path)完成。", LogLevel.Debug);
        }

        // 检测是否可以 Ping 通的方法
        public static bool PingHost(string host)
        {
            WriteLog($"PingHost(string host)被调用，参数host：{host}。", LogLevel.Debug);

            bool pingable = false;
            Ping pingSender = new Ping();
            try
            {
                PingReply reply = pingSender.Send(host);
                if (reply.Status == IPStatus.Success)
                {
                    pingable = true;
                }
            }
            catch (PingException pex)
            {
                WriteLog($"遇到异常：{pex}。", LogLevel.Error);
            }

            WriteLog($"PingHost(string host)完成，返回{pingable}。", LogLevel.Debug);

            return pingable;
        }

        // 确保 api.github.com 可以正常访问的方法
        // 解决由于 api.github.com 访问异常引起的有关问题（https://github.com/racpast/Pixiv-Nginx-GUI/issues/2）
        public static void EnsureGithubAPI()
        {
            WriteLog($"EnsureGithubAPI()被调用。", LogLevel.Debug);

            // api.github.com DNS A记录 IPv4 列表
            List<string> APIIPAddress = new List<string>
            {
                "20.205.243.168",
                "140.82.113.5",
                "140.82.116.6",
                "4.237.22.34"
            };
            foreach (string IPAddress in APIIPAddress)
            {
                bool isReachable = PingHost(IPAddress);

                WriteLog($"{IPAddress}测试完成，Ping结果：{isReachable}。", LogLevel.Info);

                if (isReachable)
                {
                    string[] NewAPIRecord =
                    {
                        "#\tapi.github.com Start",
                        $"{IPAddress}\tapi.github.com",
                        "#\tapi.github.com End"
                    };
                    WriteLinesToFile(NewAPIRecord,SystemHosts);
                    break;
                }
            }

            WriteLog($"EnsureGithubAPI()完成。", LogLevel.Debug);
        }

        // 用于刷新DNS缓存的方法
        public static void Flushdns()
        {
            WriteLog($"Flushdns()被调用。", LogLevel.Debug);

            // 构建要执行的命令字符串，该命令用于刷新DNS缓存然后退出
            string command = "ipconfig /flushdns & exit";
            RunCMD(command);

            WriteLog($"Flushdns()完成。", LogLevel.Debug);
        }

        // 用于移除文件中从“#   sectionName Start”到“#   sectionName End”部分的方法，用来操作 hosts
        public static void RemoveSection(string filePath, string sectionName)
        {
            WriteLog($"RemoveSection(string filePath, string sectionName)被调用，参数filePath：{filePath}，参数sectionName：{sectionName}。", LogLevel.Debug);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件{filePath}不存在！", filePath);
            }

            string startMarker = $"#\t{sectionName} Start";
            string endMarker = $"#\t{sectionName} End";
            StringBuilder newContent = new StringBuilder();
            bool isRemoving = false;
            foreach (string line in File.ReadAllLines(filePath))
            {
                if (line == startMarker)
                {
                    isRemoving = true;
                    continue;
                }
                else if (line == endMarker)
                {
                    isRemoving = false;
                    continue;
                }
                else if (!isRemoving)
                {
                    newContent.AppendLine(line);
                }
            }
            File.WriteAllText(filePath, newContent.ToString());

            WriteLog($"RemoveSection(string filePath, string sectionName)完成。", LogLevel.Debug);
        }

        // 用于把string[] linesToWrite写入一个文件的方法，用来操作 hosts
        public static void WriteLinesToFile(string[] linesToWrite, string filePath)
        {
            WriteLog($"WriteLinesToFile(string[] linesToWrite, string filePath)被调用，参数string[] linesToWrite：{linesToWrite}，参数filePath：{filePath}。", LogLevel.Debug);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件{filePath}不存在！", filePath);
            }

            using (StreamWriter writer = new StreamWriter(filePath, true, Encoding.UTF8))
            {
                foreach (string line in linesToWrite)
                {
                    writer.WriteLine(line);
                }
            }

            WriteLog($"WriteLinesToFile(string[] linesToWrite, string filePath)完成。", LogLevel.Debug);
        }

        // 用于安装证书
        public static void InstallCertificate()
        {
            WriteLog("InstallCertificate()被调用。", LogLevel.Debug);

            // 创建一个指向当前用户根证书存储的X509Store对象
            // StoreName.Root表示根证书存储，StoreLocation.CurrentUser表示当前用户的证书存储
            X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            // 以最大权限打开证书存储，以便进行添加、删除等操作
            store.Open(OpenFlags.MaxAllowed);
            // 获取证书存储中的所有证书
            X509Certificate2Collection collection = store.Certificates;
            // 在证书存储中查找具有指定指纹的证书
            // X509FindType.FindByThumbprint 表示按指纹查找，false 表示不区分大小写（对于指纹查找无效，因为指纹是唯一的）
            X509Certificate2Collection fcollection = collection.Find(X509FindType.FindByThumbprint, Thumbprint, false);
            try
            {
                // 检查是否找到了具有该指纹的证书
                if (fcollection != null)
                {
                    WriteLog($"检测到fcollection不为空。", LogLevel.Debug);

                    // 如果找到了证书，则检查证书的数量
                    if (fcollection.Count > 0)
                    {
                        WriteLog($"检测到证书数量为{fcollection.Count}，尝试移除。", LogLevel.Info);

                        // 从存储中移除找到的证书（如果存在多个相同指纹的证书，将移除所有）
                        store.RemoveRange(fcollection);
                    }
                    // 检查指定的证书文件是否存在
                    if (File.Exists(CERFile))
                    {
                        WriteLog($"检测到证书文件{CERFile}存在，尝试安装。", LogLevel.Info);

                        // 从文件中加载证书
                        X509Certificate2 x509 = new X509Certificate2(CERFile);
                        // 将证书添加到存储中
                        store.Add(x509);

                        ConfigINI.INIWrite("程序设置", "IsFirst", "false", INIPath);
                    }
                }
                // 如果没有找到证书集合（理论上不应该发生，除非Thumbprint为空或格式错误）
            }
            catch (Exception ex)
            {
                WriteLog($"遇到错误：{ex}", LogLevel.Error);

                // 如果在安装证书过程中发生异常，则显示错误消息框
                HandyControl.Controls.MessageBox.Show($"安装证书失败！\r\n{ex.Message}", "安装证书", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 无论是否发生异常，都关闭证书存储
                store.Close();

                WriteLog($"证书存储关闭。", LogLevel.Debug);
            }
            WriteLog("InstallCertificate()完成。", LogLevel.Debug);
        }

        // 文件帮助类
        public class FileHelper
        {
            // 目标目录路径
            private readonly string _targetDirectory;

            public FileHelper(string targetDirectory)
            {
                _targetDirectory = targetDirectory;
            }

            // 搜索以 "CustomBkg" 开头的文件，并返回第一个找到的文件的路径
            public string FindCustomBkg()
            {
                WriteLog($"FindCustomBkg()被调用。", LogLevel.Debug);

                string filePath = null;
                // 遍历目标目录中的所有文件
                foreach (var file in Directory.GetFiles(_targetDirectory))
                {
                    // 获取文件名（不包括路径）
                    var fileName = Path.GetFileName(file);
                    // 检查文件名是否以 "CustomBkg" 开头
                    if (fileName.StartsWith("CustomBkg", StringComparison.OrdinalIgnoreCase))
                    {
                        // 找到符合条件的文件，返回其路径
                        filePath = file;
                        break; // 只需要第一个找到的文件，退出循环
                    }
                }

                WriteLog($"FindCustomBkg()完成，返回{filePath}。", LogLevel.Debug);

                return filePath; // 如果没有找到文件，则返回 null
            }
        }

        // 释放资源型的图像调用方法
        public static BitmapImage GetImage(string imagePath)
        {
            WriteLog($"GetImage(string imagePath)被调用，参数imagePath：{imagePath}。", LogLevel.Debug);

            BitmapImage bitmap = new BitmapImage();
            if (File.Exists(imagePath))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                using (Stream ms = new MemoryStream(File.ReadAllBytes(imagePath)))
                {
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();  // 在这里释放资源  
                }
            }

            WriteLog($"GetImage(string imagePath)完成，返回{bitmap}。", LogLevel.Debug);

            return bitmap;
        }
    }
}