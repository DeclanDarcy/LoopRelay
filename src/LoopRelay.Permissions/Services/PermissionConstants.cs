using System.Text.RegularExpressions;

namespace LoopRelay.Permissions.Services;

internal static partial class PermissionConstants
{
    public const string FingerprintVersion = "v1";

    public static readonly HashSet<string> CommandsWithSubcommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "git", "npm", "pnpm", "yarn", "docker", "kubectl", "dotnet",
            "cargo", "go", "pip", "conda", "apt-get", "apt", "brew",
            "systemctl", "terraform", "az", "gcloud", "gh"
        };

    public static readonly HashSet<string> SafeTools =
        new(StringComparer.OrdinalIgnoreCase) { "read", "glob", "grep", "ls" };

    public static readonly HashSet<string> SafeBashCommands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "echo", "cat", "head", "tail", "wc", "sort", "uniq", "diff",
            "less", "more", "file", "stat", "which", "whoami", "date",
            "uname", "hostname", "basename", "dirname", "realpath",
            "true", "false", "test", "env", "printenv", "id", "groups",
            "tee", "tr", "cut", "paste", "fold", "fmt", "nl", "seq",
            "yes", "printf", "find", "xargs", "type", "command", "rg"
        };

    public static readonly Regex ChainSplitter =
        ChainSplitterRegex();

    [GeneratedRegex(@"\s*(?:&&|\|\||[;|])\s*", RegexOptions.CultureInvariant)]
    private static partial Regex ChainSplitterRegex();
}
