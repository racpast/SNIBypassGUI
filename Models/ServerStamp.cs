using System.Collections.Generic;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// Represents a parsed DNS Stamp, containing all its properties.
    /// </summary>
    public class ServerStamp
    {
        public StampProtoType Proto { get; set; }
        public ServerInformalProperties Props { get; set; }
        public string ServerAddrStr { get; set; }
        public byte[] ServerPk { get; set; }
        public List<byte[]> Hashes { get; set; } = [];
        public string ProviderName { get; set; }
        public string Path { get; set; }
    }
}
