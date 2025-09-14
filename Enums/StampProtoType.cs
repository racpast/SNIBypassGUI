namespace SNIBypassGUI.Enums
{
    public enum StampProtoType : byte
    {
        Plain = 0x00,
        DnsCrypt = 0x01,
        DoH = 0x02,
        Tls = 0x03,
        DoQ = 0x04,
        ODoHTarget = 0x05,
        DnsCryptRelay = 0x81,
        ODoHRelay = 0x85
    }
}
