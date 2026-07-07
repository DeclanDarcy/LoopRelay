namespace LoopRelay.Permissions.Models;

public enum PermissionEvaluationFlowStep
{
    RequestReceived = 0,
    ParseCommand = 1,
    RejectUnknownSyntax = 2,
    RejectEmptyCommand = 3,
    CanonicalizeCommand = 4,
    ComputeFingerprint = 5,
    ReadDecisionCache = 6,
    EvaluateRules = 7,
    EnforceInvariants = 8,
    WriteDecisionCache = 9,
    ReturnDecision = 10,
}

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
