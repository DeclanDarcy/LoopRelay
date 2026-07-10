using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Cli.Services.Cli;

internal static class UnifiedCliStatusFormatter
{
    public static string Format(
        UnifiedCliInvocation invocation,
        RepositoryObservation observation,
        WorkflowResolutionResult resolution)
    {
        string nextTransition = resolution.Explanation.EligibleTransitions.FirstOrDefault().Value ?? "(none)";
        string userAction = resolution.Explanation.Blockers.Count == 0
            ? "(none)"
            : string.Join("; ", resolution.Explanation.Blockers.Select(blocker => blocker.RequiredAction));
        string blockers = resolution.Explanation.Blockers.Count == 0
            ? "(none)"
            : string.Join("; ", resolution.Explanation.Blockers.Select(blocker => $"{blocker.Category}: {blocker.Reason}"));

        return string.Join(
            Environment.NewLine,
            [
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
            ]);
    }

    private static string List(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(none)" : string.Join(", ", values);
}
