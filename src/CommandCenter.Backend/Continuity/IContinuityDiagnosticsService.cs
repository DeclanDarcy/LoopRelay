namespace CommandCenter.Backend.Continuity;

public interface IContinuityDiagnosticsService
{
    Task<ContinuityDiagnostics> GetDiagnosticsAsync(Guid repositoryId);
}
