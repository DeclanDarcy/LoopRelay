using LoopRelay.Infrastructure.Primitives.Diagnostics;

namespace LoopRelay.Infrastructure.Models.Diagnostics;

public sealed record RuntimeDiagnostic(
    string Id,
    RuntimeDiagnosticSeverity Severity,
    string Message);
