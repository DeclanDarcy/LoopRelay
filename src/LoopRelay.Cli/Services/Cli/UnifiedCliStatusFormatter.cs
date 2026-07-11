using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Cli.Services.Cli;

internal static class UnifiedCliStatusFormatter
{
    public static string Format(
        UnifiedCliInvocation invocation,
        RepositoryObservation observation,
        WorkflowResolutionResult resolution,
        IReadOnlyList<ConsumedInputDrift>? inputDrift = null)
    {
        string nextTransition = resolution.Explanation.EligibleTransitions.FirstOrDefault().Value ?? "(none)";
        string userAction = resolution.Explanation.Warnings.Count == 0
            ? "(none)"
            : string.Join("; ", resolution.Explanation.Warnings.Select(warning => warning.Remediation));

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
        };

        // Warnings are surfaced only when they exist; a warning-free status has no warnings section.
        if (resolution.Explanation.Warnings.Count > 0)
        {
            lines.Add("Warnings:");
            foreach (ResolutionWarning warning in resolution.Explanation.Warnings)
            {
                lines.Add($"  - {warning.Category}: {warning.Concern}");
                lines.Add($"    Remediation: {warning.Remediation}");
            }
        }

        // Passive staleness is surfaced only when a consumed input has drifted; an
        // unchanged working tree has no staleness section.
        if (inputDrift is { Count: > 0 })
        {
            lines.Add("Inputs changed since last consumption:");
            foreach (ConsumedInputDrift drift in inputDrift)
            {
                string current = drift.CurrentSha256 is null ? "missing" : Abbreviate(drift.CurrentSha256);
                lines.Add($"  - {drift.Path}: consumed {Abbreviate(drift.ConsumedSha256)} now {current} ({drift.Workflow}/{drift.Transition})");
            }
        }

        lines.Add($"Storage authority: {observation.StorageAuthority.Authority} ({observation.StorageAuthority.ConfidenceQualifier})");
        lines.Add($"User action required: {userAction}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string List(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none)" : string.Join(", ", values);

    private static string Abbreviate(string sha256) =>
        sha256.Length > 8 ? sha256[..8] : sha256;
}
