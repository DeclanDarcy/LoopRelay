// Polyfill: `record` types and `init` accessors require this marker type, which
// is absent from the netstandard2.0 reference assemblies the analyzer targets.
// Declaring it internally lets the compiler bind init-only setters. No runtime
// dependency; it is a compile-time marker only.

// ReSharper disable once CheckNamespace

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
