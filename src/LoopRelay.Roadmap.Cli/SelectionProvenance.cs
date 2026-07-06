using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoopRelay.Roadmap.Cli;

internal sealed record SelectionProvenanceManifest(
    string SchemaVersion,
    IReadOnlyList<DerivedArtifactManifestEntry> Selections)
{
    public const string CurrentSchemaVersion = "selection-provenance.v1";

    public static SelectionProvenanceManifest Empty { get; } = new(
        CurrentSchemaVersion,
        []);

    public IReadOnlyList<DerivedArtifactManifestEntry> ActiveSelections =>
        Selections.Where(entry => entry.ProvenanceStatus == DerivedArtifactProvenanceStatus.Trusted).ToArray();

    public SelectionProvenanceManifest UpsertActive(DerivedArtifactManifestEntry entry)
    {
        var next = Selections
            .Where(existing =>
                !string.Equals(existing.ArtifactKind, entry.ArtifactKind, StringComparison.Ordinal) ||
                !string.Equals(existing.ArtifactIdentity, entry.ArtifactIdentity, StringComparison.Ordinal))
            .Select(existing =>
                existing.ProvenanceStatus == DerivedArtifactProvenanceStatus.Trusted &&
                string.Equals(existing.ArtifactKind, entry.ArtifactKind, StringComparison.Ordinal)
                    ? existing.Supersede([DerivedArtifactStaleReason.Superseded])
                    : existing)
            .Append(entry)
            .OrderBy(existing => existing.ArtifactKind, StringComparer.Ordinal)
            .ThenBy(existing => existing.GeneratedAt)
            .ThenBy(existing => existing.ArtifactIdentity, StringComparer.Ordinal)
            .ToArray();

        return this with
        {
            SchemaVersion = CurrentSchemaVersion,
            Selections = next,
        };
    }

    public SelectionProvenanceManifest SupersedeActive(IReadOnlyList<DerivedArtifactStaleReason> reasons)
    {
        var next = Selections
            .Select(entry =>
                entry.ProvenanceStatus == DerivedArtifactProvenanceStatus.Trusted
                    ? entry.Supersede(reasons)
                    : entry)
            .OrderBy(existing => existing.ArtifactKind, StringComparer.Ordinal)
            .ThenBy(existing => existing.GeneratedAt)
            .ThenBy(existing => existing.ArtifactIdentity, StringComparer.Ordinal)
            .ToArray();

        return this with
        {
            SchemaVersion = CurrentSchemaVersion,
            Selections = next,
        };
    }
}

internal sealed class SelectionProvenanceManifestStore(RoadmapArtifacts artifacts)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<SelectionProvenanceManifest> LoadAsync()
    {
        string? content = await artifacts.ReadAsync(RoadmapArtifactPaths.SelectionProvenanceManifest);
        if (string.IsNullOrWhiteSpace(content))
        {
            return SelectionProvenanceManifest.Empty;
        }

        try
        {
            SelectionProvenanceManifest? manifest = JsonSerializer.Deserialize<SelectionProvenanceManifest>(content, JsonOptions);
            return manifest ?? SelectionProvenanceManifest.Empty;
        }
        catch (JsonException)
        {
            return SelectionProvenanceManifest.Empty;
        }
    }

    public async Task SaveAsync(SelectionProvenanceManifest manifest)
    {
        string content = JsonSerializer.Serialize(manifest, JsonOptions) + Environment.NewLine;
        await artifacts.WriteAsync(RoadmapArtifactPaths.SelectionProvenanceManifest, content);
    }
}

