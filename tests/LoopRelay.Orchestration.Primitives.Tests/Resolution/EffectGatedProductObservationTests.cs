using System.Security.Cryptography;
using System.Text;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Resolution;

public sealed class EffectGatedProductObservationTests
{
    [Fact]
    public async Task System_product_is_not_gate_usable_while_its_required_publication_is_unsettled()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-effect-gated-product").FullName;
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        string workspace = await persistence.ReadWorkspaceIdentityAsync();
        CanonicalCausalContext causality = new(new(workspace), RunIdentity.New(),
            WorkflowInstanceIdentity.New(), TransitionRunIdentity.New(), AttemptIdentity.New());
        await persistence.UpsertProductAsync(new ProductRecord(
            ProductIdentity.ExecutionReadiness, WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity("VerifyExecuteEntryContract"), [WorkflowIdentity.Execute],
            "canonical", "execution-readiness.v1", [".agents/execution-readiness.json"],
            causality.Attempt.Value, ProductFreshness.Fresh, ProductValidationState.Valid,
            ProductLifecycle.Active, ["evidence:readiness"]));
        string payload = "{}";
        string payloadHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        EffectIntent intent = new(EffectIntentIdentity.New(), causality,
            "publication:execution-readiness:parent-push", GitEffectExecutorKeys.ParentRepositoryPush, "1",
            new EffectTargetDescriptor("git", ".", payload), payload, payloadHash, 0, [],
            EffectRequiredness.RequiredAsync, new EffectCondition("ready", payload),
            new EffectCondition("remote-updated", payload), "git-reconcile", "readiness-push", DateTimeOffset.UtcNow);
        await new CanonicalEffectWorkStore(repository).AppendPlanAsync([intent], CancellationToken.None);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(path);

        ObservedProduct readiness = Assert.Single(observation.Products,
            item => item.Product.Identity == ProductIdentity.ExecutionReadiness);
        Assert.False(readiness.GateUsable);
        Assert.Contains(observation.LifecycleRows,
            item => item.Identity == $"Effect:{intent.Identity.Value}" && item.State == EffectLifecycle.Planned.ToString());
    }
}
