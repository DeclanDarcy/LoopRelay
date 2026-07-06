namespace LoopRelay.Roadmap.Cli;

internal sealed record ProjectionManifest(IReadOnlyList<ProjectionManifestEntry> Entries)
{
    public static ProjectionManifest Empty { get; } = new([]);

    public ProjectionManifestEntry? Find(string runtimePromptName) =>
        Entries.FirstOrDefault(entry => string.Equals(entry.RuntimePromptName, runtimePromptName, StringComparison.Ordinal));

    public ProjectionManifest Upsert(ProjectionManifestEntry entry)
    {
        var next = Entries
            .Where(existing => !string.Equals(existing.RuntimePromptName, entry.RuntimePromptName, StringComparison.Ordinal))
            .Append(entry)
            .OrderBy(existing => existing.RuntimePromptName, StringComparer.Ordinal)
            .ToList();
        return new ProjectionManifest(next);
    }
}

internal sealed record ProjectionManifestEntry(
    string RuntimePromptName,
    string ProjectionPromptName,
    string ProjectionPath,
    string ProjectionPromptSourceHash,
    IReadOnlyList<string> ProjectContextFiles,
    string ProjectContextHash,
    string ProjectionHash,
    DateTimeOffset GeneratedAt,
    ProjectionValidationStatus ValidationStatus,
    ProjectionStaleStatus StaleStatus,
    string? LastValidationError,
    ProjectionProvenanceStatus ProvenanceStatus = ProjectionProvenanceStatus.Unknown,
    string ProjectionIdentity = "",
    string ProjectionPromptType = "",
    IReadOnlyList<ProjectionCausalInput>? CausalInputs = null,
    IReadOnlyList<ProjectionStaleReason>? StaleReasons = null)
{
    public string EffectiveProjectionIdentity =>
        string.IsNullOrWhiteSpace(ProjectionIdentity) ? RuntimePromptName : ProjectionIdentity;

    public IReadOnlyList<ProjectionCausalInput> EffectiveCausalInputs => CausalInputs ?? [];

    public IReadOnlyList<ProjectionStaleReason> EffectiveStaleReasons => StaleReasons ?? [];

    public static ProjectionManifestEntry FromTrustedProvenance(
        ProjectionProvenance provenance,
        string projectionHash,
        DateTimeOffset generatedAt,
        ProjectionValidationStatus validationStatus,
        ProjectionFreshness freshness,
        string? lastValidationError) =>
        new(
            provenance.RuntimePromptName,
            provenance.Prompt.PromptName,
            provenance.ProjectionPath,
            provenance.Prompt.SourceHash,
            provenance.ProjectContextFiles,
            provenance.ProjectContextHash,
            projectionHash,
            generatedAt,
            validationStatus,
            freshness.Status,
            lastValidationError,
            ProjectionProvenanceStatus.Trusted,
            provenance.ProjectionIdentity,
            provenance.Prompt.PromptType,
            provenance.CausalInputs,
            freshness.Reasons);

    public ProjectionManifestEntry WithFreshness(ProjectionFreshness freshness, string projectionHash, ProjectionValidationStatus validationStatus, string? lastValidationError) =>
        this with
        {
            ProjectionHash = projectionHash,
            ValidationStatus = validationStatus,
            StaleStatus = freshness.Status,
            LastValidationError = lastValidationError,
            StaleReasons = freshness.Reasons,
        };

}

internal enum ProjectionValidationStatus
{
    Unknown,
    Valid,
    Invalid,
}

internal enum ProjectionStaleStatus
{
    Fresh,
    Stale,
    UnknownProvenance,
}

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

internal sealed record ProjectionManifestEntryDto(
    string RuntimePromptName,
    string ProjectionPromptName,
    string ProjectionPath,
    string ProjectionPromptSourceHash,
    IReadOnlyList<string> ProjectContextFiles,
    string ProjectContextHash,
    string ProjectionHash,
    DateTimeOffset GeneratedAt,
    ProjectionValidationStatus ValidationStatus,
    ProjectionStaleStatus StaleStatus,
    string? LastValidationError,
    ProjectionProvenanceStatus ProvenanceStatus,
    string ProjectionIdentity,
    string ProjectionPromptType,
    IReadOnlyList<ProjectionCausalInputDto> CausalInputs,
    IReadOnlyList<ProjectionStaleReason> StaleReasons)
{
    public static ProjectionManifestEntryDto FromDomain(ProjectionManifestEntry entry) =>
        new(
            entry.RuntimePromptName,
            entry.ProjectionPromptName,
            entry.ProjectionPath,
            entry.ProjectionPromptSourceHash,
            entry.ProjectContextFiles.ToArray(),
            entry.ProjectContextHash,
            entry.ProjectionHash,
            entry.GeneratedAt,
            entry.ValidationStatus,
            entry.StaleStatus,
            entry.LastValidationError,
            entry.ProvenanceStatus,
            entry.EffectiveProjectionIdentity,
            entry.ProjectionPromptType,
            entry.EffectiveCausalInputs.Select(ProjectionCausalInputDto.FromDomain).ToArray(),
            entry.EffectiveStaleReasons.ToArray());

    public ProjectionManifestEntry ToDomain() =>
        new(
            RuntimePromptName,
            ProjectionPromptName,
            ProjectionPath,
            ProjectionPromptSourceHash,
            ProjectContextFiles.ToArray(),
            ProjectContextHash,
            ProjectionHash,
            GeneratedAt,
            ValidationStatus,
            StaleStatus,
            LastValidationError,
            ProvenanceStatus,
            ProjectionIdentity,
            ProjectionPromptType,
            CausalInputs.Select(input => input.ToDomain()).ToArray(),
            StaleReasons.ToArray());
}

internal sealed record ProjectionCausalInputDto(string Kind, string Identity, string Version)
{
    public static ProjectionCausalInputDto FromDomain(ProjectionCausalInput input) =>
        new(input.Kind, input.Identity, input.Version);

    public ProjectionCausalInput ToDomain() => new(Kind, Identity, Version);
}
