namespace LoopRelay.Roadmap.Cli.Models.Splits;

internal sealed record SplitFamilyDto(
    string FamilyId,
    string Proposal,
    IReadOnlyList<string> ChildEpicPaths,
    IReadOnlyList<string> DependencyOrder,
    string SelectedChildPath,
    string SelectedChildRationale,
    DateTimeOffset CreatedAt)
{
    public static SplitFamilyDto FromDomain(SplitFamily family) =>
        new(
            family.FamilyId,
            family.Proposal,
            family.ChildEpicPaths.ToArray(),
            family.DependencyOrder.ToArray(),
            family.SelectedChildPath,
            family.SelectedChildRationale,
            family.CreatedAt);

    public SplitFamily ToDomain() =>
        new(
            FamilyId,
            Proposal,
            ChildEpicPaths.ToArray(),
            DependencyOrder.ToArray(),
            SelectedChildPath,
            SelectedChildRationale,
            CreatedAt);
}
