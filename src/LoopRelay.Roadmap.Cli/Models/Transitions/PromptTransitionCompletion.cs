using LoopRelay.Roadmap.Cli.Models.TransitionInputs;

namespace LoopRelay.Roadmap.Cli.Models.Transitions;

internal sealed record PromptTransitionCompletion(
    string CorrelationId,
    DateTimeOffset Started,
    DateTimeOffset Completed,
    long ElapsedMilliseconds,
    string Output,
    TransitionInputSnapshot InputSnapshot);
