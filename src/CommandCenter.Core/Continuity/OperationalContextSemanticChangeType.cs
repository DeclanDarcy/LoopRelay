namespace CommandCenter.Core.Continuity;

public enum OperationalContextSemanticChangeType
{
    SectionAdded,
    SectionRemoved,
    SectionChanged,
    ItemAdded,
    ItemRemoved,
    ItemChanged,
    ConstraintAdded,
    ConstraintRemoved,
    QuestionAdded,
    QuestionRemoved,
    RiskAdded,
    RiskRemoved,
    DecisionAdded,
    DecisionRemoved,
    ImportantDecisionIntroduced,
    DecisionRetired,
    RationaleChanged,
    RationaleLostWarning,
    OpenDecisionPreserved,
    OpenDecisionResolved,
    PreservationWarning
}
