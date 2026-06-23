using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionOptionValidationIssue(
    DecisionOptionValidationIssueType Type,
    string Message);
