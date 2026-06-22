namespace CommandCenter.Decisions.Models;

public sealed record DecisionContextDiagnostics(
    IReadOnlyList<DecisionContextSourceDiagnostic> Sources,
    IReadOnlyList<string> Warnings);

public sealed record DecisionContextSourceDiagnostic(
    string Name,
    string RelativePath,
    bool Required,
    DecisionContextSourceStatus Status,
    string? Message = null,
    int ByteCount = 0,
    int CharacterCount = 0,
    string? Fingerprint = null);

public enum DecisionContextSourceStatus
{
    Loaded,
    Missing,
    Warning
}
