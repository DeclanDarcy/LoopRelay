namespace CommandCenter.Decisions.Models;

public sealed record DecisionPackageRegenerationResult(
    Guid RepositoryId,
    string ProposalId,
    RefinementPlan Plan,
    DecisionPackageVersion BasePackageVersion,
    DecisionPackageVersion RegeneratedPackageVersion,
    DecisionPackageComparison Comparison,
    IReadOnlyList<string> Diagnostics);
