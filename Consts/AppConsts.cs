using System;

namespace SNIBypassGUI.Consts
{
    public static class AppConsts
    {
        // 默认一言
        public const string DefaultYiyan = "行远自迩，登高自卑。";
        public const string DefaultYiyanFrom = "—— 戴圣「礼记」";

        // 证书指纹
        public const string CertificateThumbprint = "263961dd1800f3513b1e7818881683889c92aa1a";

        // 版本号，更新时需要修改
        public const string CurrentVersion = "V3.7";

        // 日志头
        public static string[] LogHead =
        [
            "——————————————————————————————————————————",
            "  ___   _  _   ___   ___                                   ___   _   _   ___ ",
            " / __| | \\| | |_ _| | _ )  _  _   _ __   __ _   ___  ___  / __| | | | | |_ _|",
            " \\__ \\ | .` |  | |  | _ \\ | || | | '_ \\ / _` | (_-< (_-< | (_ | | |_| |  | | ",
            " |___/ |_|\\_| |___| |___/  \\_, | | .__/ \\__,_| /__/ /__/  \\___|  \\___/  |___|",
            "                           |__/  |_|                                         ",
            "——————————————————————————————————————————",
            "程序版本 | " + CurrentVersion,
            "记录时间 | " + DateTime.Now.ToString(),
            "——————————————————————————————————————————",
            "请不要随意截断日志内容，除非您十分清楚地知道哪些是重要内容。",
            "——————————————————————————————————————————"
        ];

        // Nginx 进程名
        public const string NginxProcessName = "SNIBypass";

        // 计划任务名
        public const string TaskName = "StartSNIBypassGUI";

        // DNS 服务名
        public const string DnsServiceName = "AcrylicDNSProxySvc";
    }
}
