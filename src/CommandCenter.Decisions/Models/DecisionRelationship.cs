using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionRelationship(
    DecisionId SourceDecisionId,
    DecisionId TargetDecisionId,
    DecisionRelationshipType Type,
    string? Rationale = null);
