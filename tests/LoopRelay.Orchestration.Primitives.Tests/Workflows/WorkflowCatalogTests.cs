using LoopRelay.Orchestration.Workflows;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Persistence;

namespace LoopRelay.Orchestration.Tests.Workflows;

public sealed class WorkflowCatalogTests
{
    [Fact]
    public async Task Root_runs_and_workflow_instances_persist_exact_catalog_identity_and_version()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-catalog-spine").FullName;
        try
        {
            var repository = new Repository { Id = Guid.NewGuid(), Name = "fixture", Path = root };
            var store = new CanonicalWorkflowPersistenceStore(repository);
            CanonicalWorkflowCatalogSnapshot catalog = CanonicalWorkflowCatalog.Current;
            await store.UpsertRunAsync(new RunRecord("run_catalog", "workspace_catalog", "chain", "test", "Active",
                DateTimeOffset.UtcNow, null, null, "", catalog.Identity, catalog.SemanticVersion));
            var recorder = new CanonicalWorkflowInstanceRecorder(store, catalog);
            LoopRelay.Core.Models.Identity.WorkflowInstanceIdentity first = await recorder.BeginInstanceAsync(
                new LoopRelay.Core.Models.Identity.RunIdentity("run_catalog"),
                WorkflowIdentity.Plan, CancellationToken.None);
            LoopRelay.Core.Models.Identity.WorkflowInstanceIdentity resumed = await recorder.BeginInstanceAsync(
                new LoopRelay.Core.Models.Identity.RunIdentity("run_catalog"),
                WorkflowIdentity.Plan, CancellationToken.None);

            RunRecord run = Assert.Single(await store.ReadRunsAsync());
            WorkflowInstanceRecord instance = Assert.Single(await store.ReadWorkflowInstancesAsync());
            Assert.Equal(catalog.Identity, run.CatalogIdentity);
            Assert.Equal(catalog.SemanticVersion, run.CatalogVersion);
            Assert.Equal(catalog.Identity, instance.CatalogIdentity);
            Assert.Equal(catalog.SemanticVersion, instance.CatalogVersion);
            Assert.Equal(first, resumed);
            Assert.Equal(CatalogResolutionKind.Available,
                new CanonicalWorkflowCatalogRegistry([catalog]).Resolve(run.CatalogIdentity, run.CatalogVersion).Kind);
            Assert.Equal(CatalogResolutionKind.RecoveryRequired,
                new CanonicalWorkflowCatalogRegistry([catalog]).Resolve("missing", "1").Kind);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void Current_snapshot_is_single_versioned_authority_with_structurally_derived_publication()
    {
        CanonicalWorkflowCatalogSnapshot catalog = CanonicalWorkflowCatalog.Current;

        Assert.Equal(64, catalog.Identity.Length);
        Assert.Equal(CanonicalWorkflowCatalog.SemanticVersion, catalog.SemanticVersion);
        Assert.Equal(4, catalog.Workflows.Count);
        Assert.Equal(2, catalog.Chains.Count);
        Assert.Same(catalog.Workflows, CanonicalWorkflowCatalog.CreateAll());
        Assert.Same(catalog.Chains, CanonicalWorkflowCatalog.CreateChains());
        Assert.True(WorkflowCatalogValidator.Validate(catalog).IsValid);
        Assert.All(catalog.Workflows.SelectMany(item => item.Transitions), transition =>
        {
            Assert.NotEmpty(transition.ValidatorReferences!);
            Assert.NotNull(transition.PromptContract);
            Assert.NotEmpty(transition.PromptContract!.RuntimeCapabilities);
            Assert.NotEmpty(transition.InteractionCategories!);
            Assert.NotEmpty(transition.RecoveryStrategies!);
            if (transition.RequiredInputProducts.Count > 0) Assert.NotEmpty(transition.InputSurfaces!);
            Assert.All(transition.OutputSurfaces!, surface =>
            {
                Assert.DoesNotContain('\\', surface.Path);
                Assert.DoesNotContain("..", surface.Path, StringComparison.Ordinal);
                if (surface.CommitPolicy == CommitPolicy.BlockingLocal)
                    Assert.Contains(transition.Effects, effect =>
                        effect.Identity.Value.StartsWith("derived-git-commit:", StringComparison.Ordinal) &&
                        effect.Identity.Value.EndsWith(surface.Path, StringComparison.Ordinal));
                if (surface.PushPolicy == PushPolicy.RequiredAsync)
                    Assert.Contains(transition.Effects, effect =>
                        effect.Identity.Value.StartsWith("derived-git-push:", StringComparison.Ordinal) &&
                        effect.Identity.Value.EndsWith(surface.Path, StringComparison.Ordinal));
            });
            Assert.DoesNotContain(transition.Effects, effect =>
                effect.Category == EffectCategory.Git &&
                !effect.Identity.Value.StartsWith("derived-git-", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Invalid_catalog_corpus_collects_stably_ordered_path_qualified_errors()
    {
        CanonicalWorkflowCatalogSnapshot source = CanonicalWorkflowCatalog.Current;
        WorkflowDefinition plan = source.GetWorkflow(WorkflowIdentity.Plan);
        WorkflowTransitionDefinition transition = plan.Transitions[0] with
        {
            PromptContract = null,
            ValidatorReferences = [new(new ValidatorIdentity("unowned"), "unknown-owner")],
            OutputSurfaces = [new("../escape", RepositoryTarget.Workspace,
                SurfaceMutationKind.CreateOrReplaceFile, "", new(new ValidatorIdentity("bad"), "unknown-owner"),
                CommitPolicy.BlockingLocal, PushPolicy.RequiredAsync)],
            Effects = [],
            EligibleSuccessors = [new WorkflowTransitionIdentity("missing"), new WorkflowTransitionIdentity("missing")],
        };
        WorkflowStageDefinition first = plan.Stages[0] with
        {
            AllowedSuccessors = [plan.Stages[0].Identity, plan.Stages[0].Identity],
            Transitions = [new WorkflowTransitionIdentity("dangling")],
        };
        WorkflowTransitionDefinition unsupported = plan.Transitions[1] with
        {
            PromptContract = plan.Transitions[1].PromptContract! with
            {
                RuntimeCapabilities = [new RuntimeCapabilityIdentity("agent.unsupported-fixture")],
            },
            OutputSurfaces = [],
            InputSurfaces = [],
        };
        WorkflowDefinition invalidPlan = plan with
        {
            Transitions = [transition, unsupported, .. plan.Transitions.Skip(2)],
            Stages = [first, .. plan.Stages.Skip(1)],
        };
        var invalid = source with
        {
            Workflows = [invalidPlan, invalidPlan, .. source.Workflows.Where(item => item.Identity != WorkflowIdentity.Plan)],
            Chains = [source.Chains[0], source.Chains[0]],
        };

        WorkflowCatalogValidationResult result = WorkflowCatalogValidator.Validate(invalid);

        Assert.False(result.IsValid);
        Assert.Equal(result.Errors.Order(StringComparer.Ordinal), result.Errors);
        Assert.Contains(result.Errors, item => item.Contains("workflow identities must be unique", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("chain identities must be unique", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("unknown transition 'dangling'", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("prompt asset identity/version", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("runtime capability is unsupported", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("validator owner is unregistered", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("produced products require declared output surfaces", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("required products need complete filesystem input surfaces", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("cannot escape root", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("commit effect was not structurally derived", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("successors are ambiguous", StringComparison.Ordinal));
        Assert.Contains(result.Errors, item => item.Contains("unreachable", StringComparison.Ordinal));
    }

    [Fact]
    public void One_semantic_change_updates_one_stable_obligation_without_renumbering()
    {
        CanonicalWorkflowCatalogSnapshot catalog = CanonicalWorkflowCatalog.Current;
        WorkflowDefinition plan = catalog.GetWorkflow(WorkflowIdentity.Plan);
        WorkflowTransitionDefinition changedTransition = plan.Transitions[0] with
        {
            Purpose = plan.Transitions[0].Purpose + " Versioned semantic change.",
        };
        WorkflowDefinition changedPlan = plan with
        {
            Transitions = [changedTransition, .. plan.Transitions.Skip(1)],
        };
        WorkflowDefinition[] changedWorkflows = catalog.Workflows
            .Select(item => item.Identity == WorkflowIdentity.Plan ? changedPlan : item).ToArray();
        IReadOnlyDictionary<string, string> before = catalog.Obligations.ToDictionary(item => item.Key, item => item.ContentHash);
        IReadOnlyDictionary<string, string> after = CatalogObligationEnumerator
            .Enumerate(changedWorkflows, catalog.Chains).ToDictionary(item => item.Key, item => item.ContentHash);

        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        string changed = Assert.Single(before.Keys, key => before[key] != after[key]);
        Assert.Contains("/transition/Plan/", changed,
            StringComparison.Ordinal);
    }
}
