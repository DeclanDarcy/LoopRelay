namespace LoopRelay.Projections.Models.Provenance;

public sealed record ProjectionPromptMetadata(
    string PromptName,
    string PromptType,
    string SourceHash);
