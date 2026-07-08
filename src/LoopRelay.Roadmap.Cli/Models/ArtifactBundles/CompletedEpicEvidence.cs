namespace LoopRelay.Roadmap.Cli.Models.ArtifactBundles;

internal sealed record CompletedEpicEvidence(
    string Path,
    string? Title,
    string? EpicId,
    string EvidenceQuality,
    string RenderedContent);
