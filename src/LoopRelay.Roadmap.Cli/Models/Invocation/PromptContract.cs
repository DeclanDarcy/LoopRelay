using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

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
