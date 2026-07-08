using LoopRelay.Roadmap.Cli.Primitives.Transitions;

namespace LoopRelay.Roadmap.Cli.Models.TransitionInputs;

internal sealed record TransitionArtifactInput(
    string Path,
    string Roles,
    bool Required,
    TransitionInputPresence Presence,
    string? Hash);
