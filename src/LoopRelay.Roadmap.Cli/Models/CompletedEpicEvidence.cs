using System.Text;

namespace LoopRelay.Roadmap.Cli;

internal sealed record CompletedEpicEvidence(
    string Path,
    string? Title,
    string? EpicId,
    string EvidenceQuality,
    string RenderedContent);
