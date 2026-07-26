using System.Text.Json;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

/// <summary>
/// PERF-13 (W4-3): the feature-effect executor used to load the full nine-table canonical
/// persistence snapshot and then filter it in memory to the handful of products a transition
/// produces. These tests exercise the executor's public <c>ITransitionEffectIntentExecutor</c>
/// entry point end to end -- through the same durable effect ledger production code drives it
/// through -- and prove the swap to a keyed store read left the "re-observe committed products"
/// outcome unchanged: same evidence content whether the produced product is already committed,
/// missing, or committed only after an earlier execution already ran.
/// </summary>
public sealed class CanonicalFeatureEffectExecutorProductsReadTests
{
    [Fact]
    public async Task Produced_product_already_committed_is_re_observed_into_effect_evidence()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        (WorkflowTransitionDefinition transition, EffectDefinition effect) = SelectEvaluationIntentTransition();
        await store.UpsertProductAsync(new ProductRecord(
            ProductIdentity.EvaluationIntent,
            WorkflowIdentity.EvalRoadmap,
            transition.Identity,
            [],
            "repository-owned",
            "canonical",
            [".agents/evaluation-intent.md"],
            "causal-hash-evaluation-intent",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            ["evaluation-intent.md"]));

        string content = await ExecuteAndCaptureEvidenceAsync(repository, store, transition, effect);

        Assert.Equal("Products: EvaluationIntent", ExtractProductsLine(content));
    }

    [Fact]
    public async Task Produced_product_with_no_committed_row_is_omitted_without_failing_the_effect()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        (WorkflowTransitionDefinition transition, EffectDefinition effect) = SelectEvaluationIntentTransition();
        // Deliberately do not commit EvaluationIntent: the transition declares it as a produced
        // product, but nothing has persisted it yet.

        string content = await ExecuteAndCaptureEvidenceAsync(repository, store, transition, effect);

        // Same rendering the in-memory Where-filter over the old full snapshot produced for an
        // uncommitted product: the identity is silently absent rather than causing a failure.
        Assert.Equal("Products: ", ExtractProductsLine(content));
    }

    [Fact]
    public async Task Read_reflects_a_product_committed_after_an_earlier_execution_already_ran()
    {
        // Proves the "re-observe committed products" contract: the executor must not cache, hoist,
        // or otherwise remember the first execution's (empty) read. A second, independent execution
        // against the same durable store must see the product committed in between.
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        (WorkflowTransitionDefinition transition, EffectDefinition effect) = SelectEvaluationIntentTransition();

        string before = await ExecuteAndCaptureEvidenceAsync(repository, store, transition, effect);
        Assert.Equal("Products: ", ExtractProductsLine(before));

        await store.UpsertProductAsync(new ProductRecord(
            ProductIdentity.EvaluationIntent,
            WorkflowIdentity.EvalRoadmap,
            transition.Identity,
            [],
            "repository-owned",
            "canonical",
            [".agents/evaluation-intent.md"],
            "causal-hash-evaluation-intent",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            ["evaluation-intent.md"]));

        string after = await ExecuteAndCaptureEvidenceAsync(repository, store, transition, effect);

        Assert.Equal("Products: EvaluationIntent", ExtractProductsLine(after));
    }

    /// <summary>
    /// Drives <see cref="CanonicalFeatureEffectExecutor.ExecuteAsync(CanonicalCausalContext, EffectIdentity, CancellationToken)"/>
    /// through the real durable effect ledger, the same way production code reaches it: a
    /// "canonical-transition-effect:{effect}" work item is planned and the standard
    /// <see cref="EffectWorker"/> leases, starts, and executes it. Returns the content of the
    /// local-verification evidence file the executor schedules as a durable filesystem-write
    /// child effect, which embeds the re-observed product identities.
    /// </summary>
    private static async Task<string> ExecuteAndCaptureEvidenceAsync(
        Repository repository,
        CanonicalWorkflowPersistenceStore store,
        WorkflowTransitionDefinition transition,
        EffectDefinition effect)
    {
        var executor = new CanonicalFeatureEffectExecutor(repository, store, CanonicalWorkflowCatalog.Current.Workflows);
        var causality = new CanonicalCausalContext(
            WorkspaceIdentity.New(),
            RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(),
            AttemptIdentity.New());
        var effectWorkStore = new CanonicalEffectWorkStore(repository);
        await effectWorkStore.AppendPlanAsync([CreateParentIntent(causality, effect.Identity)], CancellationToken.None);
        var worker = new EffectWorker(
            $"test-worker-{Guid.NewGuid():N}",
            effectWorkStore,
            new EffectExecutorRegistry(
            [
                new TransitionEffectExecutorAdapter(
                    executor,
                    TransitionalFeatureEffectExecutorKeys.For(effect.Identity.Value),
                    effect.Identity),
            ]),
            new TransitionalFeatureEffectReconciler(effectWorkStore),
            TimeSpan.FromMinutes(5));

        EffectWorkerResult result = await worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal(1, result.Succeeded);

        IReadOnlyList<EffectWorkItem> plan = await effectWorkStore.ReadPlanAsync(causality.TransitionRun, CancellationToken.None);
        EffectWorkItem write = Assert.Single(plan, item => item.Intent.Executor == WorkspaceEffectExecutorKeys.FilesystemWrite);
        FilesystemWriteEffectPayload payload = JsonSerializer.Deserialize<FilesystemWriteEffectPayload>(
            write.Intent.TypedPayload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal($".LoopRelay/evidence/local-verification/{transition.Identity}.md", payload.RelativePath);
        return payload.Content;
    }

    private static string ExtractProductsLine(string content) =>
        content
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Single(line => line.StartsWith("Products:", StringComparison.Ordinal));

    private static (WorkflowTransitionDefinition Transition, EffectDefinition Effect) SelectEvaluationIntentTransition()
    {
        WorkflowDefinition workflow = CanonicalWorkflowCatalog.Current.Workflows
            .Single(candidate => candidate.Identity == WorkflowIdentity.EvalRoadmap);
        WorkflowTransitionDefinition transition = workflow.Transitions
            .Single(candidate => candidate.Identity.Value == "SelectEvaluationIntent");
        EffectDefinition effect = transition.Effects.Single();
        return (transition, effect);
    }

    private static EffectIntent CreateParentIntent(CanonicalCausalContext causality, EffectIdentity effectIdentity) =>
        new(
            EffectIntentIdentity.New(),
            causality,
            $"test-canonical-transition-effect:{effectIdentity.Value}",
            TransitionalFeatureEffectExecutorKeys.For(effectIdentity.Value),
            "1",
            new EffectTargetDescriptor("Effect", effectIdentity.Value, "{}"),
            "{}",
            "test-hash",
            0,
            [],
            EffectRequiredness.BlockingLocal,
            new EffectCondition("test-precondition", "{}"),
            new EffectCondition("test-postcondition", "{}"),
            "test-reconciliation-policy",
            $"test-parent:{causality.TransitionRun.Value}:{effectIdentity.Value}",
            DateTimeOffset.UtcNow);

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("cc-cli-effect-products-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }
}
