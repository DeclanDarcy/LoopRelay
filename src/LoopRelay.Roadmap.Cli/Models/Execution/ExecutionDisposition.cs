using LoopRelay.Roadmap.Cli.Primitives.Execution;
using LoopRelay.Roadmap.Cli.Services.Execution;

namespace LoopRelay.Roadmap.Cli.Models.Execution;

internal sealed record ExecutionDisposition(
    ExecutionDispositionStatus Status,
    string Confidence,
    string EvidenceSummary,
    ExecutionDispositionCommand NextStep)
{
    public string StatusText => ExecutionDispositionProtocol.StatusText(Status);

    public string NextStepText => ExecutionDispositionProtocol.CommandText(NextStep);
}
