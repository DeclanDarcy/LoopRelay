namespace CommandCenter.Reasoning.Models;

public enum ReasoningEventFamily
{
    Hypothesis,
    Alternative,
    Contradiction,
    Direction,
    DecisionEvolution,
    AssumptionEvolution,
    ConstraintEvolution,
    Evidence,
    Thread
}

public enum ReasoningEventType
{
    HypothesisRaised,
    HypothesisSupported,
    HypothesisChallenged,
    HypothesisInvalidated,
    HypothesisRetired,
    AlternativeIntroduced,
    AlternativeCompared,
    AlternativeRejected,
    AlternativeRevisited,
    AlternativeSelected,
    ContradictionIdentified,
    ContradictionInvestigated,
    ContradictionResolved,
    ContradictionAccepted,
    ContradictionRecurred,
    DirectionObserved,
    DirectionReinforced,
    DirectionShifted,
    DirectionAbandoned,
    DecisionSuperseded,
    DecisionReframed,
    DecisionReconsidered,
    AssumptionIntroduced,
    AssumptionChallenged,
    AssumptionInvalidated,
    AssumptionReplaced,
    ConstraintIntroduced,
    ConstraintModified,
    ConstraintRetired,
    EvidenceAdded,
    ThreadStarted,
    ThreadExtended,
    ThreadForked,
    ThreadMerged
}

public enum ReasoningReferenceKind
{
    Decision,
    Proposal,
    ProposalRevision,
    Candidate,
    OperationalContextRevision,
    GovernanceFinding,
    ExecutionProjection,
    ExecutionOutput,
    Handoff,
    Artifact,
    ReasoningEvent,
    ReasoningThread
}

public enum ReasoningThreadTheme
{
    BeliefUnderInvestigation,
    PathConsidered,
    Conflict,
    StrategicMovement,
    DecisionEvolution,
    AssumptionEvolution,
    ConstraintEvolution,
    EvidenceTrail,
    General
}

public enum ReasoningRelationshipType
{
    CausedBy,
    InfluencedBy,
    Supports,
    Challenges,
    Contradicts,
    Supersedes,
    Extends,
    DerivesFrom,
    LeadsTo,
    Replaces,
    Invalidates,
    Resolves,
    Reopens,
    BelongsTo,
    ComparesWith,
    SelectedOver
}
