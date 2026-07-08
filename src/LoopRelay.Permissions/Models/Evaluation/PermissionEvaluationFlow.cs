using LoopRelay.Permissions.Primitives;

namespace LoopRelay.Permissions.Models;

public sealed record PermissionEvaluationFlow
{
    public static PermissionEvaluationFlow Default { get; } = new()
    {
        Steps =
        [
            PermissionEvaluationFlowStep.RequestReceived,
            PermissionEvaluationFlowStep.ParseCommand,
            PermissionEvaluationFlowStep.RejectUnknownSyntax,
            PermissionEvaluationFlowStep.RejectEmptyCommand,
            PermissionEvaluationFlowStep.CanonicalizeCommand,
            PermissionEvaluationFlowStep.ComputeFingerprint,
            PermissionEvaluationFlowStep.ReadDecisionCache,
            PermissionEvaluationFlowStep.EvaluateRules,
            PermissionEvaluationFlowStep.EnforceInvariants,
            PermissionEvaluationFlowStep.WriteDecisionCache,
            PermissionEvaluationFlowStep.ReturnDecision
        ],
        TerminalParserFailuresBypassCache = true,
        CacheLookupPrecedesRuleEvaluation = true,
        InvariantGuardRunsAfterRuleEvaluation = true
    };

    public required IReadOnlyList<PermissionEvaluationFlowStep> Steps { get; init; }

    public required bool TerminalParserFailuresBypassCache { get; init; }

    public required bool CacheLookupPrecedesRuleEvaluation { get; init; }

    public required bool InvariantGuardRunsAfterRuleEvaluation { get; init; }
}
