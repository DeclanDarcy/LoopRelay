using System.Text.Json;
using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed class ProjectionManifestStore(RoadmapArtifacts artifacts)
{
    private readonly StructuredDocumentStore<ProjectionManifestPersistenceDocument> structuredStore = new(
        artifacts,
        RoadmapArtifactPaths.ProjectionsManifestJson,
        ProjectionManifestPersistenceDocument.CurrentSchemaVersion,
        document => document.SchemaVersion,
        ProjectionManifestPersistenceDocument.Validate);

    public async Task<ProjectionManifest> LoadAsync()
    {
        ProjectionManifestPersistenceDocument? structured = await structuredStore.LoadAsync();
        if (structured is not null)
        {
            return structured.ToDomain();
        }

        string? content = await artifacts.ReadAsync(RoadmapArtifactPaths.ProjectionsManifest);
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
            throw new RoadmapStepException($"Legacy projection manifest cannot be migrated: {exception.Message}");
        }

        await SaveAsync(migrated);
        return migrated;
    }

    public async Task SaveAsync(ProjectionManifest manifest)
    {
        ProjectionManifestPersistenceDocument persisted = ProjectionManifestPersistenceDocument.FromDomain(manifest);
        await structuredStore.SaveAsync(persisted);
    }

    public async Task UpsertAsync(ProjectionManifestEntry entry)
    {
        ProjectionManifest manifest = await LoadAsync();
        await SaveAsync(manifest.Upsert(entry));
    }

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
            throw new RoadmapStepException($"Legacy projection manifest cannot be migrated because validation failed: {string.Join("; ", errors)}");
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
