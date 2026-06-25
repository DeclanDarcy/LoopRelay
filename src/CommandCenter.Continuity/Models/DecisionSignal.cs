using CommandCenter.Continuity.Primitives;

namespace CommandCenter.Continuity.Models;

public sealed class DecisionSignal
{
    public string DecisionId { get; init; } = string.Empty;

    public DecisionTaxonomy Taxonomy { get; init; }

    public DecisionTaxonomyBasis TaxonomyBasis { get; init; } = new();

    public string Statement { get; init; } = string.Empty;

    public string? Rationale { get; init; }

    public IReadOnlyList<string> ConstraintsIntroduced { get; init; } = [];

    public IReadOnlyList<string> Consequences { get; init; } = [];

    public IReadOnlyList<string> OpenQuestions { get; init; } = [];

    public bool IsSupersededOrRetired { get; init; }

    public string SourceRelativePath { get; init; } = string.Empty;
}
