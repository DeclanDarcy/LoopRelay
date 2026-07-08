namespace LoopRelay.Infrastructure.Diagnostics;

public sealed record RuntimeDiagnostic(
    string Id,
    RuntimeDiagnosticSeverity Severity,
    string Message);
