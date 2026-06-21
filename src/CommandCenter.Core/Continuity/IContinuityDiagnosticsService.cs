namespace CommandCenter.Core.Continuity;

public interface IContinuityDiagnosticsService
{
    Task<ContinuityDiagnostics> GetDiagnosticsAsync(Guid repositoryId);
}
