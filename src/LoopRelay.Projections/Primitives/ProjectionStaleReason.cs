namespace LoopRelay.Projections.Primitives;

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
