namespace LoopRelay.Roadmap.Cli.Primitives;

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
