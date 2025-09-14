namespace SNIBypassGUI.Interop.Pcre
{
    /// <summary>
    /// 包含 PCRE 编译选项的静态常量。
    /// </summary>
    public static class PcreOptions
    {
        public const int PCRE_CASELESS = 0x00000001;
        public const int PCRE_MULTILINE = 0x00000002;
        public const int PCRE_DOTALL = 0x00000004;
        public const int PCRE_EXTENDED = 0x00000008;
        public const int PCRE_ANCHORED = 0x00000010;
        public const int PCRE_DOLLAR_ENDONLY = 0x00000020;
        public const int PCRE_UNGREEDY = 0x00000200;
        public const int PCRE_UTF8 = 0x00000800;
        public const int PCRE_NO_AUTO_CAPTURE = 0x00001000;
        public const int PCRE_NO_UTF8_CHECK = 0x00002000;
        public const int PCRE_DUPNAMES = 0x00080000;
        public const int PCRE_BSR_ANYCRLF = 0x00800000;
        public const int PCRE_BSR_UNICODE = 0x01000000;
    }
}
