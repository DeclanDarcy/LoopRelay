using LoopRelay.Permissions.Abstractions.Evaluation;
using LoopRelay.Permissions.Abstractions.Parsing;
using LoopRelay.Permissions.Abstractions.Security;
using LoopRelay.Permissions.Models.Evaluation;
using LoopRelay.Permissions.Primitives.Evaluation;
using LoopRelay.Permissions.Primitives.Parsing;
using LoopRelay.Permissions.Primitives.Requests;

namespace LoopRelay.Permissions.Services.Evaluation;

public sealed class PermissionHandler(
    ICommandParser parser,
    ICommandCanonicalizer canonicalizer,
    IFingerprintService fingerprint,
    IPermissionCache cache,
    IPermissionEvaluatorEngine engine,
    IInvariantGuard guard) : IPermissionHandler
{
    private static readonly PermissionEvaluationFlow EvaluationFlow = PermissionEvaluationFlow.Default;
    private readonly ICommandParser _parser = parser;
    private readonly ICommandCanonicalizer _canonicalizer = canonicalizer;
    private readonly IFingerprintService _fingerprint = fingerprint;
    private readonly IPermissionCache _cache = cache;
    private readonly IPermissionEvaluatorEngine _engine = engine;
    private readonly IInvariantGuard _guard = guard;

    public PermissionResult Evaluate(PermissionRequest request)
    {
        GuardEvaluationFlow(EvaluationFlow);

        ParseResult parsed = _parser.Parse(request.ToolName, request.RawCommand);
        if (parsed.HasUnknownSyntax)
        {
            return Deny(parsed.UnknownSyntaxReason);
        }

        if (parsed.Commands.Length == 0)
        {
            return Deny("Empty command");
        }

        CanonicalCommand[] canonical = _canonicalizer.Canonicalize(parsed.Commands);
        string key = _fingerprint.Compute(
            request.ToolName,
            request.RepoIdentity,
            request.WorkingDirectory,
            canonical);

        if (_cache.TryGet(key, out CacheEntry cached))
        {
            return new PermissionResult(cached.Decision, cached.Reason);
        }

        EvalResult evaluated = _engine.Evaluate(canonical);
        EvalResult guarded = _guard.Enforce(canonical, evaluated);

        _cache.Set(key, new CacheEntry(guarded.Decision, guarded.Reason));
        return new PermissionResult(guarded.Decision, guarded.Reason);
    }

    public void ClearCache() => _cache.Clear();

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
