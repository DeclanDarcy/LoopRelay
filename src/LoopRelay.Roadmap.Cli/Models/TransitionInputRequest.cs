namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record TransitionInputRequest(
    string RuntimePromptName,
    string ProjectionPath,
    string RenderedContext,
    string SecondaryInput,
    TransitionInputContext Context);
