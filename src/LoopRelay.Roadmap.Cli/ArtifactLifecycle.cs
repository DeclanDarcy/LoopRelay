namespace LoopRelay.Roadmap.Cli;

internal enum ArtifactLifecycleState
{
    Missing,
    Draft,
    Ready,
    Executing,
    Completed,
    Archived,
    Superseded,
    Blocked,
}

internal sealed record ArtifactLifecycleEntry(
    string Path,
    ArtifactLifecycleState State,
    DateTimeOffset UpdatedAt,
    string Notes);

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
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ArtifactLifecycleEntryDto entry in document.Entries)
        {
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

internal sealed record ArtifactLifecycleEntryDto(
    string Path,
    ArtifactLifecycleState State,
    DateTimeOffset UpdatedAt,
    string Notes)
{
    public static ArtifactLifecycleEntryDto FromDomain(ArtifactLifecycleEntry entry) =>
        new(entry.Path, entry.State, entry.UpdatedAt, entry.Notes);

    public ArtifactLifecycleEntry ToDomain() => new(Path, State, UpdatedAt, Notes);
}
