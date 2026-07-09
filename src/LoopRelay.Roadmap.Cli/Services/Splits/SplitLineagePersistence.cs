using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Models.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Services.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Persistence;

namespace LoopRelay.Roadmap.Cli.Services.Splits;

internal sealed class SplitLineagePersistence(
    RoadmapArtifacts _artifacts,
    IArtifactLifecycleStore _lifecycleStore,
    ISplitFamilyStore _splitFamilyStore,
    IWorkflowPersistenceCoordinator? workflowCoordinator = null)
{
    private readonly IWorkflowPersistenceCoordinator _workflowCoordinator =
        workflowCoordinator ?? NullWorkflowPersistenceCoordinator.Instance;

    public async Task PersistAsync(
        BundleExtractionResult childBundle,
        string sourcePrompt,
        string projectionPath,
        SplitFamily family)
    {
        string manifestPath = BundleManifestWriter.DefaultManifestPath(childBundle.Files);
        string manifestContent = BundleManifestWriter.Render(sourcePrompt, projectionPath, childBundle, "Valid");
        var staging = new RetainedArtifactStagingArea(_artifacts);

        foreach (ExtractedBundleFile child in childBundle.Files)
        {
            await staging.StageWriteAsync(child.Path, child.Content);
        }

        await staging.StageWriteAsync(manifestPath, manifestContent);

        try
        {
            await _workflowCoordinator.ExecuteAsync(
                _artifacts.Repository,
                WorkflowPersistenceUnit.SplitLineageChildArtifactsLifecycle,
                family.FamilyId,
                async _ =>
                {
                    await staging.CommitAsync();
                    foreach (ExtractedBundleFile child in childBundle.Files)
                    {
                        await _lifecycleStore.UpsertAsync(
                            child.Path,
                            ArtifactLifecycleState.Draft,
                            "Validated split child epic.");
                    }

                    await _splitFamilyStore.WriteAsync(family);
                });
        }
        catch
        {
            staging.Rollback();
            throw;
        }
    }
}
