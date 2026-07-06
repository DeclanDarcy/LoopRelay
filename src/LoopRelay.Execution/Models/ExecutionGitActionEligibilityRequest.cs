namespace LoopRelay.Execution.Models;

public sealed class ExecutionGitActionEligibilityRequest
{
    public string? CommitMessage { get; init; }

    public IReadOnlyList<string> SelectedPaths { get; init; } = [];
}
