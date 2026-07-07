using System.Collections.Frozen;

namespace LoopRelay.Permissions.Models;

public sealed class PermissionPolicyValidationException(string message) : InvalidOperationException(message);

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

public sealed class PermissionHardDenyOptions
{
    internal PermissionHardDenyOptions(
        IReadOnlySet<string> privilegeEscalationCommands,
        RecursiveForceDeleteOptions recursiveForceDelete,
        IReadOnlySet<string> systemControlCommands,
        IReadOnlySet<string> networkFetchCommands,
        IReadOnlySet<string> gitForcePushFlags,
        IndirectShellExecutionOptions indirectShellExecution)
    {
        PrivilegeEscalationCommands = privilegeEscalationCommands;
        RecursiveForceDelete = recursiveForceDelete;
        SystemControlCommands = systemControlCommands;
        NetworkFetchCommands = networkFetchCommands;
        GitForcePushFlags = gitForcePushFlags;
        IndirectShellExecution = indirectShellExecution;
    }

    public IReadOnlySet<string> PrivilegeEscalationCommands { get; }

    public RecursiveForceDeleteOptions RecursiveForceDelete { get; }

    public IReadOnlySet<string> SystemControlCommands { get; }

    public IReadOnlySet<string> NetworkFetchCommands { get; }

    public IReadOnlySet<string> GitForcePushFlags { get; }

    public IndirectShellExecutionOptions IndirectShellExecution { get; }
}

public sealed class RecursiveForceDeleteOptions
{
    internal RecursiveForceDeleteOptions(
        string command,
        IReadOnlyList<IReadOnlySet<string>> flagSets)
    {
        Command = command;
        FlagSets = flagSets;
    }

    public string Command { get; }

    public IReadOnlyList<IReadOnlySet<string>> FlagSets { get; }
}

public sealed class IndirectShellExecutionOptions
{
    internal IndirectShellExecutionOptions(
        IReadOnlySet<string> commands,
        string flag)
    {
        Commands = commands;
        Flag = flag;
    }

    public IReadOnlySet<string> Commands { get; }

    public string Flag { get; }
}

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

internal static class PermissionPolicyFactory
{
    public static readonly PermissionPolicyOptions Default = CreateDefault();

    public static readonly PermissionPolicyOptions Minimum = CreateMinimum();

    public static PermissionPolicyOptions MergeWithMinimum(PermissionPolicyOptions policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ValidateRequiredInvariants(MergeHardDeny(policy.HardDeny, Minimum.HardDeny));

        return new PermissionPolicyOptions(
            RequireScalar("permissions.fingerprintVersion", policy.FingerprintVersion),
            policy.CommandsWithSubcommands,
            policy.SafeTools,
            policy.SafeBashCommands,
            MergeHardDeny(policy.HardDeny, Minimum.HardDeny),
            policy.ReviewRequired,
            policy.Allow);
    }

    private static PermissionPolicyOptions CreateDefault() =>
        new(
            "v1",
            Set(
                "git",
                "npm",
                "pnpm",
                "yarn",
                "docker",
                "kubectl",
                "dotnet",
                "cargo",
                "go",
                "pip",
                "conda",
                "apt-get",
                "apt",
                "brew",
                "systemctl",
                "terraform",
                "az",
                "gcloud",
                "gh"),
            Set("read", "glob", "grep", "ls"),
            Set(
                "echo",
                "cat",
                "head",
                "tail",
                "wc",
                "sort",
                "uniq",
                "diff",
                "less",
                "more",
                "file",
                "stat",
                "which",
                "whoami",
                "date",
                "uname",
                "hostname",
                "basename",
                "dirname",
                "realpath",
                "true",
                "false",
                "test",
                "env",
                "printenv",
                "id",
                "groups",
                "tee",
                "tr",
                "cut",
                "paste",
                "fold",
                "fmt",
                "nl",
                "seq",
                "yes",
                "printf",
                "find",
                "xargs",
                "type",
                "command",
                "rg"),
            CreateMinimum().HardDeny,
            new PermissionReviewRequiredOptions(
                gitCommit: true,
                gitCommitAmendFlags: Set("--amend"),
                gitPush: true,
                installCommands: Set("npm", "pnpm", "yarn", "pip", "dotnet", "cargo", "apt-get", "apt", "brew", "conda"),
                installSubcommand: "install",
                infrastructureCommands: Set("docker", "kubectl", "terraform")),
            new PermissionAllowOptions(
                alwaysAllowedCommands: Set("pwd"),
                gitReadOnlySubcommands: Set("status", "diff"),
                gitLogAllowedUnlessFlags: Set("-p", "--patch"),
                dotnetAllowedSubcommands: Set("build", "test", "restore"),
                packageManagerAllowedSubcommands: Map(
                    ("npm", Set("test", "run")),
                    ("pnpm", Set("test", "run")),
                    ("yarn", Set("test", "run"))),
                testCommands: Map(
                    ("pytest", Set()),
                    ("go", Set("test")))));

    private static PermissionPolicyOptions CreateMinimum() =>
        new(
            "v1",
            Set(),
            Set(),
            Set(),
            new PermissionHardDenyOptions(
                privilegeEscalationCommands: Set("sudo", "su", "doas"),
                recursiveForceDelete: new RecursiveForceDeleteOptions(
                    "rm",
                    FlagSets(
                        ["-rf"],
                        ["-fr"],
                        ["-r", "-f"],
                        ["-r", "--force"],
                        ["--recursive", "-f"],
                        ["--recursive", "--force"])),
                systemControlCommands: Set("shutdown", "reboot", "halt", "poweroff"),
                networkFetchCommands: Set("curl", "wget"),
                gitForcePushFlags: Set("--force", "-f"),
                indirectShellExecution: new IndirectShellExecutionOptions(Set("bash", "sh", "zsh"), "-c")),
            new PermissionReviewRequiredOptions(
                gitCommit: false,
                gitCommitAmendFlags: Set(),
                gitPush: false,
                installCommands: Set(),
                installSubcommand: "install",
                infrastructureCommands: Set()),
            new PermissionAllowOptions(
                alwaysAllowedCommands: Set(),
                gitReadOnlySubcommands: Set(),
                gitLogAllowedUnlessFlags: Set(),
                dotnetAllowedSubcommands: Set(),
                packageManagerAllowedSubcommands: Map(),
                testCommands: Map()));

