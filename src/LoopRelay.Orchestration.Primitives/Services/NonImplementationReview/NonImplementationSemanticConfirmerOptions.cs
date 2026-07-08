using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed record NonImplementationSemanticConfirmerOptions(
    string PromptName,
    string ConfirmationPromptSourceHash,
    int MaxPromptPayloadCharacters)
{
    public static NonImplementationSemanticConfirmerOptions Default { get; } =
        new(
            PromptName: "ConfirmNonImplementationCandidate",
            ConfirmationPromptSourceHash: LoopRelay.Core.Prompts.ConfirmNonImplementationCandidate.SourceHash,
            MaxPromptPayloadCharacters: 32768);
}
