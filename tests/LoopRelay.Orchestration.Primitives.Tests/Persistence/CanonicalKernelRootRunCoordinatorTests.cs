using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Persistence;

public sealed class CanonicalKernelRootRunCoordinatorTests
{
    [Fact]
    public async Task Single_nonterminal_root_is_reentered_and_multiple_roots_fail_ambiguous()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-kernel-root").FullName;
        try
        {
            var repository = new Repository { Id = Guid.NewGuid(), Name = "fixture", Path = root };
            var store = new CanonicalWorkflowPersistenceStore(repository);
            var coordinator = new CanonicalKernelRootRunCoordinator(store);
            CanonicalWorkflowCatalogSnapshot catalog = CanonicalWorkflowCatalog.Current;

            KernelRootEntry created = await coordinator.EnterAsync("workspace", "chain", "mode", catalog,
                CancellationToken.None);
            KernelRootEntry reentered = await coordinator.EnterAsync("workspace", "chain", "mode", catalog,
                CancellationToken.None);

            Assert.Equal(KernelRootEntryKind.Created, created.Kind);
            Assert.Equal(KernelRootEntryKind.Reentered, reentered.Kind);
            Assert.Equal(created.Run, reentered.Run);

            await store.UpsertRunAsync(new RunRecord("run_conflict", "workspace", "chain", "mode", "Active",
                DateTimeOffset.UtcNow, null, null, "", catalog.Identity, catalog.SemanticVersion));
            KernelRootEntry ambiguous = await coordinator.EnterAsync("workspace", "chain", "mode", catalog,
                CancellationToken.None);
            Assert.Equal(KernelRootEntryKind.Ambiguous, ambiguous.Kind);
            Assert.Null(ambiguous.Run);
            Assert.Equal(2, ambiguous.Evidence.Count);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
