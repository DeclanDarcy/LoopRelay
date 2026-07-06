using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionRelationship(
    DecisionId SourceDecisionId,
    DecisionId TargetDecisionId,
    DecisionRelationshipType Type,
    string? Rationale = null);
