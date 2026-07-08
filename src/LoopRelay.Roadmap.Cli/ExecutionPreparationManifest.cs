using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoopRelay.Roadmap.Cli;

internal sealed record ExecutionPreparationManifest(
    string SchemaVersion,
    string ActiveEpicPath,
    string ActiveEpicHash,
    IReadOnlyList<ExecutionPreparationManifestInput> MilestoneSpecs,
    IReadOnlyList<DerivedArtifactManifestEntry> Artifacts)
{
    public const string CurrentSchemaVersion = "execution-preparation.v1";

    public static ExecutionPreparationManifest Empty { get; } = new(
        CurrentSchemaVersion,
        string.Empty,
        string.Empty,
        [],
        []);

    public IReadOnlyList<DerivedArtifactManifestEntry> ActiveArtifacts =>
        Artifacts.Where(entry => entry.ProvenanceStatus == DerivedArtifactProvenanceStatus.Trusted).ToArray();

    public DerivedArtifactManifestEntry? FindActive(string artifactKind, string artifactIdentity) =>
        ActiveArtifacts.FirstOrDefault(entry =>
            string.Equals(entry.ArtifactKind, artifactKind, StringComparison.Ordinal) &&
            string.Equals(entry.ArtifactIdentity, artifactIdentity, StringComparison.Ordinal));

    public ExecutionPreparationManifest WithAuthoritativeInputs(
        string activeEpicPath,
        string activeEpicHash,
        IReadOnlyList<ExecutionPreparationManifestInput> milestoneSpecs) =>
        this with
        {
            SchemaVersion = CurrentSchemaVersion,
            ActiveEpicPath = activeEpicPath,
            ActiveEpicHash = activeEpicHash,
            MilestoneSpecs = milestoneSpecs.OrderBy(input => input.Identity, StringComparer.Ordinal).ToArray(),
        };

    public ExecutionPreparationManifest UpsertActive(DerivedArtifactManifestEntry entry)
    {
        var next = Artifacts
            .Where(existing =>
                !string.Equals(existing.ArtifactKind, entry.ArtifactKind, StringComparison.Ordinal) ||
                !string.Equals(existing.ArtifactIdentity, entry.ArtifactIdentity, StringComparison.Ordinal))
            .Append(entry)
            .OrderBy(existing => existing.ArtifactKind, StringComparer.Ordinal)
            .ThenBy(existing => existing.ArtifactIdentity, StringComparer.Ordinal)
            .ToArray();

        return this with { Artifacts = next };
    }

    public ExecutionPreparationManifest SupersedeActiveArtifacts(
        string artifactKind,
        IReadOnlySet<string> currentActiveIdentities,
        IReadOnlyList<DerivedArtifactStaleReason> reasons)
    {
        var next = Artifacts
            .Select(entry =>
                entry.ProvenanceStatus == DerivedArtifactProvenanceStatus.Trusted &&
                string.Equals(entry.ArtifactKind, artifactKind, StringComparison.Ordinal) &&
                !currentActiveIdentities.Contains(entry.ArtifactIdentity)
                    ? entry.Supersede(reasons)
                    : entry)
            .OrderBy(existing => existing.ArtifactKind, StringComparer.Ordinal)
            .ThenBy(existing => existing.ArtifactIdentity, StringComparer.Ordinal)
            .ToArray();

        return this with { Artifacts = next };
    }
}
