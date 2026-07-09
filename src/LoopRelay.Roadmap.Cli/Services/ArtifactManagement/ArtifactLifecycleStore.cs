using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.State;

namespace LoopRelay.Roadmap.Cli.Services.ArtifactManagement;

internal sealed class ArtifactLifecycleStore(RoadmapArtifacts _artifacts) : IArtifactLifecycleStore
{
    private readonly StructuredDocumentStore<ArtifactLifecyclePersistenceDocument> _structuredStore = new(
        _artifacts,
        RoadmapArtifactPaths.LifecycleJson,
        ArtifactLifecyclePersistenceDocument.CurrentSchemaVersion,
        document => document.SchemaVersion,
        ArtifactLifecyclePersistenceDocument.Validate);

    public async Task<IReadOnlyList<ArtifactLifecycleEntry>> LoadAsync()
    {
        ArtifactLifecyclePersistenceDocument? structured = await _structuredStore.LoadAsync();
        if (structured is not null)
        {
            return structured.ToDomain();
        }

        string? content = await _artifacts.ReadAsync(RoadmapArtifactPaths.Lifecycle);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        IReadOnlyList<ArtifactLifecycleEntry> migrated;
        try
        {
            migrated = ParseLegacyMarkdown(content);
        }
        catch (MarkdownParseException exception)
        {
            throw new RoadmapStepException($"Legacy artifact lifecycle cannot be migrated: {exception.Message}");
        }

        await SaveAsync(migrated);
        return migrated;
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
        ArtifactLifecyclePersistenceDocument persisted = ArtifactLifecyclePersistenceDocument.FromDomain(entries);
        await _structuredStore.SaveAsync(persisted);
    }

    internal static IReadOnlyList<ArtifactLifecycleEntry> ParseLegacyMarkdown(string content)
    {
        MarkdownTableParser.ValidateTables(content);
        var entries = new List<ArtifactLifecycleEntry>();
        foreach (IReadOnlyDictionary<string, string> row in MarkdownTableParser.ParseTablesStrict(content))
        {
            if (!row.ContainsKey("Path") || !row.ContainsKey("State") || !row.ContainsKey("Updated At") || !row.ContainsKey("Notes"))
            {
                continue;
            }

            entries.Add(new ArtifactLifecycleEntry(
                row["Path"],
                Enum.TryParse(row["State"], out ArtifactLifecycleState state) ? state : ArtifactLifecycleState.Missing,
                DateTimeOffset.TryParse(row["Updated At"], out DateTimeOffset updatedAt) ? updatedAt : DateTimeOffset.MinValue,
                row["Notes"]));
        }

        ArtifactLifecyclePersistenceDocument persisted = ArtifactLifecyclePersistenceDocument.FromDomain(entries);
        IReadOnlyList<string> errors = ArtifactLifecyclePersistenceDocument.Validate(persisted);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException($"Legacy artifact lifecycle cannot be migrated because validation failed: {string.Join("; ", errors)}");
        }

        return entries.OrderBy(entry => entry.Path, StringComparer.Ordinal).ToArray();
    }

}
