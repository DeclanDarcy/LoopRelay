namespace LoopRelay.Projections.Models;

public sealed record ProjectionPromptMetadata(
    string PromptName,
    string PromptType,
    string SourceHash);
