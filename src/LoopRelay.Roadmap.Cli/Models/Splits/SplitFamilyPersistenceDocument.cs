namespace LoopRelay.Roadmap.Cli.Models.Splits;

internal sealed record SplitFamilyPersistenceDocument(
    string SchemaVersion,
    SplitFamilyDto Family)
{
    public const string CurrentSchemaVersion = "split-family.v1";

    public static SplitFamilyPersistenceDocument FromDomain(SplitFamily family) =>
        new(CurrentSchemaVersion, SplitFamilyDto.FromDomain(family));

    public SplitFamily ToDomain() => Family.ToDomain();

    public static IReadOnlyList<string> Validate(SplitFamilyPersistenceDocument document)
    {
        var errors = new List<string>();
        if (document.Family is null)
        {
            errors.Add("Split family document must include a family.");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(document.Family.FamilyId))
        {
            errors.Add("Split family must include a family ID.");
        }

        if (document.Family.ChildEpicPaths is null)
        {
            errors.Add("Split family must include child epic paths.");
        }
        else if (document.Family.ChildEpicPaths.Count == 0)
        {
            errors.Add("Split family must include child epic paths.");
        }

        if (document.Family.DependencyOrder is null)
        {
            errors.Add("Split family must include a dependency order.");
        }

        if (string.IsNullOrWhiteSpace(document.Family.SelectedChildPath))
        {
            errors.Add("Split family must include a selected child path.");
        }
        else if (document.Family.ChildEpicPaths is not null &&
            !document.Family.ChildEpicPaths.Contains(document.Family.SelectedChildPath, StringComparer.Ordinal))
        {
            errors.Add($"Selected child `{document.Family.SelectedChildPath}` must be present in child epic paths.");
        }

        return errors;
    }
}
