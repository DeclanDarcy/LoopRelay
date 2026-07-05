namespace CommandCenter.Roadmap.Cli;

internal sealed class ArtifactLifecycleStore(RoadmapArtifacts artifacts)
{
    public async Task<IReadOnlyList<ArtifactLifecycleEntry>> LoadAsync()
    {
        string? content = await artifacts.ReadAsync(RoadmapArtifactPaths.Lifecycle);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var entries = new List<ArtifactLifecycleEntry>();
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith('|') || trimmed.StartsWith("|---", StringComparison.Ordinal) || trimmed.Contains("Path", StringComparison.Ordinal))
            {
                continue;
            }

            string[] cells = trimmed.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
            if (cells.Length < 4)
            {
                continue;
            }

            entries.Add(new ArtifactLifecycleEntry(
                cells[0],
                Enum.TryParse(cells[1], out ArtifactLifecycleState state) ? state : ArtifactLifecycleState.Missing,
                DateTimeOffset.TryParse(cells[2], out DateTimeOffset updatedAt) ? updatedAt : DateTimeOffset.MinValue,
                cells[3]));
        }

        return entries;
    }

    public async Task UpsertAsync(string path, ArtifactLifecycleState state, string notes = "")
    {
        IReadOnlyList<ArtifactLifecycleEntry> current = await LoadAsync();
        var next = current
            .Where(entry => !string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase))
            .Append(new ArtifactLifecycleEntry(path, state, DateTimeOffset.UtcNow, notes))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ToList();
        await SaveAsync(next);
    }

    public async Task SaveAsync(IReadOnlyList<ArtifactLifecycleEntry> entries)
    {
        var lines = new List<string>
        {
            "# Artifact Lifecycle",
            string.Empty,
            "| Path | State | Updated At | Notes |",
            "|---|---|---|---|",
        };

        foreach (ArtifactLifecycleEntry entry in entries.OrderBy(entry => entry.Path, StringComparer.Ordinal))
        {
            lines.Add($"| {entry.Path} | {entry.State} | {entry.UpdatedAt:O} | {entry.Notes.Replace('\n', ' ')} |");
        }

        await artifacts.WriteAsync(RoadmapArtifactPaths.Lifecycle, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }
}
