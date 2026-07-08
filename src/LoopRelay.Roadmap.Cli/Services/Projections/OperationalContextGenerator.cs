using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.ArtifactManagement;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;

namespace LoopRelay.Roadmap.Cli.Services.Projections;

internal sealed class OperationalContextGenerator(
    RoadmapArtifacts artifacts,
    ArtifactLifecycleStore lifecycleStore,
    ExecutionPreparationProvenanceService provenanceService)
{
    private readonly RoadmapArtifacts _artifacts = artifacts;
    private readonly ArtifactLifecycleStore _lifecycleStore = lifecycleStore;
    private readonly ExecutionPreparationProvenanceService _provenanceService = provenanceService;
    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string activeEpic = await _artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        IReadOnlyList<string> specs = await _provenanceService.RequireFreshMilestoneSpecPathsAsync(cancellationToken);
        if (specs.Count == 0)
        {
            throw new RoadmapStepException("Cannot generate operational context without milestone specs.");
        }

        string ledger = await _artifacts.ReadAsync(RoadmapArtifactPaths.DecisionLedgerJson) ?? "No roadmap decisions recorded.";
        IReadOnlyList<ArtifactLifecycleEntry> lifecycle = await _lifecycleStore.LoadAsync();

        var lines = new List<string>
        {
            "# Roadmap Operational Context",
            string.Empty,
            "## Active Epic",
            string.Empty,
            activeEpic,
            string.Empty,
            "## Ordered Milestone Specs",
            string.Empty,
        };

        foreach (string spec in specs.Order(StringComparer.Ordinal))
        {
            lines.Add($"### {spec}");
            lines.Add(string.Empty);
            lines.Add(await _artifacts.ReadRequiredAsync(spec));
            lines.Add(string.Empty);
        }

        lines.AddRange(
        [
            "## Relevant Roadmap Decisions",
            string.Empty,
            ledger,
            string.Empty,
            "## Artifact Lifecycle",
            string.Empty,
            "| Path | State |",
            "|---|---|",
        ]);

        foreach (ArtifactLifecycleEntry entry in lifecycle)
        {
            lines.Add($"| {entry.Path} | {entry.State} |");
        }

        string content = EnsureNoRawProjectContext(string.Join(Environment.NewLine, lines) + Environment.NewLine);
        await _artifacts.WriteAsync(RoadmapArtifactPaths.OperationalContext, content);
        await _provenanceService.RecordOperationalContextAsync(content, cancellationToken);
        await _lifecycleStore.UpsertAsync(RoadmapArtifactPaths.OperationalContext, ArtifactLifecycleState.Ready);
        return content;
    }

    private static string EnsureNoRawProjectContext(string content)
    {
        if (content.Contains("<!-- BEGIN PROJECT-CONTEXT FILE:", StringComparison.Ordinal))
        {
            throw new RoadmapStepException("Operational context contains raw Project Context markers.");
        }

        return content;
    }
}
