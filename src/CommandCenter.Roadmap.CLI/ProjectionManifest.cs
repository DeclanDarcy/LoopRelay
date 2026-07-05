namespace CommandCenter.Roadmap.Cli;

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
