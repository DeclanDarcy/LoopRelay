using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoopRelay.Roadmap.Cli;

internal sealed record SelectionProvenanceManifest(
    string SchemaVersion,
    IReadOnlyList<DerivedArtifactManifestEntry> Selections)
{
    public const string CurrentSchemaVersion = "selection-provenance.v1";

    public static SelectionProvenanceManifest Empty { get; } = new(
        CurrentSchemaVersion,
        []);

    public IReadOnlyList<DerivedArtifactManifestEntry> ActiveSelections =>
        Selections.Where(entry => entry.ProvenanceStatus == DerivedArtifactProvenanceStatus.Trusted).ToArray();

    public SelectionProvenanceManifest UpsertActive(DerivedArtifactManifestEntry entry)
    {
        var next = Selections
            .Where(existing =>
                !string.Equals(existing.ArtifactKind, entry.ArtifactKind, StringComparison.Ordinal) ||
                !string.Equals(existing.ArtifactIdentity, entry.ArtifactIdentity, StringComparison.Ordinal))
            .Select(existing =>
                existing.ProvenanceStatus == DerivedArtifactProvenanceStatus.Trusted &&
                string.Equals(existing.ArtifactKind, entry.ArtifactKind, StringComparison.Ordinal)
                    ? existing.Supersede([DerivedArtifactStaleReason.Superseded])
                    : existing)
            .Append(entry)
            .OrderBy(existing => existing.ArtifactKind, StringComparer.Ordinal)
            .ThenBy(existing => existing.GeneratedAt)
            .ThenBy(existing => existing.ArtifactIdentity, StringComparer.Ordinal)
            .ToArray();

        return this with
        {
            SchemaVersion = CurrentSchemaVersion,
            Selections = next,
        };
    }

    public SelectionProvenanceManifest SupersedeActive(IReadOnlyList<DerivedArtifactStaleReason> reasons)
    {
        var next = Selections
            .Select(entry =>
                entry.ProvenanceStatus == DerivedArtifactProvenanceStatus.Trusted
                    ? entry.Supersede(reasons)
                    : entry)
            .OrderBy(existing => existing.ArtifactKind, StringComparer.Ordinal)
            .ThenBy(existing => existing.GeneratedAt)
            .ThenBy(existing => existing.ArtifactIdentity, StringComparer.Ordinal)
            .ToArray();

        return this with
        {
            SchemaVersion = CurrentSchemaVersion,
            Selections = next,
        };
    }
}
