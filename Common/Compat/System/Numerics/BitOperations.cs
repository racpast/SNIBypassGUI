#if !NET5_0_OR_GREATER
namespace System.Numerics
{
    [Obsolete("升级到 .NET 5 或更高版本时，可以使用 System.Numerics.BitOperations 类。")]
    public static class BitOperations
    {
        public static int PopCount(ulong value)
        {
            // This is a classic, high-performance bit-fiddling algorithm (SWAR).
            const ulong c1 = 0x5555555555555555UL;
            const ulong c2 = 0x3333333333333333UL;
            const ulong c3 = 0x0F0F0F0F0F0F0F0FUL;
            const ulong c4 = 0x0101010101010101UL;

            value -= (value >> 1) & c1;
            value = (value & c2) + ((value >> 2) & c2);
            value = (value + (value >> 4)) & c3;
            value = (value * c4) >> 56;

            return (int)value;
        }
    }
}
#endif