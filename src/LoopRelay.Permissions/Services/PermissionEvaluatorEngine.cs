using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Services;

public sealed class PermissionEvaluatorEngine : IPermissionEvaluatorEngine
{
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

    internal static EvalResult EvaluateSingle(in CanonicalCommand command)
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

    private static EvalResult? CheckHardDeny(in CanonicalCommand command)
    {
        if (command.Command is "sudo" or "su" or "doas")
        {
            return new EvalResult(RuleDecision.Deny, $"Privilege escalation command '{command.Command}' is forbidden");
        }

        if (command.Command == "rm" && IsRecursiveForceDelete(command.Flags))
        {
            return new EvalResult(RuleDecision.Deny, "Destructive operation 'rm -rf' is forbidden");
        }

        if (command.Command is "shutdown" or "reboot" or "halt" or "poweroff")
        {
            return new EvalResult(RuleDecision.Deny, $"System control command '{command.Command}' is forbidden");
        }

        if (command.Command is "curl" or "wget")
        {
            return new EvalResult(RuleDecision.Deny, $"Network fetch command '{command.Command}' is forbidden");
        }

        if (command.Command == "git" &&
            command.Subcommand == "push" &&
            (command.Flags.Contains("--force") || command.Flags.Contains("-f")))
        {
            return new EvalResult(RuleDecision.Deny, "Git force push is forbidden");
        }

        if (command.Command is "bash" or "sh" or "zsh" && command.Flags.Contains("-c"))
        {
            return new EvalResult(RuleDecision.Deny, $"Indirect shell execution '{command.Command} -c' is forbidden");
        }

        return null;
    }

    private static EvalResult? CheckReviewRequired(in CanonicalCommand command)
    {
        if (command.Command == "git" && command.Subcommand == "commit")
        {
            return command.Flags.Contains("--amend")
                ? new EvalResult(RuleDecision.Deny, "git commit --amend is not allowed")
                : new EvalResult(RuleDecision.Deny, "git commit requires review");
        }

        if (command.Command == "git" && command.Subcommand == "push")
        {
            return new EvalResult(RuleDecision.Deny, "git push requires review");
        }

        if (command.Subcommand == "install" &&
            command.Command is "npm" or "pnpm" or "yarn" or "pip" or "dotnet"
                or "cargo" or "apt-get" or "apt" or "brew" or "conda")
        {
            return new EvalResult(RuleDecision.Deny, $"{command.Command} install requires review");
        }

        if (command.Command is "docker" or "kubectl" or "terraform")
        {
            return new EvalResult(RuleDecision.Deny, $"Infrastructure command '{command.Command}' requires review");
        }

        return null;
    }

    private static EvalResult? CheckAllowList(in CanonicalCommand command)
    {
        if (PermissionConstants.SafeTools.Contains(command.Command))
        {
            return new EvalResult(RuleDecision.Allow, $"Tool '{command.Command}' is always safe");
        }

        if (PermissionConstants.SafeBashCommands.Contains(command.Command))
        {
            return new EvalResult(RuleDecision.Allow, $"Command '{command.Command}' is read-only and safe");
        }

        if (command.Command == "pwd")
        {
            return new EvalResult(RuleDecision.Allow, "pwd is always safe");
        }

        if (command.Command == "git" && command.Subcommand == "status")
        {
            return new EvalResult(RuleDecision.Allow, "git status is read-only");
        }

        if (command.Command == "git" && command.Subcommand == "diff")
        {
            return new EvalResult(RuleDecision.Allow, "git diff is read-only");
        }

        if (command.Command == "git" && command.Subcommand == "log" &&
            !command.Flags.Contains("-p") &&
            !command.Flags.Contains("--patch"))
        {
            return new EvalResult(RuleDecision.Allow, "git log is read-only");
        }

        if (command.Command == "dotnet" && command.Subcommand is "build" or "test" or "restore")
        {
            return new EvalResult(RuleDecision.Allow, $"dotnet {command.Subcommand} is safe");
        }

        if (command.Command is "npm" or "pnpm" or "yarn" && command.Subcommand is "test" or "run")
        {
            return new EvalResult(RuleDecision.Allow, "Test/script execution is safe");
        }

        if (command.Command == "pytest" || command.Command == "go" && command.Subcommand == "test")
        {
            return new EvalResult(RuleDecision.Allow, "Test execution is safe");
        }

        return null;
    }

    internal static bool IsRecursiveForceDelete(IReadOnlyCollection<string> flags) =>
        flags.Contains("-rf") ||
        flags.Contains("-fr") ||
        (flags.Contains("-r") && flags.Contains("-f")) ||
        (flags.Contains("-r") && flags.Contains("--force")) ||
        (flags.Contains("--recursive") && flags.Contains("-f")) ||
        (flags.Contains("--recursive") && flags.Contains("--force"));
}
