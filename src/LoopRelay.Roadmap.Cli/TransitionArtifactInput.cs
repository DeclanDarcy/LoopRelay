using System.Text;

namespace LoopRelay.Roadmap.Cli;

internal sealed record TransitionArtifactInput(
    string Path,
    string Roles,
    bool Required,
    TransitionInputPresence Presence,
    string? Hash);
