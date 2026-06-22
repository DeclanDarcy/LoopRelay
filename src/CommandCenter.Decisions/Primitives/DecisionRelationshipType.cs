namespace CommandCenter.Decisions.Primitives;

public enum DecisionRelationshipType
{
    DependsOn,
    Supersedes,
    ConflictsWith,
    Supports,
    Constrains,
    DerivedFrom,
    RelatedTo
}
