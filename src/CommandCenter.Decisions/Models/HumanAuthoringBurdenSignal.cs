using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record HumanAuthoringBurdenSignal(
    string Id,
    Guid RepositoryId,
    string DecisionId,
    HumanAuthoringBurden Burden,
    string SourceKind,
    string Summary,
    IReadOnlyList<DecisionSourceReference> Sources);
