namespace LoopRelay.Projections;

public sealed record ProjectionPromptMetadata(
    string PromptName,
    string PromptType,
    string SourceHash);
