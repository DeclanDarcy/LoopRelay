using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Projections;

namespace LoopRelay.Roadmap.Cli.Services.Artifacts;

internal sealed class RoadmapArtifactSnapshot
{
    private RoadmapArtifactSnapshot(
        IReadOnlyDictionary<string, ArtifactStatus> statuses,
        IReadOnlyDictionary<string, ArtifactLifecycleState> lifecycle,
        IReadOnlyDictionary<string, int> directoryArtifactCounts,
        IReadOnlyList<string> milestoneSpecPaths,
        bool roadmapSourceAvailable,
        ProjectionManifest manifest)
    {
        Statuses = statuses;
        Lifecycle = lifecycle;
        DirectoryArtifactCounts = directoryArtifactCounts;
        MilestoneSpecPaths = milestoneSpecPaths;
        RoadmapSourceAvailable = roadmapSourceAvailable;
        Manifest = manifest;
    }

    public IReadOnlyDictionary<string, ArtifactStatus> Statuses { get; }
    public IReadOnlyDictionary<string, ArtifactLifecycleState> Lifecycle { get; }
    public IReadOnlyDictionary<string, int> DirectoryArtifactCounts { get; }
    public IReadOnlyList<string> MilestoneSpecPaths { get; }
    public bool RoadmapSourceAvailable { get; }
    public ProjectionManifest Manifest { get; }

    public static async Task<RoadmapArtifactSnapshot> CaptureAsync(
        RoadmapArtifacts artifacts,
        ProjectionManifestStore manifestStore,
        ArtifactLifecycleStore lifecycleStore,
        ExecutionPreparationProvenanceService executionPreparation)
    {
        string[] knownPaths =
        [
            RoadmapArtifactPaths.StateJson,
            RoadmapArtifactPaths.RoadmapCompletionContext,
            RoadmapArtifactPaths.Selection,
            RoadmapArtifactPaths.ActiveEpic,
            RoadmapArtifactPaths.OperationalContext,
            RoadmapArtifactPaths.ExecutionPrompt,
            RoadmapArtifactPaths.DecisionLedgerJson,
        ];

        var statuses = new Dictionary<string, ArtifactStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in knownPaths)
        {
            statuses[path] = await artifacts.GetStatusAsync(path);
        }

        IReadOnlyList<string> specs = await FreshMilestoneSpecPathsOrEmptyAsync(executionPreparation);
        foreach (string spec in specs)
        {
            statuses[spec] = await artifacts.GetStatusAsync(spec);
        }

        IReadOnlyList<string> roadmapFiles = await artifacts.ListAsync(RoadmapArtifactPaths.RoadmapDirectory, "*.md");
        bool roadmapSourceAvailable = roadmapFiles.Count > 0;

        IReadOnlyDictionary<string, ArtifactLifecycleState> lifecycle = (await lifecycleStore.LoadAsync())
            .GroupBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().State,
                StringComparer.OrdinalIgnoreCase);

        var directoryArtifactCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [RoadmapArtifactPaths.SpecsDirectory] = specs.Count,
            [RoadmapArtifactPaths.SelectionEvidenceDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.SelectionEvidenceDirectory, "*.md")).Count,
            [RoadmapArtifactPaths.AuditEvidenceDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.AuditEvidenceDirectory, "*.md")).Count,
            [RoadmapArtifactPaths.ExecutionEvidenceDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.ExecutionEvidenceDirectory, "*.md")).Count,
            [RoadmapArtifactPaths.EvaluationEvidenceDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.EvaluationEvidenceDirectory, "*.md")).Count,
            [RoadmapArtifactPaths.BlockerEvidenceDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.BlockerEvidenceDirectory, "*.md")).Count,
            [RoadmapArtifactPaths.SplitFamiliesDirectory] = (await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "*.json")).Count,
        };

        return new RoadmapArtifactSnapshot(
            statuses,
            lifecycle,
            directoryArtifactCounts,
            specs,
            roadmapSourceAvailable,
            await manifestStore.LoadAsync());
    }

    private static async Task<IReadOnlyList<string>> FreshMilestoneSpecPathsOrEmptyAsync(
        ExecutionPreparationProvenanceService executionPreparation)
    {
        try
        {
            return await executionPreparation.RequireFreshMilestoneSpecPathsAsync();
        }
        catch (RoadmapStepException)
        {
            return [];
        }
    }

    public bool HasRequiredInput(string path)
    {
        if (string.Equals(path, RoadmapArtifactPaths.RoadmapDirectoryPattern, StringComparison.OrdinalIgnoreCase))
        {
            return RoadmapSourceAvailable;
        }

        if (string.Equals(path, RoadmapArtifactPaths.SpecsDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return MilestoneSpecPaths.Count > 0;
        }

        return HasUsableFile(path);
    }

    public bool HasRequiredOutput(string path)
    {
        if (string.Equals(path, RoadmapArtifactPaths.SpecsDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return MilestoneSpecPaths.Count > 0;
        }

        if (DirectoryArtifactCounts.TryGetValue(path, out int artifactCount))
        {
            return artifactCount > 0;
        }

        return HasUsableFile(path);
    }

    public bool HasUsableActiveEpic()
    {
        if (!HasPresentFile(RoadmapArtifactPaths.ActiveEpic))
        {
            return false;
        }

        if (!Lifecycle.TryGetValue(RoadmapArtifactPaths.ActiveEpic, out ArtifactLifecycleState state))
        {
            return true;
        }

        return state is ArtifactLifecycleState.Ready or ArtifactLifecycleState.Executing;
    }

    public bool HasUsableFile(string path) =>
        HasPresentFile(path) && LifecycleAllowsUse(path);

    public bool HasPresentFile(string path) =>
        Statuses.TryGetValue(path, out ArtifactStatus status) && status == ArtifactStatus.Present;

    private bool LifecycleAllowsUse(string path)
    {
        if (!Lifecycle.TryGetValue(path, out ArtifactLifecycleState state))
        {
            return true;
        }

        return state is ArtifactLifecycleState.Ready
            or ArtifactLifecycleState.Executing
            or ArtifactLifecycleState.Completed;
    }
}
