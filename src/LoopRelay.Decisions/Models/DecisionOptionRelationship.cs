using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionOptionRelationship(
    string SourceOptionId,
    string TargetOptionId,
    DecisionOptionRelationshipType Type,
    string Rationale,
    IReadOnlyList<DecisionEvidence> Evidence);
