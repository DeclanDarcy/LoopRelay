using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Primitives;

namespace LoopRelay.Permissions.Services;

public sealed class PermissionEvaluatorEngine : IPermissionEvaluatorEngine
{
    private readonly PermissionPolicyOptions policy;

    public PermissionEvaluatorEngine()
        : this(PermissionPolicyOptions.Default)
    {
    }

    public PermissionEvaluatorEngine(PermissionPolicyOptions policy)
    {
        this.policy = PermissionPolicyFactory.MergeWithMinimum(policy);
    }

    public EvalResult Evaluate(CanonicalCommand[] commands)
    {
        var aggregated = RuleDecision.Allow;
        string aggregatedReason = "All commands allowed";

        foreach (ref readonly CanonicalCommand command in commands.AsSpan())
        {
            EvalResult single = EvaluateSingle(command);
            if (single.Decision == RuleDecision.Deny)
            {
                return single;
            }

            if (single.Decision != RuleDecision.Allow && aggregated == RuleDecision.Allow)
            {
                aggregated = single.Decision;
                aggregatedReason = single.Reason;
            }
        }

        return new EvalResult(aggregated, aggregatedReason);
    }

    internal static EvalResult EvaluateSingle(in CanonicalCommand command) =>
        new PermissionEvaluatorEngine().EvaluateSingleCore(command);

    private EvalResult EvaluateSingleCore(in CanonicalCommand command)
    {
        EvalResult? result;

        result = CheckHardDeny(command);
        if (result is not null)
        {
            return result.Value;
        }

        result = CheckReviewRequired(command);
        if (result is not null)
        {
            return result.Value;
        }

        result = CheckAllowList(command);
        if (result is not null)
        {
            return result.Value;
        }

        return new EvalResult(RuleDecision.Deny, $"No rule matched command '{command.Command}' (closed-world deny)");
    }

    private EvalResult? CheckHardDeny(in CanonicalCommand command)
    {
        PermissionHardDenyOptions hardDeny = policy.HardDeny;

        if (hardDeny.PrivilegeEscalationCommands.Contains(command.Command))
        {
            return new EvalResult(RuleDecision.Deny, $"Privilege escalation command '{command.Command}' is forbidden");
        }

        if (string.Equals(
                command.Command,
                hardDeny.RecursiveForceDelete.Command,
                StringComparison.OrdinalIgnoreCase) &&
            IsRecursiveForceDelete(command.Flags, hardDeny.RecursiveForceDelete.FlagSets))
        {
            return new EvalResult(RuleDecision.Deny, "Destructive operation 'rm -rf' is forbidden");
        }

        if (hardDeny.SystemControlCommands.Contains(command.Command))
        {
            return new EvalResult(RuleDecision.Deny, $"System control command '{command.Command}' is forbidden");
        }

        if (hardDeny.NetworkFetchCommands.Contains(command.Command))
        {
            return new EvalResult(RuleDecision.Deny, $"Network fetch command '{command.Command}' is forbidden");
        }

        if (command.Command == "git" &&
            command.Subcommand == "push" &&
            command.Flags.Any(hardDeny.GitForcePushFlags.Contains))
        {
            return new EvalResult(RuleDecision.Deny, "Git force push is forbidden");
        }

        if (hardDeny.IndirectShellExecution.Commands.Contains(command.Command) &&
            command.Flags.Contains(hardDeny.IndirectShellExecution.Flag, StringComparer.OrdinalIgnoreCase))
        {
            return new EvalResult(
                RuleDecision.Deny,
                $"Indirect shell execution '{command.Command} {hardDeny.IndirectShellExecution.Flag}' is forbidden");
        }

        return null;
    }

    private EvalResult? CheckReviewRequired(in CanonicalCommand command)
    {
        PermissionReviewRequiredOptions reviewRequired = policy.ReviewRequired;

        if (reviewRequired.GitCommit && command.Command == "git" && command.Subcommand == "commit")
        {
            return command.Flags.Any(reviewRequired.GitCommitAmendFlags.Contains)
                ? new EvalResult(RuleDecision.Deny, "git commit --amend is not allowed")
                : new EvalResult(RuleDecision.Deny, "git commit requires review");
        }

        if (reviewRequired.GitPush && command.Command == "git" && command.Subcommand == "push")
        {
            return new EvalResult(RuleDecision.Deny, "git push requires review");
        }

        if (string.Equals(
                command.Subcommand,
                reviewRequired.InstallSubcommand,
                StringComparison.OrdinalIgnoreCase) &&
            reviewRequired.InstallCommands.Contains(command.Command))
        {
            return new EvalResult(RuleDecision.Deny, $"{command.Command} install requires review");
        }

        if (reviewRequired.InfrastructureCommands.Contains(command.Command))
        {
            return new EvalResult(RuleDecision.Deny, $"Infrastructure command '{command.Command}' requires review");
        }

        return null;
    }

    private EvalResult? CheckAllowList(in CanonicalCommand command)
    {
        PermissionAllowOptions allow = policy.Allow;

        if (policy.SafeTools.Contains(command.Command))
        {
            return new EvalResult(RuleDecision.Allow, $"Tool '{command.Command}' is always safe");
        }

        if (policy.SafeBashCommands.Contains(command.Command))
        {
            return new EvalResult(RuleDecision.Allow, $"Command '{command.Command}' is read-only and safe");
        }

        if (allow.AlwaysAllowedCommands.Contains(command.Command))
        {
            return new EvalResult(RuleDecision.Allow, $"{command.Command} is always safe");
        }

        if (command.Command == "git" &&
            command.Subcommand is not null &&
            allow.GitReadOnlySubcommands.Contains(command.Subcommand))
        {
            return new EvalResult(RuleDecision.Allow, $"git {command.Subcommand} is read-only");
        }

        if (command.Command == "git" && command.Subcommand == "log" &&
            !command.Flags.Any(allow.GitLogAllowedUnlessFlags.Contains))
        {
            return new EvalResult(RuleDecision.Allow, "git log is read-only");
        }

        if (command.Command == "dotnet" &&
            command.Subcommand is not null &&
            allow.DotnetAllowedSubcommands.Contains(command.Subcommand))
        {
            return new EvalResult(RuleDecision.Allow, $"dotnet {command.Subcommand} is safe");
        }

        if (command.Subcommand is not null &&
            allow.PackageManagerAllowedSubcommands.TryGetValue(command.Command, out IReadOnlySet<string>? allowedSubcommands) &&
            allowedSubcommands.Contains(command.Subcommand))
        {
            return new EvalResult(RuleDecision.Allow, "Test/script execution is safe");
        }

        if (allow.TestCommands.TryGetValue(command.Command, out IReadOnlySet<string>? testSubcommands) &&
            (testSubcommands.Count == 0 && command.Subcommand is null ||
             command.Subcommand is not null && testSubcommands.Contains(command.Subcommand)))
        {
            return new EvalResult(RuleDecision.Allow, "Test execution is safe");
        }

        return null;
    }

    internal static bool IsRecursiveForceDelete(IReadOnlyCollection<string> flags) =>
        IsRecursiveForceDelete(flags, PermissionPolicyOptions.Default.HardDeny.RecursiveForceDelete.FlagSets);

    internal static bool IsRecursiveForceDelete(
        IReadOnlyCollection<string> flags,
        IReadOnlyList<IReadOnlySet<string>> configuredFlagSets)
    {
        foreach (IReadOnlySet<string> configuredFlagSet in configuredFlagSets)
        {
            if (configuredFlagSet.All(flag => flags.Contains(flag, StringComparer.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
