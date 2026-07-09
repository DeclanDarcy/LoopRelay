using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LoopRelay.Orchestration.Services;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Decisions;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Services.State;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal enum WorkspaceLoopHistoryKind
{
    Decisions,
    Handoff,
    OperationalDelta,
}

internal sealed record SplitFamilyFilesystemSnapshot(
    string FamilyId,
    string RelativePath,
    SplitFamilyPersistenceDocument Document);

internal sealed record LoopHistoryFilesystemSnapshot(
    WorkspaceLoopHistoryKind Kind,
    int Sequence,
    string RelativePath,
    string Body);

internal sealed record ExecutionEvidenceFilesystemSnapshot(
    string Stem,
    int Sequence,
    string RelativePath,
    string Body);

internal sealed record WorkspaceFilesystemSnapshot(
    DecisionLedgerPersistenceDocument DecisionLedger,
    RoadmapStatePersistenceDocument? RoadmapState,
    ArtifactLifecyclePersistenceDocument ArtifactLifecycle,
    IReadOnlyList<SplitFamilyFilesystemSnapshot> SplitFamilies,
    ExecutionPreparationManifest ExecutionPreparationManifest,
    SelectionProvenanceManifest SelectionProvenanceManifest,
    ProjectionManifestPersistenceDocument ProjectionManifest,
    IReadOnlyList<TransitionJournalRecord> TransitionJournal,
    IReadOnlyList<LoopHistoryFilesystemSnapshot> LoopHistories,
    IReadOnlyList<ExecutionEvidenceFilesystemSnapshot> ExecutionEvidence);

