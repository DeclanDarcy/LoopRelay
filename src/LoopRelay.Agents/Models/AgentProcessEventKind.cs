namespace LoopRelay.Agents.Models;

public enum AgentProcessEventKind
{
    ProcessStarted,
    ProcessOutput,
    ProcessCompleted,
    ProcessFailed,
    ProcessCancelled,
    ProcessDisposed,
    ProcessDiagnostic
}
