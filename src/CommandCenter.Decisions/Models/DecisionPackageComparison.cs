namespace CommandCenter.Decisions.Models;

public sealed record DecisionPackageComparison(
    string ProposalId,
    string LeftPackageId,
    string RightPackageId,
    Guid RepositoryId,
    string LeftPackageFingerprint,
    string RightPackageFingerprint,
    bool RecommendationChanged,
    bool OptionsChanged,
    bool EvidenceChanged,
    bool RisksChanged,
    bool ContextFingerprintChanged,
    IReadOnlyList<DecisionRevisionFieldComparison> FieldComparisons,
    IReadOnlyList<DecisionOption> AddedOptions,
    IReadOnlyList<DecisionOption> RemovedOptions,
    IReadOnlyList<DecisionOption> ModifiedOptions,
    IReadOnlyList<string> AddedEvidence,
    IReadOnlyList<string> RemovedEvidence,
    IReadOnlyList<string> AddedRisks,
    IReadOnlyList<string> RemovedRisks,
    IReadOnlyList<string> Diagnostics);
