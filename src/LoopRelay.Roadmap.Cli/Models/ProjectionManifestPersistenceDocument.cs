namespace LoopRelay.Roadmap.Cli;

internal sealed record ProjectionManifestPersistenceDocument(
    string SchemaVersion,
    IReadOnlyList<ProjectionManifestEntryDto> Entries)
{
    public const string CurrentSchemaVersion = "projection-manifest.v1";

    public static ProjectionManifestPersistenceDocument FromDomain(ProjectionManifest manifest) =>
        new(
            CurrentSchemaVersion,
            manifest.Entries
                .OrderBy(entry => entry.RuntimePromptName, StringComparer.Ordinal)
                .Select(ProjectionManifestEntryDto.FromDomain)
                .ToArray());

    public ProjectionManifest ToDomain() =>
        new(Entries.Select(entry => entry.ToDomain()).OrderBy(entry => entry.RuntimePromptName, StringComparer.Ordinal).ToArray());

    public static IReadOnlyList<string> Validate(ProjectionManifestPersistenceDocument document)
    {
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ProjectionManifestEntryDto entry in document.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.RuntimePromptName))
            {
                errors.Add("Projection manifest entries must include a runtime prompt name.");
            }

            if (!seen.Add(entry.RuntimePromptName))
            {
                errors.Add($"Projection manifest contains duplicate runtime prompt `{entry.RuntimePromptName}`.");
            }

            if (string.IsNullOrWhiteSpace(entry.ProjectionPath))
            {
                errors.Add($"Projection manifest entry `{entry.RuntimePromptName}` must include a projection path.");
            }

            if (string.IsNullOrWhiteSpace(entry.ProjectionHash))
            {
                errors.Add($"Projection manifest entry `{entry.RuntimePromptName}` must include a projection hash.");
            }
        }

        return errors;
    }
}
