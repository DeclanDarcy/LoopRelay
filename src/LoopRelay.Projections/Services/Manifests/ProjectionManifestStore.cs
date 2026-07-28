using System.Text.Json;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.Manifests;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Models.Provenance;
using LoopRelay.Projections.Primitives;
using LoopRelay.Projections.Services.ProjectionArtifacts;
using LoopRelay.Projections.Services.Prompts;

namespace LoopRelay.Projections.Services.Manifests;

public sealed class ProjectionManifestStore(ProjectionArtifacts.ProjectionArtifacts _artifacts)
{
    private readonly StructuredJsonDocumentStore<ProjectionManifestPersistenceDocument> _structuredStore = new(
        _artifacts,
        ProjectionArtifactPaths.ProjectionsManifestJson,
        ProjectionManifestPersistenceDocument.CurrentSchemaVersion,
        document => document.SchemaVersion,
        ProjectionManifestPersistenceDocument.Validate);

    public async Task<ProjectionManifest> LoadAsync()
    {
        ProjectionManifestPersistenceDocument? structured = await _structuredStore.LoadAsync();
        if (structured is not null)
        {
            return structured.ToDomain();
        }

        string? content = await _artifacts.ReadAsync(ProjectionArtifactPaths.ProjectionsManifest);
        if (string.IsNullOrWhiteSpace(content))
        {
            return ProjectionManifest.Empty;
        }

        ProjectionManifest migrated;
        try
        {
            migrated = ParseLegacyMarkdown(content);
        }
        catch (MarkdownParseException exception)
        {
            throw new ProjectionException($"Legacy projection manifest cannot be migrated: {exception.Message}");
        }

        await SaveAsync(migrated);
        return migrated;
    }

    public async Task SaveAsync(ProjectionManifest manifest)
    {
        ProjectionManifestPersistenceDocument persisted = ProjectionManifestPersistenceDocument.FromDomain(manifest);
        await _structuredStore.SaveAsync(persisted);
    }

    public async Task UpsertAsync(ProjectionManifestEntry entry)
    {
        ProjectionManifest manifest = await LoadAsync();
        await UpsertAsync(manifest, entry, CancellationToken.None);
    }

    /// <summary>
    /// Upserts <paramref name="entry"/> into the caller-supplied <paramref name="loaded"/> manifest instead of
    /// re-loading it from disk, and skips the physical <see cref="SaveAsync"/> write entirely when the resulting
    /// manifest is unchanged from <paramref name="loaded"/>. This is what makes an unchanged steady-state loop
    /// iteration produce zero manifest writes: <see cref="Models.Manifests.ProjectionManifestEntry.FromTrustedProvenance"/>
    /// preserves <c>GeneratedAt</c> for non-regenerated entries, so a repeat ensure over unchanged inputs upserts
    /// an entry that is structurally identical to the one already on disk.
    /// </summary>
    /// <remarks>
    /// Equality here is NOT record `==`/`Equals`: <see cref="ProjectionManifest.Entries"/> and several
    /// <see cref="ProjectionManifestEntry"/> fields (<c>ProjectContextFiles</c>, <c>CausalInputs</c>,
    /// <c>StaleReasons</c>) are collections, and generated record equality compares collection-typed fields by
    /// reference (via <see cref="EqualityComparer{T}.Default"/> on the interface type), not by content. Two
    /// structurally identical manifests built from distinct <c>List</c>/array instances would therefore compare
    /// UNEQUAL under record equality even though they represent the same state — which would defeat this gate by
    /// always taking the "changed" branch. <see cref="ManifestEquals"/> performs the real structural comparison.
    /// </remarks>
    public async Task UpsertAsync(
        ProjectionManifest loaded,
        ProjectionManifestEntry entry,
        CancellationToken cancellationToken = default)
    {
        ProjectionManifest updated = loaded.Upsert(entry);
        if (ManifestEquals(loaded, updated))
        {
            return;
        }

        await SaveAsync(updated);
    }

