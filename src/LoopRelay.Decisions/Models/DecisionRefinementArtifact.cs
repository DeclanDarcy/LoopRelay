using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionRefinementArtifact(
    string Id,
    Guid RepositoryId,
    string ProposalId,
    DateTimeOffset CreatedAt,
    DecisionPackageRegenerationRequest Request,
    IReadOnlyList<RefinementDirective> Directives,
    RefinementPlan Plan,
    string BasePackageId,
    string BasePackageFingerprint,
    string RegeneratedPackageId,
    string RegeneratedPackageFingerprint,
    DecisionPackageComparison Comparison,
    HumanAuthoringBurden HumanAuthoringBurden,
    IReadOnlyList<string> Diagnostics);
