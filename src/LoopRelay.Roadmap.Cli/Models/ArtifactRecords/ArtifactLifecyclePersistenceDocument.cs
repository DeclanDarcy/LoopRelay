namespace LoopRelay.Roadmap.Cli.Models.ArtifactRecords;

internal sealed record ArtifactLifecyclePersistenceDocument(
    string SchemaVersion,
    IReadOnlyList<ArtifactLifecycleEntryDto> Entries)
{
    public const string CurrentSchemaVersion = "artifact-lifecycle.v1";

    public static ArtifactLifecyclePersistenceDocument FromDomain(IReadOnlyList<ArtifactLifecycleEntry> entries) =>
        new(
            CurrentSchemaVersion,
            entries
                .OrderBy(entry => entry.Path, StringComparer.Ordinal)
                .Select(ArtifactLifecycleEntryDto.FromDomain)
                .ToArray());

    public IReadOnlyList<ArtifactLifecycleEntry> ToDomain() =>
        Entries.Select(entry => entry.ToDomain()).OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<string> Validate(ArtifactLifecyclePersistenceDocument document)
    {
        var errors = new List<string>();
        if (document.Entries is null)
        {
            errors.Add("Artifact lifecycle entries are required.");
            return errors;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ArtifactLifecycleEntryDto entry in document.Entries)
        {
            if (entry is null)
            {
                errors.Add("Artifact lifecycle entries cannot be null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                errors.Add("Artifact lifecycle entries must include a path.");
            }

            if (!seen.Add(entry.Path))
            {
                errors.Add($"Artifact lifecycle contains duplicate path `{entry.Path}`.");
            }
        }

        return errors;
    }
}
