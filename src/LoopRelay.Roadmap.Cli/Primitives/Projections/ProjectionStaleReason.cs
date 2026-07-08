namespace LoopRelay.Roadmap.Cli.Primitives.Projections;

internal enum ProjectionStaleReason
{
    MissingManifest,
    UnknownProvenance,
    ProjectionIdentityDrift,
    PromptIdentityDrift,
    PromptTemplateDrift,
    ProjectContextDrift,
    CausalInputDrift,
}
