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
            if (cells.Length < 11)
            {
                continue;
            }

            entries.Add(new ProjectionManifestEntry(
                cells[0],
                cells[1],
                cells[2],
                cells[3],
                cells[4].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                cells[5],
                cells[6],
                DateTimeOffset.TryParse(cells[7], out DateTimeOffset generatedAt) ? generatedAt : DateTimeOffset.MinValue,
                Enum.TryParse(cells[8], out ProjectionValidationStatus validationStatus) ? validationStatus : ProjectionValidationStatus.Unknown,
                Enum.TryParse(cells[9], out ProjectionStaleStatus staleStatus) ? staleStatus : ProjectionStaleStatus.Fresh,
                string.Equals(cells[10], "None", StringComparison.Ordinal) ? null : cells[10]));
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
            "| Runtime Prompt | Projection Prompt | Path | Projection Prompt Source Hash | Project Context Files | Project Context Hash | Projection Hash | Generated At | Validation Status | Stale Status | Last Validation Error |",
            "|---|---|---|---|---|---|---|---|---|---|---|",
        };

        foreach (ProjectionManifestEntry entry in manifest.Entries.OrderBy(entry => entry.RuntimePromptName, StringComparer.Ordinal))
        {
            lines.Add(string.Join(" | ", new[]
            {
                "| " + EscapeCell(entry.RuntimePromptName),
                EscapeCell(entry.ProjectionPromptName),
                EscapeCell(entry.ProjectionPath),
                EscapeCell(entry.ProjectionPromptSourceHash),
                EscapeCell(string.Join(';', entry.ProjectContextFiles)),
                EscapeCell(entry.ProjectContextHash),
                EscapeCell(entry.ProjectionHash),
                EscapeCell(entry.GeneratedAt.ToString("O")),
                EscapeCell(entry.ValidationStatus.ToString()),
                EscapeCell(entry.StaleStatus.ToString()),
                EscapeCell(string.IsNullOrWhiteSpace(entry.LastValidationError) ? "None" : entry.LastValidationError),
            }) + " |");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string EscapeCell(string? value) =>
        (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string UnescapeCell(string value) =>
        value.Trim().Replace("\\|", "|", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
}
