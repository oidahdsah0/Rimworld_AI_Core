// Shim for C# 9 `init` setters on legacy frameworks (< .NET 5).
// Placed in Core project so that compiler can locate the symbol.
namespace System.Runtime.CompilerServices
{
    internal sealed class IsExternalInit { }
}