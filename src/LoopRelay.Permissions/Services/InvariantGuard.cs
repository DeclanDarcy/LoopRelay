using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Services;

public sealed class InvariantGuard : IInvariantGuard
{
    private readonly PermissionPolicyOptions policy;

    public InvariantGuard()
        : this(PermissionPolicyOptions.Default)
    {
    }

    public InvariantGuard(PermissionPolicyOptions policy)
    {
        this.policy = PermissionPolicyFactory.MergeWithMinimum(policy);
    }

    public EvalResult Enforce(CanonicalCommand[] commands, EvalResult result)
    {
        PermissionHardDenyOptions hardDeny = policy.HardDeny;

        foreach (ref readonly CanonicalCommand command in commands.AsSpan())
        {
            if (hardDeny.PrivilegeEscalationCommands.Contains(command.Command))
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: privilege escalation is forbidden");
            }

            if (command.Command == "git" &&
                command.Subcommand == "push" &&
                command.Flags.Any(hardDeny.GitForcePushFlags.Contains))
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: git force push is forbidden");
            }

            if (hardDeny.NetworkFetchCommands.Contains(command.Command))
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: network fetch is forbidden");
            }

            if (hardDeny.IndirectShellExecution.Commands.Contains(command.Command) &&
                command.Flags.Contains(hardDeny.IndirectShellExecution.Flag, StringComparer.OrdinalIgnoreCase))
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: indirect shell execution is forbidden");
            }

            if (string.Equals(
                    command.Command,
                    hardDeny.RecursiveForceDelete.Command,
                    StringComparison.OrdinalIgnoreCase) &&
                PermissionEvaluatorEngine.IsRecursiveForceDelete(command.Flags, hardDeny.RecursiveForceDelete.FlagSets))
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: rm -rf is forbidden");
            }

            if (hardDeny.SystemControlCommands.Contains(command.Command))
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: system control is forbidden");
            }
        }

        return result;
    }
}
