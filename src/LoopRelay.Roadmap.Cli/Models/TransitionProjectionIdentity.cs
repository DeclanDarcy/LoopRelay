namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record TransitionProjectionIdentity(
    string RuntimePromptName,
    string ProjectionPath,
    string? ProjectionHash);
