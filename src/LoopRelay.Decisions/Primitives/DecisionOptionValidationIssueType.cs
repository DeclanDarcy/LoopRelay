namespace LoopRelay.Decisions.Primitives;

public enum DecisionOptionValidationIssueType
{
    MissingTitle,
    MissingDescription,
    MissingEvidence,
    Duplicate,
    NonActionable,
    EvidenceUnrelated
}
