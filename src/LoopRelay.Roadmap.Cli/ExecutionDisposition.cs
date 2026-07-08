namespace LoopRelay.Roadmap.Cli;

internal sealed record ExecutionDisposition(
    ExecutionDispositionStatus Status,
    string Confidence,
    string EvidenceSummary,
    ExecutionDispositionCommand NextStep)
{
    public string StatusText => ExecutionDispositionProtocol.StatusText(Status);

    public string NextStepText => ExecutionDispositionProtocol.CommandText(NextStep);
}
