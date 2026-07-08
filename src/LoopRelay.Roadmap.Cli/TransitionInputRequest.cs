using System.Text;

namespace LoopRelay.Roadmap.Cli;

internal sealed record TransitionInputRequest(
    string RuntimePromptName,
    string ProjectionPath,
    string RenderedContext,
    string SecondaryInput,
    TransitionInputContext Context);
