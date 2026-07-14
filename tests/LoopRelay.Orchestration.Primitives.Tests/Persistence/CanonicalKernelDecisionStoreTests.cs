using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Persistence;

namespace LoopRelay.Orchestration.Tests.Persistence;

public sealed class CanonicalKernelDecisionStoreTests
{
    [Fact]
    public async Task Decision_boundary_round_trips_all_causal_and_selection_evidence()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-kernel-decisions").FullName;
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
        var store = new CanonicalKernelDecisionStore(repository);
        DateTimeOffset recorded = DateTimeOffset.UtcNow;
        var decision = new KernelDecisionFact(
            KernelDecisionIdentity.New(), "catalog-sha256", "snapshot-sha256",
            RunIdentity.New(), WorkflowInstanceIdentity.New(), TransitionRunIdentity.New(), AttemptIdentity.New(),
            ["eligible-transition"], ["rejected-transition:GateRejected:evidence-1"],
            "eligible-transition", WorkflowStopReason.TransitionCompleted,
            ["gate:evidence-1", "effect:receipt-1"], recorded);

        await store.AppendAsync(decision, CancellationToken.None);
        await store.AppendAsync(decision, CancellationToken.None);

        KernelDecisionFact restored = Assert.Single(await store.ReadAsync());
        Assert.Equal(decision.Identity, restored.Identity);
        Assert.Equal(decision.CatalogIdentity, restored.CatalogIdentity);
        Assert.Equal(decision.SnapshotIdentity, restored.SnapshotIdentity);
        Assert.Equal(decision.RootRun, restored.RootRun);
        Assert.Equal(decision.WorkflowInstance, restored.WorkflowInstance);
        Assert.Equal(decision.TransitionRun, restored.TransitionRun);
        Assert.Equal(decision.Attempt, restored.Attempt);
        Assert.Equal(decision.EligibleAlternatives, restored.EligibleAlternatives);
        Assert.Equal(decision.RejectedAlternatives, restored.RejectedAlternatives);
        Assert.Equal(decision.SelectedAction, restored.SelectedAction);
        Assert.Equal(decision.Outcome, restored.Outcome);
        Assert.Equal(decision.Evidence, restored.Evidence);
        Assert.Equal(recorded, restored.RecordedAt, TimeSpan.FromMilliseconds(1));
    }
}
