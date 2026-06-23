namespace CommandCenter.Decisions.Models;

public sealed record DecisionOptionGenerationResult(
    IReadOnlyList<DecisionOption> Options,
    IReadOnlyList<DecisionOptionRelationship> Relationships,
    DecisionGenerationDiagnostics Diagnostics);
