using System.Text;

namespace LoopRelay.Roadmap.Cli;

internal sealed record TransitionInputSnapshot(
    string RuntimePromptName,
    TransitionProjectionIdentity Projection,
    IReadOnlyList<TransitionArtifactInput> ArtifactInputs,
    string PromptContextHash,
    string SecondaryInputHash,
    string SnapshotHash)
{
    public IReadOnlyDictionary<string, string> ToInputArtifactHashes() =>
        new SortedDictionary<string, string>(
            ArtifactInputs
                .Where(input => input.Presence == TransitionInputPresence.Present && input.Hash is not null)
                .ToDictionary(input => input.Path, input => input.Hash!, StringComparer.Ordinal),
            StringComparer.Ordinal);
}
