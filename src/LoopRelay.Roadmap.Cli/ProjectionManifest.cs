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
