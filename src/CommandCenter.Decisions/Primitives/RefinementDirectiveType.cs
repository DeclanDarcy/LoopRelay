namespace CommandCenter.Decisions.Primitives;

public enum RefinementDirectiveType
{
    AddConstraint,
    RemoveConstraint,
    IncreasePriority,
    DecreasePriority,
    ExploreAlternative,
    ReevaluateRisk,
    ReevaluateCost,
    ReevaluateRecommendation,
    ClarifyGoal
}
