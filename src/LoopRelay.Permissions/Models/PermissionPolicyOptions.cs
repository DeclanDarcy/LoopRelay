using System.Collections.Frozen;
using LoopRelay.Permissions.Services;

namespace LoopRelay.Permissions.Models;

public sealed class PermissionPolicyOptions
{
    internal PermissionPolicyOptions(
        string fingerprintVersion,
        IReadOnlySet<string> commandsWithSubcommands,
        IReadOnlySet<string> safeTools,
        IReadOnlySet<string> safeBashCommands,
        PermissionHardDenyOptions hardDeny,
        PermissionReviewRequiredOptions reviewRequired,
        PermissionAllowOptions allow)
    {
        FingerprintVersion = fingerprintVersion;
        CommandsWithSubcommands = commandsWithSubcommands;
        SafeTools = safeTools;
        SafeBashCommands = safeBashCommands;
        HardDeny = hardDeny;
        ReviewRequired = reviewRequired;
        Allow = allow;
    }

    public static PermissionPolicyOptions Default => PermissionPolicyFactory.Default;

    public string FingerprintVersion { get; }

    public IReadOnlySet<string> CommandsWithSubcommands { get; }

    public IReadOnlySet<string> SafeTools { get; }

    public IReadOnlySet<string> SafeBashCommands { get; }

    public PermissionHardDenyOptions HardDeny { get; }

    public PermissionReviewRequiredOptions ReviewRequired { get; }

    public PermissionAllowOptions Allow { get; }
}
