using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionPackageRegenerationResult(
    Guid RepositoryId,
    string ProposalId,
    RefinementPlan Plan,
    DecisionPackageVersion BasePackageVersion,
    DecisionPackageVersion RegeneratedPackageVersion,
    DecisionPackageComparison Comparison,
    HumanAuthoringBurden HumanAuthoringBurden,
    IReadOnlyList<string> Diagnostics,
    DecisionRefinementArtifact? RefinementArtifact = null);
