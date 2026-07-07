using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Services;

public sealed class InvariantGuard : IInvariantGuard
{
    public EvalResult Enforce(CanonicalCommand[] commands, EvalResult result)
    {
        foreach (ref readonly CanonicalCommand command in commands.AsSpan())
        {
            if (command.Command is "sudo" or "su" or "doas")
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: privilege escalation is forbidden");
            }

            if (command.Command == "git" &&
                command.Subcommand == "push" &&
                (command.Flags.Contains("--force") || command.Flags.Contains("-f")))
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: git force push is forbidden");
            }

            if (command.Command is "curl" or "wget")
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: network fetch is forbidden");
            }

            if (command.Command is "bash" or "sh" or "zsh" && command.Flags.Contains("-c"))
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: indirect shell execution is forbidden");
            }

            if (command.Command == "rm" && PermissionEvaluatorEngine.IsRecursiveForceDelete(command.Flags))
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: rm -rf is forbidden");
            }

            if (command.Command is "shutdown" or "reboot" or "halt" or "poweroff")
            {
                return new EvalResult(RuleDecision.Deny, "Invariant violation: system control is forbidden");
            }
        }

        return result;
    }
}
