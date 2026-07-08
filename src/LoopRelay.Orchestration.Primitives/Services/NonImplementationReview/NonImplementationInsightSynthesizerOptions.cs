using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

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