internal sealed partial class WorkspaceFilesystemSnapshotStore
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions JournalJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkspaceFilesystemSnapshot> ImportAsync(RoadmapArtifacts artifacts) =>
        new(
            await ImportDecisionLedgerAsync(artifacts),
            await ImportRoadmapStateAsync(artifacts),
            await ImportArtifactLifecycleAsync(artifacts),
            await ImportSplitFamiliesAsync(artifacts),
            await ImportExecutionPreparationManifestAsync(artifacts),
            await ImportSelectionProvenanceManifestAsync(artifacts),
            await ImportProjectionManifestAsync(artifacts),
            await ImportTransitionJournalAsync(artifacts),
            await ImportLoopHistoriesAsync(artifacts),
            await ImportExecutionEvidenceAsync(artifacts));

    public async Task ExportAsync(RoadmapArtifacts artifacts, WorkspaceFilesystemSnapshot snapshot)
    {
        await artifacts.WriteAsync(
            RoadmapArtifactPaths.DecisionLedgerJson,
            SerializeStructured(snapshot.DecisionLedger));

        if (snapshot.RoadmapState is not null)
        {
            await artifacts.WriteAsync(
                RoadmapArtifactPaths.StateJson,
                SerializeStructured(snapshot.RoadmapState));
        }

        await artifacts.WriteAsync(
            RoadmapArtifactPaths.LifecycleJson,
            SerializeStructured(snapshot.ArtifactLifecycle));

        foreach (SplitFamilyFilesystemSnapshot split in snapshot.SplitFamilies.OrderBy(split => split.FamilyId, StringComparer.Ordinal))
        {
            await artifacts.WriteAsync(split.RelativePath, SerializeStructured(split.Document));
        }

        await artifacts.WriteAsync(
            RoadmapArtifactPaths.ExecutionPreparationManifest,
            SerializeManifest(snapshot.ExecutionPreparationManifest));

        await artifacts.WriteAsync(
            RoadmapArtifactPaths.SelectionProvenanceManifest,
            SerializeManifest(snapshot.SelectionProvenanceManifest));

        await artifacts.WriteAsync(
            RoadmapArtifactPaths.ProjectionsManifestJson,
            SerializeStructured(snapshot.ProjectionManifest));

        if (snapshot.TransitionJournal.Count > 0)
        {
            string journal = string.Join(
                Environment.NewLine,
                snapshot.TransitionJournal.Select(record => JsonSerializer.Serialize(record, JournalJsonOptions))) + Environment.NewLine;
            await artifacts.WriteAsync(RoadmapArtifactPaths.TransitionJournal, journal);
        }

        foreach (LoopHistoryFilesystemSnapshot history in snapshot.LoopHistories.OrderBy(history => history.Kind).ThenBy(history => history.Sequence))
        {
            await artifacts.WriteAsync(history.RelativePath, history.Body);
        }

        foreach (ExecutionEvidenceFilesystemSnapshot evidence in snapshot.ExecutionEvidence
            .OrderBy(evidence => evidence.Stem, StringComparer.Ordinal)
            .ThenBy(evidence => evidence.Sequence))
        {
            await artifacts.WriteAsync(evidence.RelativePath, evidence.Body);
        }
    }

    private static async Task<DecisionLedgerPersistenceDocument> ImportDecisionLedgerAsync(RoadmapArtifacts artifacts)
    {
        string? json = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.DecisionLedgerJson);
        if (json is not null)
        {
            return DeserializeStrict<DecisionLedgerPersistenceDocument>(
                "decision ledger",
                RoadmapArtifactPaths.DecisionLedgerJson,
                json,
                DecisionLedgerPersistenceDocument.CurrentSchemaVersion,
                document => document.SchemaVersion,
                DecisionLedgerPersistenceDocument.Validate);
        }

        string? legacy = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.DecisionLedger);
        if (legacy is null)
        {
            return DecisionLedgerPersistenceDocument.Empty;
        }

        try
        {
            return DecisionLedgerStore.ParseLegacyMarkdown(legacy);
        }
        catch (MarkdownParseException exception)
        {
            throw SnapshotFailure("decision ledger", RoadmapArtifactPaths.DecisionLedger, "legacy markdown", exception.Message);
        }
    }

    private static async Task<RoadmapStatePersistenceDocument?> ImportRoadmapStateAsync(RoadmapArtifacts artifacts)
    {
        string? json = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.StateJson);
        if (json is not null)
        {
            return DeserializeStrict<RoadmapStatePersistenceDocument>(
                "roadmap state",
                RoadmapArtifactPaths.StateJson,
                json,
                RoadmapStatePersistenceDocument.CurrentSchemaVersion,
                document => document.SchemaVersion,
                RoadmapStatePersistenceDocument.Validate);
        }

        string? legacy = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.State);
        if (legacy is null)
        {
            return null;
        }

        try
        {
            return RoadmapStatePersistenceDocument.FromDomain(RoadmapStateStore.ParseLegacyMarkdown(legacy));
        }
        catch (MarkdownParseException exception)
        {
            throw SnapshotFailure("roadmap state", RoadmapArtifactPaths.State, "legacy markdown", exception.Message);
        }
    }

    private static async Task<ArtifactLifecyclePersistenceDocument> ImportArtifactLifecycleAsync(RoadmapArtifacts artifacts)
    {
        string? json = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.LifecycleJson);
        if (json is not null)
        {
            return DeserializeStrict<ArtifactLifecyclePersistenceDocument>(
                "artifact lifecycle",
                RoadmapArtifactPaths.LifecycleJson,
                json,
                ArtifactLifecyclePersistenceDocument.CurrentSchemaVersion,
                document => document.SchemaVersion,
                ArtifactLifecyclePersistenceDocument.Validate);
        }

        string? legacy = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.Lifecycle);
        if (legacy is null)
        {
            return ArtifactLifecyclePersistenceDocument.FromDomain([]);
        }

        try
        {
            return ArtifactLifecyclePersistenceDocument.FromDomain(ArtifactLifecycleStore.ParseLegacyMarkdown(legacy));
        }
        catch (MarkdownParseException exception)
        {
            throw SnapshotFailure("artifact lifecycle", RoadmapArtifactPaths.Lifecycle, "legacy markdown", exception.Message);
        }
    }

    private static async Task<IReadOnlyList<SplitFamilyFilesystemSnapshot>> ImportSplitFamiliesAsync(RoadmapArtifacts artifacts)
    {
        var snapshots = new List<SplitFamilyFilesystemSnapshot>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        IReadOnlyList<string> structured = await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.json");
        foreach (string path in structured.Order(StringComparer.Ordinal))
        {
            string content = await ReadRequiredListedFileAsync(artifacts, path, "split lineage");
            SplitFamilyPersistenceDocument document = DeserializeStrict<SplitFamilyPersistenceDocument>(
                "split lineage",
                path,
                content,
                SplitFamilyPersistenceDocument.CurrentSchemaVersion,
                item => item.SchemaVersion,
                SplitFamilyPersistenceDocument.Validate);
            string pathFamilyId = SplitFamilyStore.FamilyIdFromPath(path);
            if (!string.Equals(pathFamilyId, document.Family.FamilyId, StringComparison.Ordinal))
            {
                throw SnapshotFailure(
                    "split lineage",
                    path,
                    document.Family.FamilyId,
                    $"filename family ID `{pathFamilyId}` does not match document family ID `{document.Family.FamilyId}`.");
            }

            AddSplitSnapshot(snapshots, seen, path, document);
        }

        IReadOnlyList<string> legacyFiles = await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.md");
        foreach (string path in legacyFiles.Order(StringComparer.Ordinal))
        {
            string pathFamilyId = SplitFamilyStore.FamilyIdFromPath(path);
            if (await artifacts.ExistsAsync(RoadmapArtifactPaths.SplitFamilyJson(pathFamilyId)))
            {
                continue;
            }

            string content = await ReadRequiredListedFileAsync(artifacts, path, "split lineage");
            SplitFamily family;
            try
            {
                family = SplitFamilyStore.ParseLegacyMarkdown(path, content);
            }
            catch (MarkdownParseException exception)
            {
                throw SnapshotFailure("split lineage", path, pathFamilyId, exception.Message);
            }

            AddSplitSnapshot(
                snapshots,
                seen,
                RoadmapArtifactPaths.SplitFamilyJson(family.FamilyId),
                SplitFamilyPersistenceDocument.FromDomain(family));
        }

        return snapshots.OrderBy(snapshot => snapshot.FamilyId, StringComparer.Ordinal).ToArray();
    }

    private static async Task<ExecutionPreparationManifest> ImportExecutionPreparationManifestAsync(RoadmapArtifacts artifacts)
    {
        string? content = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.ExecutionPreparationManifest);
        return content is null
            ? ExecutionPreparationManifest.Empty
            : DeserializeManifest(content, ExecutionPreparationManifest.Empty);
    }

    private static async Task<SelectionProvenanceManifest> ImportSelectionProvenanceManifestAsync(RoadmapArtifacts artifacts)
    {
        string? content = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.SelectionProvenanceManifest);
        return content is null
            ? SelectionProvenanceManifest.Empty
            : DeserializeManifest(content, SelectionProvenanceManifest.Empty);
    }

    private static async Task<ProjectionManifestPersistenceDocument> ImportProjectionManifestAsync(RoadmapArtifacts artifacts)
    {
        string? json = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.ProjectionsManifestJson);
        if (json is not null)
        {
            return DeserializeStrict<ProjectionManifestPersistenceDocument>(
                "projection manifest",
                RoadmapArtifactPaths.ProjectionsManifestJson,
                json,
                ProjectionManifestPersistenceDocument.CurrentSchemaVersion,
                document => document.SchemaVersion,
                ProjectionManifestPersistenceDocument.Validate);
        }

        string? legacy = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.ProjectionsManifest);
        if (legacy is null)
        {
            return ProjectionManifestPersistenceDocument.FromDomain(ProjectionManifest.Empty);
        }

        try
        {
            return ProjectionManifestPersistenceDocument.FromDomain(ProjectionManifestStore.ParseLegacyMarkdown(legacy));
        }
        catch (MarkdownParseException exception)
        {
            throw SnapshotFailure("projection manifest", RoadmapArtifactPaths.ProjectionsManifest, "legacy markdown", exception.Message);
        }
    }

    private static async Task<IReadOnlyList<TransitionJournalRecord>> ImportTransitionJournalAsync(RoadmapArtifacts artifacts)
    {
        string? content = await ReadOptionalAsync(artifacts, RoadmapArtifactPaths.TransitionJournal);
        if (content is null)
        {
            return [];
        }

        var records = new List<TransitionJournalRecord>();
        int lineNumber = 0;
        foreach (string line in content.Split('\n'))
        {
            lineNumber++;
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            try
            {
                records.Add(JsonSerializer.Deserialize<TransitionJournalRecord>(trimmed, JournalJsonOptions)
                    ?? throw new JsonException("Journal record was null."));
            }
            catch (JsonException exception)
            {
                throw SnapshotFailure(
                    "transition journal",
                    RoadmapArtifactPaths.TransitionJournal,
                    $"line {lineNumber}",
                    $"invalid JSONL record: {exception.Message}");
            }
        }

        return records.ToArray();
    }

    private static async Task<IReadOnlyList<LoopHistoryFilesystemSnapshot>> ImportLoopHistoriesAsync(RoadmapArtifacts artifacts)
    {
        var histories = new List<LoopHistoryFilesystemSnapshot>();
        await ImportLoopHistoryKindAsync(
            artifacts,
            histories,
            WorkspaceLoopHistoryKind.Decisions,
            OrchestrationArtifactPaths.DecisionsDirectory,
            OrchestrationArtifactPaths.HistoricalDecisionSearchPattern,
            "decisions");
        await ImportLoopHistoryKindAsync(
            artifacts,
            histories,
            WorkspaceLoopHistoryKind.Handoff,
            OrchestrationArtifactPaths.HandoffsDirectory,
            OrchestrationArtifactPaths.HistoricalHandoffSearchPattern,
            "handoff");
        await ImportLoopHistoryKindAsync(
            artifacts,
            histories,
            WorkspaceLoopHistoryKind.OperationalDelta,
            OrchestrationArtifactPaths.DeltasDirectory,
            OrchestrationArtifactPaths.HistoricalDeltaSearchPattern,
            "operational_delta");

        return histories.OrderBy(history => history.Kind).ThenBy(history => history.Sequence).ToArray();
    }

    private static async Task ImportLoopHistoryKindAsync(
        RoadmapArtifacts artifacts,
        List<LoopHistoryFilesystemSnapshot> histories,
        WorkspaceLoopHistoryKind kind,
        string directory,
        string searchPattern,
        string baseName)
    {
        var seen = new HashSet<int>();
        IReadOnlyList<string> paths = await artifacts.ListAsync(directory, searchPattern);
        foreach (string path in paths.Order(StringComparer.Ordinal))
        {
            Match match = HistoryFileRegex(baseName).Match(Path.GetFileName(path));
            if (!match.Success || !int.TryParse(match.Groups["number"].Value, out int sequence) || sequence <= 0)
            {
                throw SnapshotFailure("loop history", path, kind.ToString(), "history filename must end with a positive four-digit sequence.");
            }

            if (!seen.Add(sequence))
            {
                throw SnapshotFailure("loop history", path, kind.ToString(), $"duplicate history sequence `{sequence:0000}`.");
            }

            histories.Add(new LoopHistoryFilesystemSnapshot(
                kind,
                sequence,
                path,
                await ReadRequiredListedFileAsync(artifacts, path, "loop history")));
        }
    }

    private static async Task<IReadOnlyList<ExecutionEvidenceFilesystemSnapshot>> ImportExecutionEvidenceAsync(RoadmapArtifacts artifacts)
    {
        IReadOnlyList<string> paths = await artifacts.ListAsync(RoadmapArtifactPaths.ExecutionEvidenceDirectory, "*.md");
        var evidence = new List<ExecutionEvidenceFilesystemSnapshot>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string path in paths.Order(StringComparer.Ordinal))
        {
            Match match = ExecutionEvidenceFileRegex().Match(Path.GetFileName(path));
            if (!match.Success || !int.TryParse(match.Groups["number"].Value, out int sequence) || sequence <= 0)
            {
                throw SnapshotFailure("execution evidence", path, Path.GetFileName(path), "evidence filename must end with a positive four-digit sequence.");
            }

            string stem = match.Groups["stem"].Value;
            string key = $"{stem}\0{sequence:0000}";
            if (!seen.Add(key))
            {
                throw SnapshotFailure("execution evidence", path, key, "duplicate evidence stem and sequence.");
            }

            evidence.Add(new ExecutionEvidenceFilesystemSnapshot(
                stem,
                sequence,
                path,
                await ReadRequiredListedFileAsync(artifacts, path, "execution evidence")));
        }

        return evidence
            .OrderBy(item => item.Stem, StringComparer.Ordinal)
            .ThenBy(item => item.Sequence)
            .ToArray();
    }

    private static void AddSplitSnapshot(
        List<SplitFamilyFilesystemSnapshot> snapshots,
        HashSet<string> seen,
        string path,
        SplitFamilyPersistenceDocument document)
    {
        string familyId = document.Family.FamilyId;
        if (!seen.Add(familyId))
        {
            throw SnapshotFailure("split lineage", path, familyId, $"duplicate split family ID `{familyId}`.");
        }

        snapshots.Add(new SplitFamilyFilesystemSnapshot(familyId, path, document));
    }

    private static async Task<string?> ReadOptionalAsync(RoadmapArtifacts artifacts, string path)
    {
        string? content = await artifacts.ReadAsync(path);
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    private static async Task<string> ReadRequiredListedFileAsync(RoadmapArtifacts artifacts, string path, string domain)
    {
        string? content = await artifacts.ReadAsync(path);
        if (content is null)
        {
            throw SnapshotFailure(domain, path, path, "listed file could not be read.");
        }

        return content;
    }

    private static TDocument DeserializeStrict<TDocument>(
        string domain,
        string path,
        string content,
        string expectedSchemaVersion,
        Func<TDocument, string?> getSchemaVersion,
        Func<TDocument, IReadOnlyList<string>> validate)
        where TDocument : class
    {
        TDocument document;
        try
        {
            document = JsonSerializer.Deserialize<TDocument>(content, RoadmapJson.Options)
                ?? throw new JsonException("Document was null.");
        }
        catch (JsonException exception)
        {
            throw SnapshotFailure(domain, path, path, $"invalid JSON: {exception.Message}");
        }

        string? schemaVersion = getSchemaVersion(document);
        if (!string.Equals(schemaVersion, expectedSchemaVersion, StringComparison.Ordinal))
        {
            throw SnapshotFailure(
                domain,
                path,
                schemaVersion ?? "null",
                $"unsupported schema version `{schemaVersion ?? "null"}`; expected `{expectedSchemaVersion}`.");
        }

        IReadOnlyList<string> errors = validate(document);
        if (errors.Count > 0)
        {
            throw SnapshotFailure(domain, path, path, string.Join("; ", errors));
        }

        return document;
    }

    private static TManifest DeserializeManifest<TManifest>(string content, TManifest empty)
        where TManifest : class
    {
        try
        {
            return JsonSerializer.Deserialize<TManifest>(content, ManifestJsonOptions) ?? empty;
        }
        catch (JsonException)
        {
            return empty;
        }
    }

    private static string SerializeStructured<TDocument>(TDocument document) =>
        JsonSerializer.Serialize(document, RoadmapJson.Options) + Environment.NewLine;

    private static string SerializeManifest<TManifest>(TManifest manifest) =>
        JsonSerializer.Serialize(manifest, ManifestJsonOptions) + Environment.NewLine;

    private static RoadmapStepException SnapshotFailure(string domain, string path, string identity, string reason) =>
        new($"Filesystem snapshot import failed for {domain} at `{path}` ({identity}): {reason}");

    private static Regex HistoryFileRegex(string baseName) =>
        new($"^{Regex.Escape(baseName)}\\.(?<number>\\d{{4}})\\.md$", RegexOptions.CultureInvariant);

    [GeneratedRegex(@"^(?<stem>.+)\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex ExecutionEvidenceFileRegex();
}
