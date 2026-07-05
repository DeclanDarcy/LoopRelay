using System.Text.Json;

namespace CommandCenter.Roadmap.Cli;

internal sealed class ProjectionManifestStore(RoadmapArtifacts artifacts)
{
    public async Task<ProjectionManifest> LoadAsync()
    {
        string? content = await artifacts.ReadAsync(RoadmapArtifactPaths.ProjectionsManifest);
        if (string.IsNullOrWhiteSpace(content))
        {
            return ProjectionManifest.Empty;
        }

        var entries = new List<ProjectionManifestEntry>();
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith('|') || trimmed.StartsWith("|---", StringComparison.Ordinal) ||
                trimmed.Contains("Runtime Prompt", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = trimmed.Trim('|').Split('|').Select(UnescapeCell).ToArray();
            if (cells.Length >= 16)
            {
                entries.Add(new ProjectionManifestEntry(
                    cells[0],
                    cells[2],
                    cells[4],
                    cells[7],
                    ParseList(cells[8]),
                    cells[9],
                    cells[10],
                    ParseGeneratedAt(cells[11]),
                    ParseValidationStatus(cells[12]),
                    ParseStaleStatus(cells[13]),
                    string.Equals(cells[15], "None", StringComparison.Ordinal) ? null : cells[15],
                    ParseProvenanceStatus(cells[5]),
                    cells[1],
                    cells[3],
                    ParseCausalInputs(cells[6]),
                    ParseStaleReasons(cells[14])));
                continue;
            }

            if (cells.Length < 11)
            {
                continue;
            }

            entries.Add(new ProjectionManifestEntry(
                cells[0],
                cells[1],
                cells[2],
                cells[3],
                ParseList(cells[4]),
                cells[5],
                cells[6],
                ParseGeneratedAt(cells[7]),
                ParseValidationStatus(cells[8]),
                ProjectionStaleStatus.UnknownProvenance,
                string.Equals(cells[10], "None", StringComparison.Ordinal) ? null : cells[10],
                ProjectionProvenanceStatus.Unknown,
                cells[0],
                string.Empty,
                [],
                [ProjectionStaleReason.UnknownProvenance]));
        }

        return new ProjectionManifest(entries);
    }

    public async Task SaveAsync(ProjectionManifest manifest)
    {
        await artifacts.WriteAsync(RoadmapArtifactPaths.ProjectionsManifest, Render(manifest));
    }

    public async Task UpsertAsync(ProjectionManifestEntry entry)
    {
        ProjectionManifest manifest = await LoadAsync();
        await SaveAsync(manifest.Upsert(entry));
    }

    public static string Render(ProjectionManifest manifest)
    {
        var lines = new List<string>
        {
            "# Projection Manifest",
            string.Empty,
            "| Runtime Prompt | Projection Identity | Projection Prompt | Projection Prompt Type | Path | Provenance Status | Causal Inputs | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Stale Reasons | Last Validation Error |",
            "|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|",
        };

        foreach (ProjectionManifestEntry entry in manifest.Entries.OrderBy(entry => entry.RuntimePromptName, StringComparer.Ordinal))
        {
            lines.Add(string.Join(" | ", new[]
            {
                "| " + EscapeCell(entry.RuntimePromptName),
                EscapeCell(entry.EffectiveProjectionIdentity),
                EscapeCell(entry.ProjectionPromptName),
                EscapeCell(entry.ProjectionPromptType),
                EscapeCell(entry.ProjectionPath),
                EscapeCell(entry.ProvenanceStatus.ToString()),
                EscapeCell(JsonSerializer.Serialize(entry.EffectiveCausalInputs)),
                EscapeCell(entry.ProjectionPromptSourceHash),
                EscapeCell(string.Join(';', entry.ProjectContextFiles)),
                EscapeCell(entry.ProjectContextHash),
                EscapeCell(entry.ProjectionHash),
                EscapeCell(entry.GeneratedAt.ToString("O")),
                EscapeCell(entry.ValidationStatus.ToString()),
                EscapeCell(entry.StaleStatus.ToString()),
                EscapeCell(string.Join(';', entry.EffectiveStaleReasons)),
                EscapeCell(string.IsNullOrWhiteSpace(entry.LastValidationError) ? "None" : entry.LastValidationError),
            }) + " |");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string EscapeCell(string? value) =>
        (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string UnescapeCell(string value) =>
        value.Trim().Replace("\\|", "|", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);

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
        catch (JsonException)
        {
            return [];
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
            }
        }

        return reasons;
    }
}
