namespace LoopRelay.Completion.Models;

public sealed record CompletedEpicEvidence(
    string Path,
    string? Title,
    string? EpicId,
    string EvidenceQuality,
    string RenderedContent);
