using System;
using System.Runtime.InteropServices;

namespace SNIBypassGUI.Interop.Pcre
{
    /// <summary>
    /// 封装了对原生 PCRE DLL 的 P/Invoke 调用。
    /// 内部类，外部应该使用 PcreRegex 类。
    /// </summary>
    internal static class PcreNative
    {
        private const string DllName = "PcreWrapper";
        private const CallingConvention Convent = CallingConvention.Cdecl;

#if NET5_0_OR_GREATER
        [DllImport(DllName, CallingConvention = Convent, CharSet = CharSet.Utf8, EntryPoint = "pcrew_compile")]
        internal static extern IntPtr PcreCompile(string pattern, int options, out IntPtr outCode, out int outErrOffset);
#else
#warning 在 .NET 5 及更高版本中应使用 CharSet.Utf8 作为字符集。
        [DllImport(DllName, CallingConvention = Convent, CharSet = CharSet.Ansi, EntryPoint = "pcrew_compile")]
        internal static extern IntPtr PcreCompile(byte[] pattern, int options, out IntPtr outCode, out int outErrOffset);
#endif

        [DllImport(DllName, CallingConvention = Convent, EntryPoint = "pcrew_free")]
        internal static extern void PcreFree(IntPtr code);

        [DllImport(DllName, CallingConvention = Convent, EntryPoint = "pcrew_match")]
        internal static extern int PcreMatch(IntPtr code, byte[] subject, int length, [Out] int[] ovector, int ovecsize);

        [DllImport(DllName, CallingConvention = Convent, EntryPoint = "pcrew_get_capture_count")]
        internal static extern int PcreGetCaptureCount(IntPtr code, out int count);
    }
}
