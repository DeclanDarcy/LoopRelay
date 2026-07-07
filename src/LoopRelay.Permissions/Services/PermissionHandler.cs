using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Permissions.Services;

public sealed class PermissionHandler(
    ICommandParser parser,
    ICommandCanonicalizer canonicalizer,
    IFingerprintService fingerprint,
    IPermissionCache cache,
    IPermissionEvaluatorEngine engine,
    IInvariantGuard guard) : IPermissionHandler
{
    private static readonly PermissionEvaluationFlow EvaluationFlow = PermissionEvaluationFlow.Default;

    public PermissionResult Evaluate(PermissionRequest request)
    {
        GuardEvaluationFlow(EvaluationFlow);

        ParseResult parsed = parser.Parse(request.ToolName, request.RawCommand);
        if (parsed.HasUnknownSyntax)
        {
            return Deny(parsed.UnknownSyntaxReason);
        }

        if (parsed.Commands.Length == 0)
        {
            return Deny("Empty command");
        }

        CanonicalCommand[] canonical = canonicalizer.Canonicalize(parsed.Commands);
        string key = fingerprint.Compute(
            request.ToolName,
            request.RepoIdentity,
            request.WorkingDirectory,
            canonical);

        if (cache.TryGet(key, out CacheEntry cached))
        {
            return new PermissionResult(cached.Decision, cached.Reason);
        }

        EvalResult evaluated = engine.Evaluate(canonical);
        EvalResult guarded = guard.Enforce(canonical, evaluated);

        cache.Set(key, new CacheEntry(guarded.Decision, guarded.Reason));
        return new PermissionResult(guarded.Decision, guarded.Reason);
    }

    public void ClearCache() => cache.Clear();

    private static PermissionResult Deny(string? reason) =>
        new(RuleDecision.Deny, reason ?? "Unknown error");

    private static void GuardEvaluationFlow(PermissionEvaluationFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        RequireStep(flow, PermissionEvaluationFlowStep.RequestReceived);
        RequireStep(flow, PermissionEvaluationFlowStep.ParseCommand);
        RequireStep(flow, PermissionEvaluationFlowStep.CanonicalizeCommand);
        RequireStep(flow, PermissionEvaluationFlowStep.ComputeFingerprint);
        RequireStep(flow, PermissionEvaluationFlowStep.ReadDecisionCache);
        RequireStep(flow, PermissionEvaluationFlowStep.EvaluateRules);
        RequireStep(flow, PermissionEvaluationFlowStep.EnforceInvariants);
        RequireStep(flow, PermissionEvaluationFlowStep.WriteDecisionCache);
        RequireStep(flow, PermissionEvaluationFlowStep.ReturnDecision);

        RequireBefore(flow, PermissionEvaluationFlowStep.ParseCommand, PermissionEvaluationFlowStep.CanonicalizeCommand);

        if (flow.TerminalParserFailuresBypassCache)
        {
            RequireBefore(flow, PermissionEvaluationFlowStep.RejectUnknownSyntax, PermissionEvaluationFlowStep.ComputeFingerprint);
            RequireBefore(flow, PermissionEvaluationFlowStep.RejectEmptyCommand, PermissionEvaluationFlowStep.ComputeFingerprint);
        }

        if (flow.CacheLookupPrecedesRuleEvaluation)
        {
            RequireBefore(flow, PermissionEvaluationFlowStep.ReadDecisionCache, PermissionEvaluationFlowStep.EvaluateRules);
        }

        if (flow.InvariantGuardRunsAfterRuleEvaluation)
        {
            RequireBefore(flow, PermissionEvaluationFlowStep.EvaluateRules, PermissionEvaluationFlowStep.EnforceInvariants);
        }
    }

    private static void RequireStep(PermissionEvaluationFlow flow, PermissionEvaluationFlowStep step)
    {
        if (!flow.Steps.Contains(step))
        {
            throw new InvalidOperationException($"Permission evaluation flow is missing required step '{step}'.");
        }
    }

    private static void RequireBefore(
        PermissionEvaluationFlow flow,
        PermissionEvaluationFlowStep before,
        PermissionEvaluationFlowStep after)
    {
        int beforeIndex = IndexOf(flow, before);
        int afterIndex = IndexOf(flow, after);

        if (beforeIndex >= afterIndex)
        {
            throw new InvalidOperationException($"Permission evaluation flow must run '{before}' before '{after}'.");
        }
    }

    private static int IndexOf(PermissionEvaluationFlow flow, PermissionEvaluationFlowStep step)
    {
        for (int i = 0; i < flow.Steps.Count; i++)
        {
            if (flow.Steps[i] == step)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"Permission evaluation flow is missing required step '{step}'.");
    }
}
