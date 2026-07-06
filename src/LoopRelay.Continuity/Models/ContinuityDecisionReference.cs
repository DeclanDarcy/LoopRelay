using LoopRelay.Continuity.Primitives;

namespace LoopRelay.Continuity.Models;

public sealed class ContinuityDecisionReference
{
    public string DecisionId { get; init; } = string.Empty;

    public string SourceRelativePath { get; init; } = string.Empty;

    public string Statement { get; init; } = string.Empty;

    public DecisionTaxonomy Taxonomy { get; init; }
}
