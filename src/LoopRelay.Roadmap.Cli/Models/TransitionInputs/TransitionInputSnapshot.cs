using LoopRelay.Roadmap.Cli.Primitives.Transitions;

namespace LoopRelay.Roadmap.Cli.Models.TransitionInputs;

internal sealed record TransitionInputSnapshot(
    string RuntimePromptName,
    TransitionProjectionIdentity Projection,
    IReadOnlyList<TransitionArtifactInput> ArtifactInputs,
    string PromptContextHash,
    string SecondaryInputHash,
    string SnapshotHash)
{
    public TransitionPromptPolicyIdentity PromptPolicy { get; init; } =
        TransitionPromptPolicyIdentity.None;

    public IReadOnlyDictionary<string, string> ToInputArtifactHashes() =>
        new SortedDictionary<string, string>(
            ArtifactInputs
                .Where(input => input.Presence == TransitionInputPresence.Present && input.Hash is not null)
                .ToDictionary(input => input.Path, input => input.Hash!, StringComparer.Ordinal),
            StringComparer.Ordinal);
}
