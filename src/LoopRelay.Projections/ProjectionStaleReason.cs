namespace LoopRelay.Projections;

public enum ProjectionStaleReason
{
    MissingManifest,
    MissingProjectionArtifact,
    UnknownProvenance,
    ProjectionIdentityDrift,
    PromptIdentityDrift,
    PromptTemplateDrift,
    ProjectContextDrift,
    CausalInputDrift,
}
