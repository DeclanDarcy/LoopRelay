using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Primitives.Evaluation;
using LoopRelay.Permissions.Primitives.Parsing;
using LoopRelay.Permissions.Primitives.Requests;
using LoopRelay.Permissions.Services.Evaluation;

namespace LoopRelay.Permissions.Tests.Services;

public sealed class PermissionEvaluatorEnginePolicyTests
{
    [Fact]
    public void Evaluate_HonorsConfiguredSafeBashCommands()
    {
        // A command NOT in the default SafeBashCommands set, allowed by a custom policy.
        PermissionPolicyOptions custom = WithSafeBashCommands("mytool");
        var engine = new PermissionEvaluatorEngine(custom);

        EvalResult result = engine.Evaluate([new CanonicalCommand("mytool", null, [], [])]);

        Assert.Equal(RuleDecision.Allow, result.Decision);
    }

    [Fact]
    public void Evaluate_DefaultPolicyBehaviorUnchanged()
    {
        var engine = new PermissionEvaluatorEngine();

        Assert.Equal(RuleDecision.Deny, engine.Evaluate([new CanonicalCommand("unknowncmd", null, [], [])]).Decision); // closed-world deny
        Assert.Equal(RuleDecision.Deny, engine.Evaluate([new CanonicalCommand("sudo", null, [], [])]).Decision);       // hard deny floor
    }

    [Fact]
    public void Evaluate_CustomPolicyCannotOverrideHardDenyFloor()
    {
        // The custom policy OMITS 'sudo' from its own HardDeny.PrivilegeEscalationCommands (as
        // if an operator's configuration never declared it) and additionally allows 'sudo' via
        // the configured safe-bash-commands list. Only PermissionEvaluatorEngine's constructor,
        // via PermissionPolicyFactory.MergeWithMinimum, puts 'sudo' back into HardDeny; if that
        // merge were skipped, this command would fall through to the allow list and be
        // permitted. Asserting Deny here proves the code-level floor rescues a config that
        // omits the protection.
        PermissionPolicyOptions permissive = WithSafeBashCommandsAndNoHardDenyFloor("sudo");
        var engine = new PermissionEvaluatorEngine(permissive);

        EvalResult result = engine.Evaluate([new CanonicalCommand("sudo", null, [], [])]);

        Assert.Equal(RuleDecision.Deny, result.Decision);
    }

    private static PermissionPolicyOptions WithSafeBashCommands(params string[] safeBashCommands)
    {
        PermissionPolicyOptions basePolicy = PermissionPolicyOptions.Default;

        return new PermissionPolicyOptions(
            basePolicy.FingerprintVersion,
            basePolicy.CommandsWithSubcommands,
            basePolicy.SafeTools,
            new HashSet<string>(safeBashCommands, StringComparer.OrdinalIgnoreCase),
            basePolicy.HardDeny,
            basePolicy.ReviewRequired,
            basePolicy.Allow);
    }

    private static PermissionPolicyOptions WithSafeBashCommandsAndNoHardDenyFloor(
        params string[] safeBashCommands)
    {
        PermissionPolicyOptions basePolicy = PermissionPolicyOptions.Default;

        var hardDenyWithoutFloor = new PermissionHardDenyOptions(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            basePolicy.HardDeny.RecursiveForceDelete,
            basePolicy.HardDeny.SystemControlCommands,
            basePolicy.HardDeny.NetworkFetchCommands,
            basePolicy.HardDeny.GitForcePushFlags,
            basePolicy.HardDeny.IndirectShellExecution);

        return new PermissionPolicyOptions(
            basePolicy.FingerprintVersion,
            basePolicy.CommandsWithSubcommands,
            basePolicy.SafeTools,
            new HashSet<string>(safeBashCommands, StringComparer.OrdinalIgnoreCase),
            hardDenyWithoutFloor,
            basePolicy.ReviewRequired,
            basePolicy.Allow);
    }
}
