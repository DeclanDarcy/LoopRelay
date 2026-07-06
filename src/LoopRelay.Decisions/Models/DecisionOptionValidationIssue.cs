using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionOptionValidationIssue(
    DecisionOptionValidationIssueType Type,
    string Message);
