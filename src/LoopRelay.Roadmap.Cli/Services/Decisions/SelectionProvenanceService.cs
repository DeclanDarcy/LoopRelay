using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;
using LoopRelay.Roadmap.Cli.Models.RoadmapTracking;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.DerivedArtifacts;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.State;
using LoopRelay.Roadmap.Cli.Services.TransitionState;

namespace LoopRelay.Roadmap.Cli.Services.Decisions;

internal sealed class SelectionProvenanceService(
    RoadmapArtifacts _artifacts,
    SelectionProvenanceManifestStore _manifestStore,
    RoadmapPromptContextBuilder _contextBuilder,
    TransitionInputResolver _inputResolver)
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
        string context = await _contextBuilder.BuildSelectionContextAsync(projectionContent, retiredEpics);
        return await _inputResolver.ResolveAsync(new TransitionInputRequest(
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
        SelectionProvenanceManifest manifest = (await _manifestStore.LoadAsync()).UpsertActive(entry);
        await _manifestStore.SaveAsync(manifest);
    }

    public async Task<DerivedArtifactFreshness> EvaluateActiveSelectionFreshnessAsync(
        TransitionInputSnapshot currentCycle,
        IReadOnlyList<RetiredEpic> retiredEpics,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SelectionProvenanceManifest manifest = await _manifestStore.LoadAsync();
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

        string? selectionContent = await _artifacts.ReadAsync(RoadmapArtifactPaths.Selection);
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
        SelectionProvenanceManifest manifest = await _manifestStore.LoadAsync();
        if (manifest.ActiveSelections.Count == 0)
        {
            return;
        }

        await _manifestStore.SaveAsync(manifest.SupersedeActive(reasons));
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
