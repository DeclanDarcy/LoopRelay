using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record DerivedArtifactManifestEntry(
    string ArtifactKind,
    string ArtifactIdentity,
    string ArtifactPath,
    string Generator,
    string ArtifactHash,
    DateTimeOffset GeneratedAt,
    DerivedArtifactProvenanceStatus ProvenanceStatus,
    IReadOnlyList<DerivedArtifactCausalInput> CausalInputs,
    DerivedArtifactFreshnessStatus FreshnessStatus,
    IReadOnlyList<DerivedArtifactStaleReason> FreshnessReasons)
{
    public bool IsActiveTrusted => ProvenanceStatus == DerivedArtifactProvenanceStatus.Trusted;

    public static DerivedArtifactManifestEntry FromTrustedProvenance(
        DerivedArtifactProvenance provenance,
        string artifactPath,
        string artifactHash,
        DateTimeOffset generatedAt) =>
        new(
            provenance.ArtifactKind,
            provenance.ArtifactIdentity,
            artifactPath,
            provenance.Generator,
            artifactHash,
            generatedAt,
            DerivedArtifactProvenanceStatus.Trusted,
            provenance.CausalInputs,
            DerivedArtifactFreshnessStatus.Fresh,
            []);

    public DerivedArtifactManifestEntry Supersede(IReadOnlyList<DerivedArtifactStaleReason> reasons) =>
        this with
        {
            ProvenanceStatus = DerivedArtifactProvenanceStatus.Superseded,
            FreshnessStatus = DerivedArtifactFreshnessStatus.Stale,
            FreshnessReasons = reasons.Count == 0 ? [DerivedArtifactStaleReason.Superseded] : reasons,
        };

    public DerivedArtifactManifestEntry WithFreshness(DerivedArtifactFreshness freshness) =>
        this with
        {
            FreshnessStatus = freshness.Status,
            FreshnessReasons = freshness.Reasons,
        };
}