internal sealed class SelectionProvenanceService(
    RoadmapArtifacts artifacts,
    SelectionProvenanceManifestStore manifestStore,
    RoadmapPromptContextBuilder contextBuilder,
    TransitionInputResolver inputResolver)
{
    public const string SelectionArtifactKind = "SelectionDecision";
    public const string SelectionGenerator = "SelectNextEpic:v1";
    public const string SelectionCycleInputKind = "SelectionCycle";
    public const string SelectionProjectionInputKind = "SelectionProjection";
    public const string SelectionPromptContextInputKind = "SelectionPromptContext";
    public const string SelectionSecondaryInputKind = "SelectionSecondaryInput";
    public const string RoadmapCompletionContextInputKind = "RoadmapCompletionContext";
    public const string RoadmapSourceInputKind = "RoadmapSource";
    public const string RetiredEpicStateInputKind = "RetiredEpicState";

    private const string MissingInputVersion = "missing";

    public async Task<TransitionInputSnapshot> CaptureCurrentCycleAsync(
        string projectionContent,
        IReadOnlyList<RetiredEpic> retiredEpics,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string context = await contextBuilder.BuildSelectionContextAsync(projectionContent, retiredEpics);
        return await inputResolver.ResolveAsync(new TransitionInputRequest(
            "SelectNextEpic",
            RoadmapArtifactPaths.ProjectionPaths["SelectNextEpic"],
            context,
            string.Empty,
            TransitionInputContext.Empty));
    }

    public async Task RecordActiveSelectionAsync(
        string selectionContent,
        TransitionInputSnapshot inputSnapshot,
        IReadOnlyList<RetiredEpic> retiredEpics,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DerivedArtifactProvenance provenance = CreateProvenance(inputSnapshot, retiredEpics);
        DerivedArtifactManifestEntry entry = DerivedArtifactManifestEntry.FromTrustedProvenance(
            provenance,
            RoadmapArtifactPaths.Selection,
            RoadmapHash.Sha256(selectionContent),
            DateTimeOffset.UtcNow);
        SelectionProvenanceManifest manifest = (await manifestStore.LoadAsync()).UpsertActive(entry);
        await manifestStore.SaveAsync(manifest);
    }

    public async Task<DerivedArtifactFreshness> EvaluateActiveSelectionFreshnessAsync(
        TransitionInputSnapshot currentCycle,
        IReadOnlyList<RetiredEpic> retiredEpics,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectionProvenanceManifest manifest = await manifestStore.LoadAsync();
        IReadOnlyList<DerivedArtifactManifestEntry> activeSelections = manifest.ActiveSelections;
        if (activeSelections.Count == 0)
        {
            if (manifest.Selections.Any(entry =>
                string.Equals(entry.ArtifactKind, SelectionArtifactKind, StringComparison.Ordinal) &&
                entry.ProvenanceStatus == DerivedArtifactProvenanceStatus.Superseded))
            {
                return DerivedArtifactFreshness.Stale(DerivedArtifactStaleReason.Superseded);
            }

            return DerivedArtifactFreshness.Unknown(DerivedArtifactStaleReason.MissingManifest);
        }

        if (activeSelections.Count > 1)
        {
            return DerivedArtifactFreshness.Unknown(DerivedArtifactStaleReason.UnknownProvenance);
        }

        string? selectionContent = await artifacts.ReadAsync(RoadmapArtifactPaths.Selection);
        string? selectionHash = string.IsNullOrWhiteSpace(selectionContent)
            ? null
            : RoadmapHash.Sha256(selectionContent);
        return DerivedArtifactFreshnessEvaluator.Evaluate(
            CreateProvenance(currentCycle, retiredEpics),
            activeSelections[0],
            selectionHash);
    }

    public async Task SupersedeActiveSelectionAsync(
        IReadOnlyList<DerivedArtifactStaleReason> reasons,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectionProvenanceManifest manifest = await manifestStore.LoadAsync();
        if (manifest.ActiveSelections.Count == 0)
        {
            return;
        }

        await manifestStore.SaveAsync(manifest.SupersedeActive(reasons));
    }

    private static DerivedArtifactProvenance CreateProvenance(
        TransitionInputSnapshot snapshot,
        IReadOnlyList<RetiredEpic> retiredEpics)
    {
        var inputs = new List<DerivedArtifactCausalInput>
        {
            new(SelectionCycleInputKind, "SelectNextEpic", snapshot.SnapshotHash),
            new(SelectionPromptContextInputKind, "SelectNextEpic:prompt-context", snapshot.PromptContextHash),
            new(SelectionSecondaryInputKind, "SelectNextEpic:secondary-input", snapshot.SecondaryInputHash),
            new(RetiredEpicStateInputKind, "retired-epics", RetiredEpicStateHash(retiredEpics)),
        };

        foreach (TransitionArtifactInput input in snapshot.ArtifactInputs)
        {
            inputs.Add(new DerivedArtifactCausalInput(
                SelectionInputKind(input),
                input.Path,
                input.Hash ?? MissingInputVersion));
        }

        return new DerivedArtifactProvenance(
            SelectionArtifactKind,
            CycleIdentity(snapshot),
            SelectionGenerator,
            inputs);
    }

    private static string CycleIdentity(TransitionInputSnapshot snapshot) =>
        $"selection-cycle:{snapshot.SnapshotHash}";

    private static string RetiredEpicStateHash(IReadOnlyList<RetiredEpic> retiredEpics)
    {
        if (retiredEpics.Count == 0)
        {
            return RoadmapHash.Sha256("none");
        }

        var lines = retiredEpics
            .OrderBy(epic => epic.IdentityKind, StringComparer.Ordinal)
            .ThenBy(epic => epic.StableIdentity, StringComparer.Ordinal)
            .Select(epic => string.Join(
                '\t',
                epic.IdentityKind,
                epic.StableIdentity,
                epic.DisplayName,
                epic.PrimaryReason,
                epic.AuditEvidencePath));
        return RoadmapHash.Sha256(string.Join('\n', lines));
    }

    private static string SelectionInputKind(TransitionArtifactInput input)
    {
        string[] roles = input.Roles.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (roles.Contains(TransitionInputRole.Projection, StringComparer.Ordinal))
        {
            return SelectionProjectionInputKind;
        }

        if (roles.Contains(TransitionInputRole.RoadmapCompletionContext, StringComparer.Ordinal))
        {
            return RoadmapCompletionContextInputKind;
        }

        if (roles.Contains(TransitionInputRole.RoadmapSource, StringComparer.Ordinal))
        {
            return RoadmapSourceInputKind;
        }

        return input.Roles;
    }
}
