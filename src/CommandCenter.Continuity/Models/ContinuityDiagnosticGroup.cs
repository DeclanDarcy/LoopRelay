namespace CommandCenter.Continuity.Models;

public sealed class ContinuityDiagnosticGroup
{
    public string Category { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<string> Diagnostics { get; init; } = [];
}
