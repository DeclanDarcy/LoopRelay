using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Cli.Services.Cli;

internal static class UnifiedCliStatusFormatter
{
    public static string Format(
        UnifiedCliInvocation invocation,
        RepositoryObservation observation,
        WorkflowResolutionResult resolution,
        DecisionContinuityStatusSnapshot? continuity = null)
    {
        string nextTransition = resolution.Explanation.EligibleTransitions.FirstOrDefault().Value ?? "(none)";
        string userAction = resolution.Explanation.Blockers.Count == 0
            ? "(none)"
            : string.Join("; ", resolution.Explanation.Blockers.Select(blocker => blocker.RequiredAction));
        string blockers = resolution.Explanation.Blockers.Count == 0
            ? "(none)"
            : string.Join("; ", resolution.Explanation.Blockers.Select(blocker => $"{blocker.Category}: {blocker.Reason}"));

        var lines = new List<string>
        {
            $"Repository: {invocation.Repository.Path}",
            $"Invocation mode: {resolution.Selection.InvocationMode}",
            $"Selected chain: {resolution.Selection.SelectedChain}",
            $"Selected workflow: {resolution.Selection.SelectedWorkflow}",
            $"Current stage: {resolution.SelectedStage?.Value ?? "(none)"}",
            $"Next eligible transition: {nextTransition}",
            $"Satisfied gates: {List(resolution.Explanation.SatisfiedGates)}",
            $"Unsatisfied gates: {List(resolution.Explanation.UnsatisfiedGates)}",
            $"Blockers: {blockers}",
            $"Storage authority: {observation.StorageAuthority.Authority} ({observation.StorageAuthority.ConfidenceQualifier})",
            $"User action required: {userAction}",
        };
        if (continuity is not null)
        {
            string ancestry = continuity.Ancestry.Count == 0
                ? "(none)"
                : string.Join(" <- ", continuity.Ancestry.Select(node =>
                    $"{node.LineageId}:{node.ProviderSessionId}:{node.Mechanism}"));
            string recovery = continuity.LatestAttempt is null
                ? "(none)"
                : $"{continuity.LatestAttempt.Status} id={continuity.LatestAttempt.AttemptId} " +
                  $"profile={continuity.LatestAttempt.ProfileDigest} plan={continuity.LatestAttempt.PlanDigest ?? "(none)"}";
            string unresolved = continuity.UnresolvedAttempt is null
                ? "(none)"
                : $"{continuity.UnresolvedAttempt.Status} id={continuity.UnresolvedAttempt.AttemptId}";
            string unresolvedTurn = continuity.UnresolvedTurn is null
                ? "(none)"
                : $"{continuity.UnresolvedTurn.State} id={continuity.UnresolvedTurn.TurnRecordId} " +
                  $"provider-turn={continuity.UnresolvedTurn.ProviderTurnId ?? "(unknown)"}";
            string requiredAction = continuity.Diagnostic is not null
                ? "repair continuity storage before Execute"
                : continuity.LatestAttempt?.Status == RecoveryAttemptStatus.ProtocolRepairRequired
                    ? "repair provider protocol/profile compatibility"
                    : continuity.UnresolvedAttempt is not null || continuity.UnresolvedTurn is not null
                        ? "reconcile the unresolved continuity operation"
                        : "(none)";
            lines.Add($"Continuity active scopes: {continuity.ActiveScopeCount}");
            lines.Add($"Continuity active scope: {continuity.Active?.ScopeId ?? "(none)"}");
            lines.Add($"Continuity active lineage: {continuity.Lineage?.LineageId ?? "(none)"}");
            lines.Add($"Continuity provider thread: {continuity.Lineage?.ProviderSessionId ?? "(none)"}");
            lines.Add($"Continuity completeness: {continuity.Lineage?.Completeness.ToString() ?? "(none)"}");
            lines.Add($"Continuity ancestry: {ancestry}");
            lines.Add($"Last recovery: {recovery}");
            lines.Add($"Unresolved recovery: {unresolved}");
            lines.Add($"Unresolved decision turn: {unresolvedTurn}");
            lines.Add($"Continuity diagnostic: {continuity.Diagnostic ?? "(none)"}");
            lines.Add($"Continuity operator action: {requiredAction}");
        }

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static string List(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none)" : string.Join(", ", values);
}
