using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Persistence;

public enum KernelRootEntryKind
{
    Created,
    Reentered,
    Ambiguous,
    RecoveryRequired,
}

public sealed record KernelRootEntry(
    KernelRootEntryKind Kind,
    RunIdentity? Run,
    DateTimeOffset StartedAt,
    string Explanation,
    IReadOnlyList<string> Evidence);

public sealed class CanonicalKernelRootRunCoordinator(CanonicalWorkflowPersistenceStore _store)
{
    public async Task<KernelRootEntry> EnterAsync(
        string workspaceIdentity,
        string chainIdentity,
        string invocationMode,
        CanonicalWorkflowCatalogSnapshot catalog,
        CancellationToken cancellationToken)
    {
        RunRecord[] active = (await _store.ReadRunsAsync(cancellationToken))
            .Where(item => item.WorkspaceId == workspaceIdentity && item.ChainIdentity == chainIdentity &&
                           item.Status == "Active").ToArray();
        if (active.Length > 1)
            return new(KernelRootEntryKind.Ambiguous, null, DateTimeOffset.UtcNow,
                "More than one nonterminal root run exists for the requested workspace and chain.",
                active.Select(item => item.RunId).ToArray());
        if (active.Length == 1)
        {
            RunRecord existing = active[0];
            CatalogResolution resolution = new CanonicalWorkflowCatalogRegistry([catalog])
                .Resolve(existing.CatalogIdentity, existing.CatalogVersion);
            return resolution.Kind == CatalogResolutionKind.Available
                ? new(KernelRootEntryKind.Reentered, new(existing.RunId), existing.StartedAt,
                    "Re-entered the single active root run with its exact catalog.", [existing.RunId])
                : new(KernelRootEntryKind.RecoveryRequired, new(existing.RunId), existing.StartedAt,
                    resolution.Explanation, [existing.RunId, existing.CatalogIdentity, existing.CatalogVersion]);
        }

        RunIdentity run = RunIdentity.New();
        DateTimeOffset started = DateTimeOffset.UtcNow;
        await _store.UpsertRunAsync(new RunRecord(run.Value, workspaceIdentity, chainIdentity, invocationMode,
            "Active", started, null, null, string.Empty, catalog.Identity, catalog.SemanticVersion), cancellationToken);
        return new(KernelRootEntryKind.Created, run, started,
            "Created a new authorized root run because no nonterminal lineage existed.", [run.Value]);
    }
}
