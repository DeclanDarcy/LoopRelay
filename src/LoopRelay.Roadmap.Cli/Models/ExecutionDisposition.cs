using LoopRelay.Roadmap.Cli.Primitives;
using LoopRelay.Roadmap.Cli.Services;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ExecutionDisposition(
    ExecutionDispositionStatus Status,
    string Confidence,
    string EvidenceSummary,
    ExecutionDispositionCommand NextStep)
{
    public string StatusText => ExecutionDispositionProtocol.StatusText(Status);

    public string NextStepText => ExecutionDispositionProtocol.CommandText(NextStep);
}
