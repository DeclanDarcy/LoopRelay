using CommandCenter.Continuity.Primitives;

namespace CommandCenter.Continuity.Models;

public sealed class DecisionAssimilationRecord
{
    public string DecisionId { get; init; } = string.Empty;

    public string SourceRelativePath { get; init; } = string.Empty;

    public string Statement { get; init; } = string.Empty;

    public DecisionTaxonomy Taxonomy { get; init; }

    public DecisionTaxonomyBasis TaxonomyBasis { get; init; } = new();

    public DecisionAssimilationStatus Status { get; init; }

    public bool IsDurable { get; init; }

    public bool QualifiesForAssimilation { get; init; }

    public bool IsAssimilated { get; init; }

    public bool IsOmittedByLimit { get; init; }

    public string? ExclusionReason { get; init; }

    public string? OmissionReason { get; init; }

    public string? OperationalStatement { get; init; }

    public string? Rationale { get; init; }

    public IReadOnlyList<string> ConstraintsIntroduced { get; init; } = [];

    public IReadOnlyList<string> ConsequencesIntroduced { get; init; } = [];

    public IReadOnlyList<string> OpenQuestions { get; init; } = [];

    public IReadOnlyList<string> SourceEvidence { get; init; } = [];
}
