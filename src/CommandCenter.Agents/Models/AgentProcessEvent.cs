namespace CommandCenter.Agents.Models;

public sealed record AgentProcessEvent(
    Guid EventId,
    int ProcessId,
    long Sequence,
    DateTimeOffset OccurredAt,
    AgentProcessEventKind Kind,
    AgentProcessState State,
    int? ExitCode = null,
    AgentProcessOutputStream? OutputStream = null,
    string? Content = null,
    string? DiagnosticCode = null,
    string? Message = null);
