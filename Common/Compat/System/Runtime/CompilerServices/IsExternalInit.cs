#if !NET5_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// 升级至 .NET 5 或更高版本后，可以使用系统自带的 System.Runtime.CompilerServices.IsExternalInit 类。
    /// </summary>
    internal sealed class IsExternalInit { }
}
#endif
