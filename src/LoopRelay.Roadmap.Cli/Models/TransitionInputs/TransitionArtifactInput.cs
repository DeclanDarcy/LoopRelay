using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record TransitionArtifactInput(
    string Path,
    string Roles,
    bool Required,
    TransitionInputPresence Presence,
    string? Hash);
