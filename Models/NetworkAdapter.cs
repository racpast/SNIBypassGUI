using System;

namespace SNIBypassGUI.Models
{
    public class NetworkAdapter(string name, string friendlyName, string description, string serviceName, uint interfaceIndex, string macAddress, string manufacturer, bool isPhysicalAdapter, string guid, bool isNetEnabled, ushort netConnectionStatus, bool isIPEnabled, string[] ipAddress, string[] ipSubnet, string[] defaultIPGateway, bool isDhcpEnabled, string dhcpServer, DateTime dhcpLeaseObtained, DateTime dhcpLeaseExpires, string[] ipv4DnsServer, bool isIPv4DNSAuto, bool isIPv6Enabled, string[] ipv6Address, ushort[] ipv6PrefixLength, string[] ipv6DnsServer)
    {
        /// <summary>
        /// 适配器的内部名称，这个名字通常和设备管理器里的网卡名称相同。
        /// 来自 Win32_NetworkAdapter 类的 Name 属性。
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// 控制面板（网络连接窗口）里显示的名称，可以被用户手动修改。这个名字就是 “以太网”、“ Wi-Fi ”、“本地连接” 这些网卡的显示名称。
        /// 来自 Win32_NetworkAdapter 类的 NetConnectionID 属性。
        /// </summary>
        public string FriendlyName { get; } = friendlyName;

        /// <summary>
        /// 网卡的硬件描述，通常是网卡驱动提供的名称。
        /// 来自 Win32_NetworkAdapter 类的 Description 属性或 Win32_NetworkAdapterConfiguration 类的 Description 属性。
        /// </summary>
        public string Description { get; } = description;

        /// <summary>
        /// 网络适配器的驱动程序名称，也就是 Windows 设备管理器里用来加载网卡驱动的服务名称。
        /// 来自 Win32_NetworkAdapter 的 ServiceName 属性或 Win32_NetworkAdapterConfiguration 类的 ServiceName 属性。
        /// </summary>
        public string ServiceName { get; } = serviceName;

        /// <summary>
        /// 唯一标识网络接口的编号，主要用于路由表和Windows 网络 API。
        /// 来自 Win32_NetworkAdapter 的 InterfaceIndex 属性或 Win32_NetworkAdapterConfiguration 类的 InterfaceIndex 属性。
        /// </summary>
        public uint InterfaceIndex { get; } = interfaceIndex;

        /// <summary>
        /// 网络设备的唯一标识，即 MAC 地址。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 MACAddress 属性。
        /// </summary>
        public string MACAddress { get; } = macAddress;

        /// <summary>
        /// 网络适配器的制造商。
        /// 来自 Win32_NetworkAdapter 类的 Manufacturer 属性。
        /// </summary>
        public string Manufacturer { get; } = manufacturer;

        /// <summary>
        /// 指示适配器是物理适配器还是逻辑适配器。
        /// 来自 Win32_NetworkAdapter 类的 PhysicalAdapter 属性。
        /// </summary>
        public bool IsPhysicalAdapter { get; } = isPhysicalAdapter;

        /// <summary>
        /// Windows 为每个网络适配器分配的 唯一标识符。
        /// 来自 Win32_NetworkAdapter 类的 GUID 属性。
        /// </summary>
        public string GUID { get; } = guid;

        /// <summary>
        /// 指示适配器是否已启用。
        /// 来自 Win32_NetworkAdapter 类的 NetEnabled 属性。
        /// </summary>
        public bool IsNetEnabled { get; } = isNetEnabled;

        /// <summary>
        /// 网络适配器连接到网络的状态。
        /// 来自 Win32_NetworkAdapter 类的 NetConnectionStatus 属性。
        /// </summary>
        public ushort NetConnectionStatus { get; } = netConnectionStatus;

        /// <summary>
        /// 是否启用 TCP/IPv4。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 IPEnabled 属性。
        /// </summary>
        public bool IsIPEnabled { get; } = isIPEnabled;

        /// <summary>
        /// IPv4 地址。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 IPAddress 属性。
        /// </summary>
        public string[] IPAddress { get; } = ipAddress;

        /// <summary>
        /// IPv4 子网掩码。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 IPSubnet 属性。
        /// </summary>
        public string[] IPSubnet { get; } = ipSubnet;

        /// <summary>
        /// 默认网关。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 DefaultIPGateway 属性。
        /// </summary>
        public string[] DefaultIPGateway { get; } = defaultIPGateway;

        /// <summary>
        /// DHCP 是否启用。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 DHCPEnabled 属性。
        /// </summary>
        public bool IsDHCPEnabled { get; } = isDhcpEnabled;

        /// <summary>
        /// DHCP 服务器地址。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 DHCPServer 属性。
        /// </summary>
        public string DHCPServer { get; } = dhcpServer;

        /// <summary>
        /// DHCP 租约开始时间。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 DHCPLeaseObtained 属性。
        /// </summary>
        public DateTime DHCPLeaseObtained { get; } = dhcpLeaseObtained;

        /// <summary>
        /// DHCP 租约到期时间。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 DHCPLeaseExpires 属性。
        /// </summary>
        public DateTime DHCPLeaseExpires { get; } = dhcpLeaseExpires;

        /// <summary>
        /// IPv4 DNS 服务器地址。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 DNSServerSearchOrder 属性。
        /// </summary>
        public string[] IPv4DNSServer { get; } = ipv4DnsServer;

        /// <summary>
        /// 指示 IPv4 DNS 是否自动获取。
        /// 从注册表判断。
        /// </summary>
        public bool IsIPv4DNSAuto { get; } = isIPv4DNSAuto;

        /// <summary>
        /// 是否启用 TCP/IPv6。
        /// 根据 IP 地址是否含有 IPv6 地址来判断。
        /// </summary>
        public bool IsIPv6Enabled { get; } = isIPv6Enabled;

        /// <summary>
        /// IPv6 地址。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 IPAddress 属性。
        /// </summary>
        public string[] IPv6Address { get; } = ipv6Address;

        /// <summary>
        /// IPv6 子网前缀长度。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 IPSubnet 属性。
        /// </summary>
        public ushort[] IPv6PrefixLength { get; } = ipv6PrefixLength;

        /// <summary>
        /// IPv6  DNS 服务器地址。
        /// 来自 Win32_NetworkAdapterConfiguration 类的 DNSServerSearchOrder 属性。
        /// </summary>
        public string[] IPv6DNSServer { get; } = ipv6DnsServer;
    }
}