    private static PermissionHardDenyOptions MergeHardDeny(
        PermissionHardDenyOptions configured,
        PermissionHardDenyOptions minimum)
    {
        if (!string.Equals(
                configured.RecursiveForceDelete.Command,
                minimum.RecursiveForceDelete.Command,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new PermissionPolicyValidationException(
                $"permissions.hardDeny.recursiveForceDelete.command must be '{minimum.RecursiveForceDelete.Command}'.");
        }

        return new PermissionHardDenyOptions(
            Union(configured.PrivilegeEscalationCommands, minimum.PrivilegeEscalationCommands),
            new RecursiveForceDeleteOptions(
                minimum.RecursiveForceDelete.Command,
                UnionFlagSets(configured.RecursiveForceDelete.FlagSets, minimum.RecursiveForceDelete.FlagSets)),
            Union(configured.SystemControlCommands, minimum.SystemControlCommands),
            Union(configured.NetworkFetchCommands, minimum.NetworkFetchCommands),
            Union(configured.GitForcePushFlags, minimum.GitForcePushFlags),
            new IndirectShellExecutionOptions(
                Union(configured.IndirectShellExecution.Commands, minimum.IndirectShellExecution.Commands),
                minimum.IndirectShellExecution.Flag));
    }

    private static void ValidateRequiredInvariants(PermissionHardDenyOptions hardDeny)
    {
        RequireContains(
            hardDeny.PrivilegeEscalationCommands,
            Minimum.HardDeny.PrivilegeEscalationCommands,
            "permissions.hardDeny.privilegeEscalationCommands");
        RequireContains(
            hardDeny.SystemControlCommands,
            Minimum.HardDeny.SystemControlCommands,
            "permissions.hardDeny.systemControlCommands");
        RequireContains(
            hardDeny.NetworkFetchCommands,
            Minimum.HardDeny.NetworkFetchCommands,
            "permissions.hardDeny.networkFetchCommands");
        RequireContains(
            hardDeny.GitForcePushFlags,
            Minimum.HardDeny.GitForcePushFlags,
            "permissions.hardDeny.gitForcePushFlags");
        RequireContains(
            hardDeny.IndirectShellExecution.Commands,
            Minimum.HardDeny.IndirectShellExecution.Commands,
            "permissions.hardDeny.indirectShellExecution.commands");

        if (!string.Equals(
                hardDeny.RecursiveForceDelete.Command,
                Minimum.HardDeny.RecursiveForceDelete.Command,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new PermissionPolicyValidationException(
                $"permissions.hardDeny.recursiveForceDelete.command must be '{Minimum.HardDeny.RecursiveForceDelete.Command}'.");
        }

        foreach (IReadOnlySet<string> requiredFlagSet in Minimum.HardDeny.RecursiveForceDelete.FlagSets)
        {
            if (!hardDeny.RecursiveForceDelete.FlagSets.Any(flagSet => SetEquals(flagSet, requiredFlagSet)))
            {
                string flags = string.Join(", ", requiredFlagSet);
                throw new PermissionPolicyValidationException(
                    $"permissions.hardDeny.recursiveForceDelete.flagSets is missing required flag set [{flags}].");
            }
        }
    }

    internal static IReadOnlySet<string> Set(params string[] values) =>
        values.Select(value => RequireScalar("policy value", value))
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlyDictionary<string, IReadOnlySet<string>> Map(
        params (string Key, IReadOnlySet<string> Values)[] values)
    {
        var dictionary = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, IReadOnlySet<string> set) in values)
        {
            dictionary.Add(RequireScalar("policy dictionary key", key), set);
        }

        return dictionary.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<IReadOnlySet<string>> FlagSets(params string[][] values) =>
        values.Select(value => (IReadOnlySet<string>)Set(value)).ToArray();

    private static IReadOnlySet<string> Union(
        IReadOnlySet<string> configured,
        IReadOnlySet<string> minimum) =>
        configured.Concat(minimum).ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<IReadOnlySet<string>> UnionFlagSets(
        IReadOnlyList<IReadOnlySet<string>> configured,
        IReadOnlyList<IReadOnlySet<string>> minimum)
    {
        var merged = new List<IReadOnlySet<string>>(configured);
        foreach (IReadOnlySet<string> required in minimum)
        {
            if (!merged.Any(existing => SetEquals(existing, required)))
            {
                merged.Add(required);
            }
        }

        return merged.ToArray();
    }

    private static bool SetEquals(IReadOnlySet<string> left, IReadOnlySet<string> right) =>
        left.Count == right.Count && left.All(right.Contains);

    private static void RequireContains(
        IReadOnlySet<string> configured,
        IReadOnlySet<string> required,
        string path)
    {
        foreach (string value in required)
        {
            if (!configured.Contains(value))
            {
                throw new PermissionPolicyValidationException($"{path} is missing required value '{value}'.");
            }
        }
    }

    private static string RequireScalar(string path, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PermissionPolicyValidationException($"{path} must not be empty.");
        }

        return value.Trim();
    }
}
