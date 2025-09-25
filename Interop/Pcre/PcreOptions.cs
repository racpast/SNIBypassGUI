namespace SNIBypassGUI.Interop.Pcre
{
    /// <summary>
    /// 定义了 PCRE (Perl Compatible Regular Expressions) 库的常量。
    /// </summary>
    /// <remarks>
    /// 这些常量可用作 pcre_compile() 和 pcre_exec() 等函数的选项参数，
    /// 其值与 pcre.h 中的定义保持一致。
    /// </remarks>
    public static class PcreOptions
    {
        public const int PCRE_CASELESS = 0x00000001;
        public const int PCRE_MULTILINE = 0x00000002;
        public const int PCRE_DOTALL = 0x00000004;
        public const int PCRE_EXTENDED = 0x00000008;
        public const int PCRE_ANCHORED = 0x00000010;
        public const int PCRE_DOLLAR_ENDONLY = 0x00000020;
        public const int PCRE_EXTRA = 0x00000040;
        public const int PCRE_NOTBOL = 0x00000080;
        public const int PCRE_NOTEOL = 0x00000100;
        public const int PCRE_UNGREEDY = 0x00000200;
        public const int PCRE_NOTEMPTY = 0x00000400;
        public const int PCRE_UTF8 = 0x00000800;
        public const int PCRE_NO_AUTO_CAPTURE = 0x00001000;
        public const int PCRE_NO_UTF8_CHECK = 0x00002000;
        public const int PCRE_AUTO_CALLOUT = 0x00004000;
        public const int PCRE_PARTIAL = 0x00008000;
        public const int PCRE_DFA_SHORTEST = 0x00010000;
        public const int PCRE_DFA_RESTART = 0x00020000;
        public const int PCRE_FIRSTLINE = 0x00040000;
        public const int PCRE_DUPNAMES = 0x00080000;
        public const int PCRE_NEWLINE_CR = 0x00100000;
        public const int PCRE_NEWLINE_LF = 0x00200000;
        public const int PCRE_NEWLINE_CRLF = 0x00300000;
        public const int PCRE_NEWLINE_ANY = 0x00400000;
        public const int PCRE_NEWLINE_ANYCRLF = 0x00500000;
        public const int PCRE_BSR_ANYCRLF = 0x00800000;
        public const int PCRE_BSR_UNICODE = 0x01000000;
        public const int PCRE_JAVASCRIPT_COMPAT = 0x02000000;
        public const int PCRE_NO_START_OPTIMIZE = 0x04000000;
        public const int PCRE_NO_START_OPTIMISE = 0x04000000; // British spelling
    }
}