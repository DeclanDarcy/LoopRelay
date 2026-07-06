using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record Decision(
    DecisionId Id,
    DecisionState State,
    DecisionClassification Classification,
    string Title,
    string Context,
    DecisionMetadata Metadata,
    DecisionResolution? Resolution,
    IReadOnlyList<DecisionRelationship> Relationships,
    IReadOnlyList<DecisionEvidence> Evidence,
    IReadOnlyList<DecisionHistoryEntry> History);
