namespace CommandCenter.Decisions.Primitives;

public enum DecisionOptionValidationIssueType
{
    MissingTitle,
    MissingDescription,
    MissingEvidence,
    Duplicate,
    NonActionable,
    EvidenceUnrelated
}
