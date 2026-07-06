namespace LoopRelay.Decisions.Models;

public sealed record DecisionContext(
    Guid RepositoryId,
    string Fingerprint,
    IReadOnlyList<DecisionContextItem> Items,
    DecisionContextDiagnostics Diagnostics,
    DecisionContextValidationResult Validation);
