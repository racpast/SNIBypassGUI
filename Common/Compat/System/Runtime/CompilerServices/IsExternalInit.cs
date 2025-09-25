#if !NET5_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
namespace System.Runtime.CompilerServices
{
#warning 在 .NET 5 及更高版本中，System.Runtime.CompilerServices.IsExternalInit 已由系统提供，无需手动定义。
    internal sealed class IsExternalInit { }
}
#endif