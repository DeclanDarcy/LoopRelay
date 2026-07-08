using LoopRelay.Roadmap.Cli.Primitives.Projections;

namespace LoopRelay.Roadmap.Cli.Models.Invocation;

internal sealed record PromptContract(
    string RuntimePromptName,
    string RequiredProjectionRuntimePrompt,
    IReadOnlyList<string> RequiredInputs,
    IReadOnlyList<string> OptionalInputs,
    IReadOnlyList<string> RequiredOutputs,
    IReadOnlyList<string> AllowedDecisions,
    IReadOnlyList<string> BlockingOutputs,
    string ArtifactWriter,
    StaleProjectionPolicy StaleProjectionPolicy,
    string ParserName);
