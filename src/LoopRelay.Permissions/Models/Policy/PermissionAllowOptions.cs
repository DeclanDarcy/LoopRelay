namespace LoopRelay.Permissions.Models.Policy;

public sealed class PermissionAllowOptions
{
    internal PermissionAllowOptions(
        IReadOnlySet<string> alwaysAllowedCommands,
        IReadOnlySet<string> gitReadOnlySubcommands,
        IReadOnlySet<string> gitLogAllowedUnlessFlags,
        IReadOnlySet<string> dotnetAllowedSubcommands,
        IReadOnlyDictionary<string, IReadOnlySet<string>> packageManagerAllowedSubcommands,
        IReadOnlyDictionary<string, IReadOnlySet<string>> testCommands)
    {
        AlwaysAllowedCommands = alwaysAllowedCommands;
        GitReadOnlySubcommands = gitReadOnlySubcommands;
        GitLogAllowedUnlessFlags = gitLogAllowedUnlessFlags;
        DotnetAllowedSubcommands = dotnetAllowedSubcommands;
        PackageManagerAllowedSubcommands = packageManagerAllowedSubcommands;
        TestCommands = testCommands;
    }

    public IReadOnlySet<string> AlwaysAllowedCommands { get; }

    public IReadOnlySet<string> GitReadOnlySubcommands { get; }

    public IReadOnlySet<string> GitLogAllowedUnlessFlags { get; }

    public IReadOnlySet<string> DotnetAllowedSubcommands { get; }

    public IReadOnlyDictionary<string, IReadOnlySet<string>> PackageManagerAllowedSubcommands { get; }

    public IReadOnlyDictionary<string, IReadOnlySet<string>> TestCommands { get; }
}
