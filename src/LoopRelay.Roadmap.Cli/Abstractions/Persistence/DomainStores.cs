using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;

namespace LoopRelay.Roadmap.Cli.Abstractions.Persistence;

internal interface IDecisionLedgerStore
{
    Task<string> AppendAsync(DecisionLedgerEntry entry);

    Task<string> NextDecisionIdAsync();

    Task<string> LastDecisionIdAsync();
}

internal interface IRoadmapStateStore
{
    Task SaveAsync(RoadmapStateDocument document);

    Task<RoadmapStateDocument?> LoadAsync();
}

internal interface IArtifactLifecycleStore
{
    Task<IReadOnlyList<ArtifactLifecycleEntry>> LoadAsync();

    Task UpsertAsync(string path, ArtifactLifecycleState state, string notes = "");

    Task SaveAsync(IReadOnlyList<ArtifactLifecycleEntry> entries);
}

internal interface ISplitFamilyStore
{
    Task<string> WriteAsync(SplitFamily family);

    Task<bool> ExistsForChildAsync(string childEpicPath);
}

internal interface IExecutionPreparationManifestStore
{
    Task<ExecutionPreparationManifest> LoadAsync();

    Task SaveAsync(ExecutionPreparationManifest manifest);
}

internal interface ISelectionProvenanceManifestStore
{
    Task<SelectionProvenanceManifest> LoadAsync();

    Task SaveAsync(SelectionProvenanceManifest manifest);
}

internal interface IProjectionManifestStore
{
    Task<ProjectionManifest> LoadAsync();

    Task SaveAsync(ProjectionManifest manifest);

    Task UpsertAsync(ProjectionManifestEntry entry);
}

internal interface ITransitionJournalStore
{
    Task AppendAsync(TransitionJournalRecord record);
}
