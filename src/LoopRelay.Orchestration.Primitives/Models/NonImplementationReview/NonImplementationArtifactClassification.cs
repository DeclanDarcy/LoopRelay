namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationArtifactClassification(
    RepositoryChangedFileFacts File,
    NonImplementationArtifactRoute Route,
    string RuleId,
    IReadOnlyList<string> PathFacts,
    string Rationale,
    string ClassifierVersion);
