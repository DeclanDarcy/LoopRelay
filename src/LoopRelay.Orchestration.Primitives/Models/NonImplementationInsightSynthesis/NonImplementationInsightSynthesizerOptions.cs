namespace LoopRelay.Orchestration.Models.NonImplementationInsightSynthesis;

public sealed record NonImplementationInsightSynthesizerOptions(
    string PromptName,
    string SynthesisPromptSourceHash,
    int MaxPromptPayloadCharacters,
    int MaxFileContentCharacters)
{
    public static NonImplementationInsightSynthesizerOptions Default { get; } =
        new(
            PromptName: "SynthesizeNonImplementationInsights",
            SynthesisPromptSourceHash: LoopRelay.Core.Prompts.SynthesizeNonImplementationInsights.SourceHash,
            MaxPromptPayloadCharacters: 65536,
            MaxFileContentCharacters: 4096);
}
