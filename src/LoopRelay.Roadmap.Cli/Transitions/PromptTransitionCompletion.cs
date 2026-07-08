using System.Diagnostics;

namespace LoopRelay.Roadmap.Cli;

internal sealed record PromptTransitionCompletion(
    string CorrelationId,
    DateTimeOffset Started,
    DateTimeOffset Completed,
    long ElapsedMilliseconds,
    string Output,
    TransitionInputSnapshot InputSnapshot);
