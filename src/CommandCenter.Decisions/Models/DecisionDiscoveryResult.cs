namespace CommandCenter.Decisions.Models;

public sealed record DecisionDiscoveryResult(
    IReadOnlyList<DecisionCandidate> Candidates,
    DecisionDiscoveryDiagnostics Diagnostics);
