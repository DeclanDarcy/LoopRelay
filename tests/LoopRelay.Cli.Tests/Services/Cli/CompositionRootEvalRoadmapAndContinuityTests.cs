using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Application.Contracts;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Planning;
using LoopRelay.Cli.Tests.Models;
using LoopRelay.Cli.Tests.Services.Agents;
using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Services.Evaluation;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class CompositionRootEvalRoadmapAndContinuityTests : CompositionRootTestBase
{
    [Fact]
    public async Task EvalRoadmap_workflow_transitions_run_through_canonical_runtime()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-eval-full-runtime").FullName;
        await WriteAsync(repo, ".agents/evals/e1.md", "# Eval Intent\n\nEvaluate the capability.");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        EnqueueEvalOutput(runtime, "CreateEvalDependencyInventory", "# Dependency Inventory");
        EnqueueEvalOutput(runtime, "CreateEvalHypothesisInventory", "# Hypothesis Inventory");
        EnqueueEvalOutput(runtime, "CreateArchitecturalCatalog", "# Architectural Catalog");
        EnqueueEvalOutput(runtime, "CreateEvalDag", "# Eval DAG");
        EnqueueEvalOutput(runtime, "CreateNextEpicRoadmap", "# Next Epic Roadmap");
        EnqueueEvalOutput(runtime, "CreateNextEpicImplementationSpec", ValidEvalActiveEpic());
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("Generate Milestone Deep Dives For Epic", prompt, StringComparison.Ordinal);
            Assert.Contains("Active Epic", prompt, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "specs"));
            File.WriteAllText(
                Path.Combine(repo, ".agents", "specs", "m1.md"),
                "# Milestone 1\n\nImplement the capability.");
            return new AgentTurnResult(6, AgentTurnState.Completed, "wrote milestone specs", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult select = await RunEvalAsync(composition, "Evaluation Foundation", "SelectEvaluationIntent");
        TransitionRuntimeResult dependencies = await RunEvalAsync(composition, "Dependency Inventory", "CreateEvalDependencyInventory");
        TransitionRuntimeResult hypotheses = await RunEvalAsync(composition, "Hypothesis Inventory", "CreateEvalHypothesisInventory");
        TransitionRuntimeResult catalog = await RunEvalAsync(composition, "Architectural Catalog", "CreateEvalArchitecturalCatalog");
        TransitionRuntimeResult dag = await RunEvalAsync(composition, "Eval DAG", "CreateEvalDag");
        TransitionRuntimeResult roadmap = await RunEvalAsync(composition, "Next Epic Roadmap", "CreateNextEpicRoadmap");
        TransitionRuntimeResult epic = await RunEvalAsync(composition, "Active Epic Preparation", "CreateNextEpicActiveEpic");
        // GenerateMilestoneDeepDivesForEpic declares `.agents/epic.md` as a clean-input
        // surface, so the prepared epic just produced must be committed before it is read.
        await GitWorkspace.CommitAgentsInputsAsync(repo);
        TransitionRuntimeResult specs = await RunEvalAsync(composition, "Milestone Specification", "GenerateMilestoneDeepDivesForEpic");
        TransitionRuntimeResult verify = await RunEvalAsync(composition, "Workflow Completion", "VerifyPlanEntryContract");

        Assert.All(
            [select, dependencies, hypotheses, catalog, dag, roadmap, epic, specs, verify],
            result => Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation));
        Assert.Equal(7, runtime.OneShotCalls.Count);
        Assert.Equal(ValidEvalActiveEpic(), await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "epic.md")));
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "specs", "m1.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.WorkflowStates, state =>
            state.Workflow == WorkflowIdentity.EvalRoadmap &&
            state.State == WorkflowResolutionState.Completed &&
            state.CurrentStage is null);
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.PreparedEpic &&
            product.StorageRepresentations.Contains(EvaluationArtifactPaths.PreparedEpic));
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.MilestoneSpecificationSet &&
            product.StorageRepresentations.Contains(".agents/specs/m1.md"));
    }

    [Fact]
    public async Task Execute_handoff_resumes_exact_implementation_thread_and_restores_slice_facts_after_restart()
    {
        (string repo, Repository repository, FakeAgentRuntime runtime, FakeProcessRunner process) = await PrepareExecuteContinuityCaseAsync(
            "cc-cli-unified-execute-warm-restart");
        await WriteAsync(repo, "README.md", "# Canonical Capability\n\nUse exact repository bytes.");
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "# Decisions\n\nCreate src/feature.cs.", AgentTokenUsage.Zero)));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Null(spec.ResumeThreadId);
            Assert.Contains("# Execution Milestone Context", prompt, StringComparison.Ordinal);
            Assert.Contains("- [ ] Create feature.", prompt, StringComparison.Ordinal);
            Assert.Contains("# Repository README Context", prompt, StringComparison.Ordinal);
            Assert.Contains("# Canonical Capability", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("integration is not wired", prompt, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.Combine(repo, "src"));
            File.WriteAllText(Path.Combine(repo, "src", "feature.cs"), "feature\n");
            return new AgentTurnResult(1, AgentTurnState.Completed, "implemented", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot first = LoopRelayCompositionRoot.CreateForTests(repository, runtime, process);
        await RunPlanAsync(first, "Workflow Completion", "VerifyExecuteEntryContract");
        await RunExecuteAsync(first, "Execution Readiness", "VerifyExecutionReadiness");
        await RunExecuteAsync(first, "Implementation Planning", "GenerateDecision");
        TransitionRuntimeResult implementation = await RunExecuteAsync(
            first, "Implementation", "ExecuteImplementationSlice");
        ExecutionWarmSessionContinuity checkpoint = Assert.IsType<ExecutionWarmSessionContinuity>(
            await new CanonicalCheckpointStore(repository).ReadAsync<ExecutionWarmSessionContinuity>(CanonicalCheckpointKeys.ExecutionWarmSession, CancellationToken.None));
        await first.DisposeAsync();

        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, _, _) =>
        {
            Assert.Equal(checkpoint.ProviderThreadId, spec.ResumeThreadId);
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "handoffs"));
            File.WriteAllText(Path.Combine(repo, ".agents", "handoffs", "handoff.md"), "# Handoff\n\nDone.\n");
            return new AgentTurnResult(2, AgentTurnState.Completed, "handoff", AgentTokenUsage.Zero);
        }));
        await using LoopRelayCompositionRoot restarted = LoopRelayCompositionRoot.CreateForTests(repository, runtime, process);
        TransitionRuntimeResult handoff = await RunExecuteAsync(
            restarted, "Execution Continuity", "GenerateHandoff");

        Assert.Equal(RuntimeOutcomeKind.Completed, implementation.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, handoff.Outcome);
        Assert.Equal(checkpoint.ProviderThreadId, runtime.OpenedSpecs[^1].ResumeThreadId);
        ExecutionWarmSessionContinuity after = Assert.IsType<ExecutionWarmSessionContinuity>(
            await new CanonicalCheckpointStore(repository).ReadAsync<ExecutionWarmSessionContinuity>(CanonicalCheckpointKeys.ExecutionWarmSession, CancellationToken.None));
        Assert.True(after.HandoffCompleted);
        Assert.Equal(checkpoint.SliceBaseline.ExecutionSliceId, after.SliceBaseline.ExecutionSliceId);
        await AssertCanonicalRecoveryPlanAsync(
            repository, $"execute:{checkpoint.ProviderThreadId}", CanonicalRecoveryAction.ResumeSession);
    }

    /// <summary>
    /// PERF-11 (W4-4) canary. <c>CompositionPromptExecutionOwner.ResolveCausalityAsync</c> no longer
    /// re-derives an attempt's causal context from the durable store; it answers from the
    /// authorization the dispatch already carries. This is the standing guard on the premise that
    /// made that substitution safe, and it stays live because those values feed the durable causal
    /// chain written into evidence.
    /// <para>
    /// It observes the real returned value rather than a reconstruction of it. The plan-materialise
    /// path is one of the nine <c>ResolveCausalityAsync</c> call sites
    /// (<c>CompositionPromptExecutionOwner.cs:1007</c>): it hands the resolved context straight to
    /// <c>DurableFilesystemWriteEffectPlanner</c>, which persists it into the effect intent.
    /// Reading that intent back out of the effect ledger therefore yields exactly what
    /// <c>ResolveCausalityAsync</c> returned -- which, after PERF-11, is
    /// <c>CurrentAuthorization.Causality</c> itself.
    /// </para>
    /// <para>
    /// What this does NOT prove: workspace identity. See
    /// <see cref="AssertResolvedCausalityMatchesTheAttemptRowAsync"/>.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Resolved_causality_written_into_the_effect_ledger_matches_the_durable_attempt_row()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-causality-plan").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(repo), Path = repo };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        // The warm session returns the plan as markdown and performs no tool write, so the executor
        // materialises it through the durable filesystem-write planner -- the call site above.
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => new AgentTurnResult(
            0,
            AgentTurnState.Completed,
            """
            # Executable Plan

            ## Milestone 1 — Implement Capability

            Implement the bounded repository capability, preserve its independent verifier, run the verifier,
            and record the exact acceptance result before checking the milestone completion item.
            """,
            AgentTokenUsage.Zero)));
        await using LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult write = await RunPlanAsync(composition, "Planning", "WriteExecutablePlan");

        Assert.Equal(RuntimeOutcomeKind.Completed, write.Outcome);
        IReadOnlyList<EffectWorkItem> planned = await new CanonicalEffectWorkStore(repository).ReadPlanAsync(
            write.TransitionRun ?? throw new InvalidOperationException("Attempt has no transition run identity."),
            CancellationToken.None);
        EffectWorkItem materialise = Assert.Single(
            planned,
            item => item.Intent.SemanticOperationKey ==
                $"candidate-filesystem:write:{OrchestrationArtifactPaths.Plan}");

        await AssertResolvedCausalityMatchesTheAttemptRowAsync(repository, materialise.Intent.Causality);
    }

    [Fact]
    public async Task GenerateDecision_honors_the_composed_observers_storage_verdict_over_a_fresh_default()
    {
        // The decision-session scope resolver used to construct its own bare RepositoryObserver
        // (defaulting to FileSystemStorageVerifier) instead of consulting the composition's own
        // observer whenever the caller omitted one - which every production call site did. A
        // call-count spy can only prove the plumbing ran through the composed instance; it cannot
        // prove the two observers ever disagree about anything a caller could act on. They do: for
        // a missing canonical database, FileSystemStorageVerifier.VerifyAsync special-cases it to
        // UsableAuthority=true with Health left at its default Healthy (see the bottom of
        // RepositoryObserver.cs), so IsUnusable is false; WorkspaceStorageVerifierAdapter - what
        // production actually composes - delegates to WorkspaceStorageInspector, which reports
        // Health=ActionRequired for a missing database (WorkspaceStorageInspector.VerifyAsync), so
        // UsableAuthority=false and BlockingConditions is non-empty, making IsUnusable true.
        // DecisionSessionScopeResolver.ResolveAsync throws exactly when IsUnusable is true
        // (DecisionSessionScopeResolver.cs), so the identical repository reaches opposite verdicts
        // at this gate depending on which observer answers it.
        //
        // That specific divergence cannot be reproduced here by deleting the database file: the
        // very first durable write this attempt performs (persisting the attempt-started row,
        // before the prompt is ever dispatched) reopens the database in create mode and recreates
        // a fresh, healthy schema, so by the time GenerateDecision's own storage check runs the
        // file exists again and every observer agrees. To isolate the gate this test protects
        // (rather than merely "was the composed observer consulted at all"), the injected verifier
        // below reports a blocking condition directly, reproducing the same disagreement in
        // IsUnusable without touching the database file, and without disturbing
        // UsableAuthority/Health - which the "Implementation Planning" stage's own
        // ExecutionReadiness requirement needs to observe canonical products earlier in this same
        // attempt at all (RepositoryObserver.ObserveAsync only short-circuits canonical persistence
        // projection on UsableAuthority, never on IsUnusable).
        (string repo, Repository repository, FakeAgentRuntime runtime, FakeProcessRunner process) =
            await PrepareExecuteContinuityCaseAsync("cc-cli-unified-decision-scope-storage-verdict");
        LoopRelayCompositionRoot first = LoopRelayCompositionRoot.CreateForTests(repository, runtime, process);
        await RunPlanAsync(first, "Workflow Completion", "VerifyExecuteEntryContract");
        await RunExecuteAsync(first, "Execution Readiness", "VerifyExecutionReadiness");
        await first.DisposeAsync();

        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "# Decisions\n\nDo the thing.", AgentTokenUsage.Zero)));
        await using LoopRelayCompositionRoot restarted = LoopRelayCompositionRoot.CreateForTests(
            repository, runtime, process, new BlockedAuthorityStorageVerifier());

        TransitionRuntimeResult decision = await RunExecuteAsync(
            restarted, "Implementation Planning", "GenerateDecision");

        // If DecisionSessionScopeResolver still consulted a freshly constructed default
        // RepositoryObserver instead of the composed one, it would see this repository's real,
        // healthy, un-blocked database and complete normally instead of failing with this
        // explanation.
        Assert.Equal(RuntimeOutcomeKind.Failed, decision.Outcome);
        Assert.Equal(TransitionDurableState.Failed, decision.DurableState);
        Assert.Contains(
            "Execute continuity scope cannot be resolved from blocked storage authority.",
            decision.Explanation,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_prompt_transition_renders_generated_prompt_asset_before_executor_integration()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-prompt").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = LoopRelayCompositionRoot.CreateForTests(repository);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            Request(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Planning"),
                new WorkflowTransitionIdentity("WriteExecutablePlan")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Contains("Prompt execution integration is not wired", result.Explanation, StringComparison.Ordinal);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        CanonicalPromptAsset asset = CanonicalPromptAssetCatalog.GetByPromptIdentity("WritePlan");
        CanonicalRenderedPromptRecord fact = Assert.Single(
            await new CanonicalWorkflowPersistenceStore(repository).ReadRenderedPromptsAsync());
        Assert.Equal(asset.PromptIdentity, fact.PromptIdentity);
        Assert.Equal(asset.SourceHash, fact.TemplateSourceHash);
    }

    [Fact]
    public async Task TraditionalRoadmap_invalid_prepared_epic_fails_with_recovery_marker()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-traditional-invalid-epic").FullName;
        await WriteProjectContextAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        EnqueueTraditionalOutput(runtime, "BootstrapRoadmapCompletionContext", "# Roadmap Completion Context");
        EnqueueTraditionalOutput(runtime, "SelectStrategicInitiative", "# Strategic Initiative Selection");
        EnqueueTraditionalOutput(runtime, "CreateNewEpic", "# Not An Epic\n\nThis cannot satisfy the prepared epic contract.");
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult context = await RunTraditionalAsync(
            composition,
            "Roadmap Context",
            "BootstrapRoadmapCompletionContext");
        TransitionRuntimeResult selection = await RunTraditionalAsync(
            composition,
            "Strategic Initiative Selection",
            "SelectStrategicInitiative");
        TransitionRuntimeResult epic = await RunTraditionalAsync(
            composition,
            "Epic Preparation",
            "CreateEpic");

        Assert.Equal(RuntimeOutcomeKind.Completed, context.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, selection.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Failed, epic.Outcome);
        Assert.Equal(TransitionDurableState.Failed, epic.DurableState);
        Assert.Contains("# Epic:", epic.Explanation, StringComparison.Ordinal);
        await composition.DisposeAsync();
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        ProductRecord proposed = Assert.Single(
            snapshot.Products,
            product => product.Identity == ProductIdentity.PreparedEpic);
        Assert.Equal(ProductLifecycle.Proposed, proposed.Lifecycle);
        Assert.NotEqual(ProductValidationState.Valid, proposed.ValidationState);
        Assert.Contains(snapshot.TransitionRuns, run =>
            run.Workflow == WorkflowIdentity.TraditionalRoadmap &&
            run.Transition == new WorkflowTransitionIdentity("CreateEpic") &&
            run.State == TransitionDurableState.Failed);
    }
}
