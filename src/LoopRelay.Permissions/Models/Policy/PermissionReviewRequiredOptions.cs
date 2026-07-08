namespace LoopRelay.Permissions.Models.Policy;

public sealed class PermissionReviewRequiredOptions
{
    internal PermissionReviewRequiredOptions(
        bool gitCommit,
        IReadOnlySet<string> gitCommitAmendFlags,
        bool gitPush,
        IReadOnlySet<string> installCommands,
        string installSubcommand,
        IReadOnlySet<string> infrastructureCommands)
    {
        GitCommit = gitCommit;
        GitCommitAmendFlags = gitCommitAmendFlags;
        GitPush = gitPush;
        InstallCommands = installCommands;
        InstallSubcommand = installSubcommand;
        InfrastructureCommands = infrastructureCommands;
    }

    public bool GitCommit { get; }

    public IReadOnlySet<string> GitCommitAmendFlags { get; }

    public bool GitPush { get; }

    public IReadOnlySet<string> InstallCommands { get; }

    public string InstallSubcommand { get; }

    public IReadOnlySet<string> InfrastructureCommands { get; }
}
