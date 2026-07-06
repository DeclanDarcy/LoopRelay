using LoopRelay.Core.Prompts;

namespace LoopRelay.Execution.Models;

public sealed class ExecutionPrompt
{
    public string Text { get; init; } = string.Empty;

    public ExecutionPromptMetadata Metadata { get; init; } = new();

    /// <summary>Provenance for this turn — which catalog prompt rendered it and the artifacts it consumed/produces.</summary>
    public PromptProvenance? Provenance { get; init; }
}
