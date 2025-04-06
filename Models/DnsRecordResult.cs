using System;

namespace SNIBypassGUI.Models
{
    public class DnsRecordResult
    {
        public DnsQueryType RecordType { get; set; }
        public string Name { get; set; }
        public TimeSpan? TTL { get; set; }
        public object Value { get; set; }

        public override string ToString() => $"[{RecordType}] {Name} (TTL: {TTL?.TotalSeconds}s) → {Value}";
    }

    public enum DnsQueryType
    {
        A,     // IPv4
        AAAA,  // IPv6
        CNAME, // 别名
        MX,    // 邮件交换
        TXT,   // 文本记录
        NS,    // 域名服务器
        SOA,   // 授权起始
        PTR,   // 反向解析
        SRV,   // 服务定位
        CAA,   // 证书颁发
        ANY    // 所有类型
    }
}
