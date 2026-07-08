using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

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
