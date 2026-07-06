using LoopRelay.Decisions.Models;

namespace LoopRelay.Execution.Models;

public sealed class ExecutionGovernedConflictDiagnostic
{
    public string Id { get; init; } = string.Empty;

    public string DecisionId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Statement { get; init; } = string.Empty;

    public string ConflictingExcerpt { get; init; } = string.Empty;

    public string ConflictReason { get; init; } = string.Empty;

    public string AffectedContext { get; init; } = string.Empty;

    public string AffectedPromptSection { get; init; } = string.Empty;

    public string RecommendedResolution { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string OriginatingAuthority { get; init; } = string.Empty;

    public IReadOnlyList<DecisionSourceReference> Sources { get; init; } = Array.Empty<DecisionSourceReference>();

    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Diagnostics { get; init; } = Array.Empty<string>();
}
