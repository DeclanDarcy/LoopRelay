namespace CommandCenter.Decisions.Models;

public sealed record DecisionDiscoveryDiagnostics(
    string ContextFingerprint,
    int ContextItemCount,
    int SignalCount,
    int CreatedCandidateCount,
    int SuppressedDuplicateCount,
    IReadOnlyList<string> Warnings);