    /// <summary>
    /// Structural equality for <see cref="ProjectionManifest"/> that correctly compares collection-typed fields by
    /// content rather than by reference (see the remarks on <see cref="UpsertAsync(ProjectionManifest,ProjectionManifestEntry,CancellationToken)"/>
    /// for why record `==` is not sufficient here). Entries are compared positionally: both
    /// <see cref="ProjectionManifest.Upsert"/> and <see cref="ProjectionManifestPersistenceDocument.ToDomain"/>
    /// always order entries by <c>RuntimePromptName</c>, so two manifests describing the same state are always in
    /// the same order.
    /// </summary>
    public static bool ManifestEquals(ProjectionManifest left, ProjectionManifest right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Entries.Count != right.Entries.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Entries.Count; index++)
        {
            if (!EntryEquals(left.Entries[index], right.Entries[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool EntryEquals(ProjectionManifestEntry left, ProjectionManifestEntry right) =>
        string.Equals(left.RuntimePromptName, right.RuntimePromptName, StringComparison.Ordinal)
        && string.Equals(left.ProjectionPromptName, right.ProjectionPromptName, StringComparison.Ordinal)
        && string.Equals(left.ProjectionPath, right.ProjectionPath, StringComparison.Ordinal)
        && string.Equals(left.ProjectionPromptSourceHash, right.ProjectionPromptSourceHash, StringComparison.Ordinal)
        && left.ProjectContextFiles.SequenceEqual(right.ProjectContextFiles, StringComparer.Ordinal)
        && string.Equals(left.ProjectContextHash, right.ProjectContextHash, StringComparison.Ordinal)
        && string.Equals(left.ProjectionHash, right.ProjectionHash, StringComparison.Ordinal)
        && left.GeneratedAt.Equals(right.GeneratedAt)
        && left.ValidationStatus == right.ValidationStatus
        && left.StaleStatus == right.StaleStatus
        && string.Equals(left.LastValidationError, right.LastValidationError, StringComparison.Ordinal)
        && left.ProvenanceStatus == right.ProvenanceStatus
        && string.Equals(left.ProjectionIdentity, right.ProjectionIdentity, StringComparison.Ordinal)
        && string.Equals(left.ProjectionPromptType, right.ProjectionPromptType, StringComparison.Ordinal)
        && left.EffectiveCausalInputs.SequenceEqual(right.EffectiveCausalInputs)
        && left.EffectiveStaleReasons.SequenceEqual(right.EffectiveStaleReasons);

    private static ProjectionManifest ParseLegacyMarkdown(string content)
    {
        MarkdownTableParser.ValidateTables(content);
        var entries = new List<ProjectionManifestEntry>();
        foreach (IReadOnlyDictionary<string, string> row in MarkdownTableParser.ParseTablesStrict(content))
        {
            if (row.ContainsKey("Projection Identity"))
            {
                entries.Add(new ProjectionManifestEntry(
                    Field(row, "Runtime Prompt"),
                    Field(row, "Projection Prompt"),
                    Field(row, "Path"),
                    Field(row, "Projection Prompt Source Hash"),
                    ParseList(Field(row, "Project Context Files")),
                    Field(row, "Project Context Hash"),
                    Field(row, "Projection Hash"),
                    ParseGeneratedAt(Field(row, "Generated At")),
                    ParseValidationStatus(Field(row, "Validation Status")),
                    ParseStaleStatus(Field(row, "Stale Status")),
                    NullIfNone(Field(row, "Last Validation Error")),
                    ParseProvenanceStatus(Field(row, "Provenance Status")),
                    Field(row, "Projection Identity"),
                    Field(row, "Projection Prompt Type"),
                    ParseCausalInputs(Field(row, "Causal Inputs")),
                    ParseStaleReasons(Field(row, "Stale Reasons"))));
                continue;
            }

            if (!row.ContainsKey("Runtime Prompt") || !row.ContainsKey("Projection Prompt") || !row.ContainsKey("Path"))
            {
                continue;
            }

            entries.Add(new ProjectionManifestEntry(
                Field(row, "Runtime Prompt"),
                Field(row, "Projection Prompt"),
                Field(row, "Path"),
                Field(row, "Projection Prompt Source Hash"),
                ParseList(Field(row, "Project Context Files")),
                Field(row, "Project Context Hash"),
                Field(row, "Projection Hash"),
                ParseGeneratedAt(Field(row, "Generated At")),
                ParseValidationStatus(Field(row, "Validation Status")),
                ProjectionStaleStatus.UnknownProvenance,
                NullIfNone(Field(row, "Last Validation Error")),
                ProjectionProvenanceStatus.Unknown,
                Field(row, "Runtime Prompt"),
                string.Empty,
                [],
                [ProjectionStaleReason.UnknownProvenance]));
        }

        ProjectionManifest manifest = new(entries.OrderBy(entry => entry.RuntimePromptName, StringComparer.Ordinal).ToArray());
        ProjectionManifestPersistenceDocument persisted = ProjectionManifestPersistenceDocument.FromDomain(manifest);
        IReadOnlyList<string> errors = ProjectionManifestPersistenceDocument.Validate(persisted);
        if (errors.Count > 0)
        {
            throw new ProjectionException($"Legacy projection manifest cannot be migrated because validation failed: {string.Join("; ", errors)}");
        }

        return manifest;
    }

    private static string Field(IReadOnlyDictionary<string, string> row, string field) =>
        row.TryGetValue(field, out string? value) ? value : string.Empty;

    private static string? NullIfNone(string value) =>
        string.Equals(value, "None", StringComparison.Ordinal) ? null : value;

    private static IReadOnlyList<string> ParseList(string cell) =>
        cell.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static DateTimeOffset ParseGeneratedAt(string cell) =>
        DateTimeOffset.TryParse(cell, out DateTimeOffset generatedAt) ? generatedAt : DateTimeOffset.MinValue;

    private static ProjectionValidationStatus ParseValidationStatus(string cell) =>
        Enum.TryParse(cell, out ProjectionValidationStatus validationStatus) ? validationStatus : ProjectionValidationStatus.Unknown;

    private static ProjectionStaleStatus ParseStaleStatus(string cell) =>
        Enum.TryParse(cell, out ProjectionStaleStatus staleStatus) ? staleStatus : ProjectionStaleStatus.UnknownProvenance;

    private static ProjectionProvenanceStatus ParseProvenanceStatus(string cell) =>
        Enum.TryParse(cell, out ProjectionProvenanceStatus provenanceStatus) ? provenanceStatus : ProjectionProvenanceStatus.Unknown;

    private static IReadOnlyList<ProjectionCausalInput> ParseCausalInputs(string cell)
    {
        if (string.IsNullOrWhiteSpace(cell) || string.Equals(cell, "None", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<ProjectionCausalInput>>(cell) ?? [];
        }
        catch (JsonException exception)
        {
            throw new MarkdownParseException($"Projection manifest causal inputs are not valid JSON: {exception.Message}");
        }
    }

    private static IReadOnlyList<ProjectionStaleReason> ParseStaleReasons(string cell)
    {
        if (string.IsNullOrWhiteSpace(cell) || string.Equals(cell, "None", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var reasons = new List<ProjectionStaleReason>();
        foreach (string value in cell.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse(value, out ProjectionStaleReason reason))
            {
                reasons.Add(reason);
                continue;
            }

            throw new MarkdownParseException($"Projection manifest contains unknown stale reason `{value}`.");
        }

        return reasons;
    }
}
