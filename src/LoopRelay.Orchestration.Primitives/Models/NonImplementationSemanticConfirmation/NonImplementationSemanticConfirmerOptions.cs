namespace LoopRelay.Orchestration.Models.NonImplementationReview;

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
