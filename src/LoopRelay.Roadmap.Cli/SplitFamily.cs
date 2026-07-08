namespace LoopRelay.Roadmap.Cli;

internal sealed record SplitFamily(
    string FamilyId,
    string Proposal,
    IReadOnlyList<string> ChildEpicPaths,
    IReadOnlyList<string> DependencyOrder,
    string SelectedChildPath,
    string SelectedChildRationale,
    DateTimeOffset CreatedAt);
