using LoopRelay.Continuity.Models;

namespace LoopRelay.Continuity.Abstractions;

public interface IContinuityDiagnosticsService
{
    Task<ContinuityDiagnostics> GetDiagnosticsAsync(Guid repositoryId);
}
