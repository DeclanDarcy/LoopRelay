using CommandCenter.Continuity.Models;

namespace CommandCenter.Continuity.Abstractions;

public interface IContinuityDiagnosticsService
{
    Task<ContinuityDiagnostics> GetDiagnosticsAsync(Guid repositoryId);
}
