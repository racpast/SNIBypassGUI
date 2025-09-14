using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SNIBypassGUI.Interop.Pcre
{
    /// <summary>
    /// 代表一个已编译的 PCRE 正则表达式。
    /// 此类实现了 IDisposable，应在 using 语句中使用或手动调用 Dispose()。
    /// </summary>
    public sealed class PcreRegex : IDisposable
    {
        internal static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
        private IntPtr _code;
        private bool _disposed;
        private readonly int _captureCount;

        public int CaptureCount => _captureCount + 1;

        public PcreRegex(string pattern, int options = 0)
        {
            IntPtr errMsgPtr = PcreNative.PcreCompile(pattern, options, out _code, out int errOffset);
            if (errMsgPtr != IntPtr.Zero || _code == IntPtr.Zero)
            {
                string errorMessage = Marshal.PtrToStringAnsi(errMsgPtr);
                if (_code != IntPtr.Zero) PcreNative.PcreFree(_code);
                throw new ArgumentException($"PCRE compilation failed: {errorMessage} (at offset {errOffset})", nameof(pattern));
            }

            int rc = PcreNative.PcreGetCaptureCount(_code, out _captureCount);
            if (rc != 0) throw new InvalidOperationException($"pcre_fullinfo failed with error code: {rc}");
        }

        public static bool TryValidatePattern(string pattern, int options, out string errorMessage, out int errorOffset)
        {
            IntPtr errMsgPtr = PcreNative.PcreCompile(pattern, options, out IntPtr tempCode, out errorOffset);
            if (errMsgPtr == IntPtr.Zero && tempCode != IntPtr.Zero)
            {
                PcreNative.PcreFree(tempCode);
                errorMessage = null;
                errorOffset = -1;
                return true;
            }
            errorMessage = Marshal.PtrToStringAnsi(errMsgPtr);
            if (tempCode != IntPtr.Zero) PcreNative.PcreFree(tempCode);
            return false;
        }

        public PcreMatchResult Match(string subject)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PcreRegex));

            // ( N + 1 ) * 3
            // 原文档说 Number of elements in the vector (a multiple of 3)
            int ovecsize = (_captureCount + 1) * 3;
            int[] ovector = new int[ovecsize];

            byte[] subjectBytes = Utf8NoBom.GetBytes(subject);
            int rc = PcreNative.PcreMatch(_code, subjectBytes, subjectBytes.Length, ovector, ovecsize);

            return new PcreMatchResult(rc, subjectBytes, ovector);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                PcreNative.PcreFree(_code);
                _code = IntPtr.Zero;
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
        ~PcreRegex() => Dispose();
    }
}
