namespace LoopRelay.Roadmap.Cli.Models.TransitionInputs;

internal sealed record TransitionProjectionIdentity(
    string RuntimePromptName,
    string ProjectionPath,
    string? ProjectionHash);
