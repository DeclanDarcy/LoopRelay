using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoopRelay.Projections;

public sealed class ProjectionManifestStore(ProjectionArtifacts artifacts)
{
    private readonly StructuredJsonDocumentStore<ProjectionManifestPersistenceDocument> structuredStore = new(
        artifacts,
        ProjectionArtifactPaths.ProjectionsManifestJson,
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

        string? content = await artifacts.ReadAsync(ProjectionArtifactPaths.ProjectionsManifest);
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

internal sealed class StructuredJsonDocumentStore<TDocument>(
    ProjectionArtifacts artifacts,
    string path,
    string expectedSchemaVersion,
    Func<TDocument, string?> getSchemaVersion,
    Func<TDocument, IReadOnlyList<string>> validate)
    where TDocument : class
{
    public async Task<TDocument?> LoadAsync()
    {
        string? content = await artifacts.ReadAsync(path);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        TDocument document;
        try
        {
            document = JsonSerializer.Deserialize<TDocument>(content, ProjectionJson.Options)
                ?? throw new ProjectionException($"Canonical structured persistence is empty: {path}.");
        }
        catch (JsonException exception)
        {
            throw new ProjectionException($"Canonical structured persistence is invalid JSON at {path}: {exception.Message}");
        }

        Validate(document);
        return document;
    }

    public async Task SaveAsync(TDocument document)
    {
        Validate(document);
        string content = JsonSerializer.Serialize(document, ProjectionJson.Options) + Environment.NewLine;
        await artifacts.WriteAsync(path, content);
    }

    private void Validate(TDocument document)
    {
        string? schemaVersion = getSchemaVersion(document);
        if (!string.Equals(schemaVersion, expectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new ProjectionException(
                $"Canonical structured persistence at {path} has unsupported schema version `{schemaVersion ?? "null"}`; expected `{expectedSchemaVersion}`.");
        }

        IReadOnlyList<string> errors = validate(document);
        if (errors.Count > 0)
        {
            throw new ProjectionException(
                $"Canonical structured persistence at {path} failed validation: {string.Join("; ", errors)}");
        }
    }
}

internal static class ProjectionJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

internal static class MarkdownTableParser
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseTablesStrict(string markdown) =>
        ParseTables(markdown, strict: true);

    public static void ValidateTables(string markdown)
    {
        _ = ParseTablesStrict(markdown);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseTables(string markdown, bool strict)
    {
        var tables = new List<IReadOnlyDictionary<string, string>>();
        string[] lines = markdown.Split('\n');

        for (int index = 0; index < lines.Length - 1; index++)
        {
            string headerLine = lines[index].Trim();
            string separatorLine = lines[index + 1].Trim();
            if (!IsTableLine(headerLine) || !IsSeparatorLine(separatorLine))
            {
                continue;
            }

            string[] headers = SplitRow(headerLine);
            index += 2;
            while (index < lines.Length && IsTableLine(lines[index].Trim()))
            {
                string rowLine = lines[index].Trim();
                string[] values = SplitRow(rowLine);
                if (strict && values.Length != headers.Length)
                {
                    throw new MarkdownParseException(
                        $"Malformed Markdown table row has {values.Length} cells but expected {headers.Length}: {rowLine}");
                }

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int column = 0; column < headers.Length; column++)
                {
                    row[headers[column]] = column < values.Length ? values[column] : string.Empty;
                }

                tables.Add(row);
                index++;
            }
        }

        return tables;
    }

    private static bool IsTableLine(string line) => line.StartsWith('|') && line.EndsWith('|');

    private static bool IsSeparatorLine(string line)
    {
        if (!IsTableLine(line))
        {
            return false;
        }

        string[] cells = SplitRow(line);
        return cells.Length > 0 && cells.All(cell => cell.Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).Trim().Length == 0);
    }

    private static string[] SplitRow(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }

        var cells = new List<string>();
        var current = new StringBuilder();
        for (int index = 0; index < trimmed.Length; index++)
        {
            char value = trimmed[index];
            if (value == '\\' && index + 1 < trimmed.Length && (trimmed[index + 1] == '|' || trimmed[index + 1] == '\\'))
            {
                current.Append(trimmed[index + 1]);
                index++;
                continue;
            }

            if (value == '|')
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(value);
        }

        cells.Add(current.ToString().Trim());
        return cells.ToArray();
    }
}

internal sealed class MarkdownParseException(string message) : Exception(message);
