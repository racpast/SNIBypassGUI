using System;

namespace SNIBypassGUI.Enums
{
    [Flags]
    public enum ServerInformalProperties : ulong
    {
        None = 0,
        Dnssec = 1UL << 0,
        NoLog = 1UL << 1,
        NoFilter = 1UL << 2
    }
}
