namespace LoopRelay.Roadmap.Cli;

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
