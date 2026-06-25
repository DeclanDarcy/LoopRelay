using CommandCenter.Continuity.Primitives;

namespace CommandCenter.Continuity.Models;

public sealed class DecisionTaxonomyBasis
{
    public DecisionTaxonomy Taxonomy { get; init; }

    public IReadOnlyList<string> MatchedRules { get; init; } = [];

    public IReadOnlyList<string> MatchedEvidence { get; init; } = [];

    public bool IsHeuristicFallback { get; init; }

    public string? FallbackReason { get; init; }

    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}
