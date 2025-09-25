using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SNIBypassGUI.Common.Extensions
{
    public static class StreamExtensions
    {
#if !NET6_0_OR_GREATER
#warning 在 .NET 6 及更高版本中应使用 System.IO.Stream.ReadExactly 方法。
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException();

            int totalRead = 0;
            while (totalRead < count)
            {
                int n = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (n == 0)
                    throw new EndOfStreamException("Unable to read beyond the end of the stream.");
                totalRead += n;
            }
        }
#endif
    }
}