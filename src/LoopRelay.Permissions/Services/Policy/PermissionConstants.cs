using System.Text.RegularExpressions;

namespace LoopRelay.Permissions.Services.Policy;

internal static partial class PermissionConstants
{
    public static readonly Regex ChainSplitter =
        ChainSplitterRegex();

    [GeneratedRegex(@"\s*(?:&&|\|\||[;|])\s*", RegexOptions.CultureInvariant)]
    private static partial Regex ChainSplitterRegex();
}
