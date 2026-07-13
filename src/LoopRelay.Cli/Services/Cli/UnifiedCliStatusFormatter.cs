using LoopRelay.Cli.Services.Application;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Cli.Services.Cli;

/// <summary>Pure rendering adapter. All interpretation is completed in the canonical snapshot.</summary>
internal static class UnifiedCliStatusFormatter
{
    public static string Format(CanonicalCliStatusSnapshot snapshot)
    {
        WorkflowResolutionResult resolution = snapshot.Resolution;
        RepositoryObservation observation = snapshot.Observation;
        string nextTransition = resolution.Explanation.EligibleTransitions.FirstOrDefault().Value ?? "(none)";
        var lines = new List<string>
        {
            $"Repository: {snapshot.RepositoryPath}",
            $"Snapshot: {snapshot.WorkspaceSnapshot?.SnapshotIdentity ?? "(legacy)"}",
            $"Invocation mode: {resolution.Selection.InvocationMode}",
            $"Selected chain: {resolution.Selection.SelectedChain}",
            $"Selected workflow: {resolution.Selection.SelectedWorkflow}",
            $"Current stage: {resolution.SelectedStage?.Value ?? "(none)"}",
            $"Next eligible transition: {nextTransition}",
            $"Satisfied gates: {List(resolution.Explanation.SatisfiedGates)}",
            $"Unsatisfied gates: {List(resolution.Explanation.UnsatisfiedGates)}",
        };

        if (resolution.Explanation.Warnings.Count > 0)
        {
            lines.Add("Warnings:");
            foreach (ResolutionWarning warning in resolution.Explanation.Warnings)
            {
                lines.Add($"  - {warning.Category}: {warning.Concern}");
                lines.Add($"    Remediation: {warning.Remediation}");
            }
        }

        if (snapshot.InputDrift.Count > 0)
        {
            lines.Add("Inputs changed since last consumption:");
            foreach (ConsumedInputDrift drift in snapshot.InputDrift)
            {
                string current = drift.CurrentSha256 is null ? "missing" : Abbreviate(drift.CurrentSha256);
                lines.Add($"  - {drift.Path}: consumed {Abbreviate(drift.ConsumedSha256)} now {current} ({drift.Workflow}/{drift.Transition})");
            }
        }

        AddContinuity(lines, snapshot.Continuity);
        lines.Add($"Pending effects: {List(snapshot.PendingEffects)}");
        lines.Add($"Pending dispatches: {List(snapshot.PendingDispatches)}");
        lines.Add($"Policy evaluations: {List(snapshot.PolicyEvaluations)}");
        lines.Add($"Compatibility: {List(snapshot.Compatibility)}");
        lines.Add($"Storage authority: {observation.StorageAuthority.Authority} ({observation.StorageAuthority.ConfidenceQualifier})");
        lines.Add($"User action required: {List(snapshot.RequiredActions)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddContinuity(List<string> lines, DecisionContinuityStatusSnapshot? continuity)
    {
        if (continuity is null) return;
        string ancestry = continuity.Ancestry.Count == 0
            ? "(none)"
            : string.Join(" <- ", continuity.Ancestry.Select(node =>
                $"{node.LineageId}:{node.ProviderSessionId}:{node.Mechanism}"));
        lines.Add($"Continuity active scopes: {continuity.ActiveScopeCount}");
        lines.Add($"Continuity active scope: {continuity.Active?.ScopeId ?? "(none)"}");
        lines.Add($"Continuity active lineage: {continuity.Lineage?.LineageId ?? "(none)"}");
        lines.Add($"Continuity provider thread: {continuity.Lineage?.ProviderSessionId ?? "(none)"}");
        lines.Add($"Continuity completeness: {continuity.Lineage?.Completeness.ToString() ?? "(none)"}");
        lines.Add($"Continuity ancestry: {ancestry}");
        lines.Add($"Unresolved recovery: {continuity.UnresolvedAttempt?.Status.ToString() ?? "(none)"}");
        lines.Add($"Unresolved decision turn: {continuity.UnresolvedTurn?.State.ToString() ?? "(none)"}");
        lines.Add($"Continuity diagnostic: {continuity.Diagnostic ?? "(none)"}");
    }

    private static string List(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none)" : string.Join(", ", values);

    private static string Abbreviate(string sha256) => sha256.Length > 8 ? sha256[..8] : sha256;
}
