using System.Text;

namespace LoopRelay.Roadmap.Cli;

internal sealed record TransitionProjectionIdentity(
    string RuntimePromptName,
    string ProjectionPath,
    string? ProjectionHash);
