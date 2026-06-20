namespace CommandCenter.Backend.Continuity;

public sealed class ContinuityReport
{
    public string ReportId { get; init; } = string.Empty;

    public Guid RepositoryId { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }

    public string RelativePath { get; init; } = string.Empty;

    public ContinuityDiagnostics Diagnostics { get; init; } = new();
}
