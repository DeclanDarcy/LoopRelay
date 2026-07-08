namespace LoopRelay.Projections;

public sealed record ProjectionManifestEntry(
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
}
