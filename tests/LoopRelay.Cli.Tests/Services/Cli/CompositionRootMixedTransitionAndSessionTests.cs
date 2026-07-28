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

public sealed class CompositionRootMixedTransitionAndSessionTests : CompositionRootTestBase
{
    [Fact]
    public async Task Plan_warm_session_prompt_success_without_plan_file_fails_product_validation()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-session-missing-plan").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "claimed success", AgentTokenUsage.Zero)));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            Request(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Planning"),
                new WorkflowTransitionIdentity("WriteExecutablePlan")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Equal(TransitionDurableState.Failed, result.DurableState);
        Assert.Contains("completed without `.agents/plan.md`", result.Explanation, StringComparison.Ordinal);
        Assert.Equal(1, runtime.OpenSessions);
        await composition.DisposeAsync();
        Assert.Equal(1, runtime.ClosedSessions);
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

    [Fact]
    public async Task Generate_operational_context_runs_as_deterministic_canonical_artifact_transition()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-operational-context").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan\n\nImplement the capability.");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = LoopRelayCompositionRoot.CreateForTests(repository);

        TransitionRuntimeResult result = await RunPlanAsync(
            composition,
            "Execution Preparation",
            "GenerateOperationalContext");

        Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation);
        Assert.Equal(TransitionDurableState.Completed, result.DurableState);
        Assert.Equal(
            "# Plan\n\nImplement the capability.",
            await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "operational_context.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        ProductRecord product = Assert.Single(
            snapshot.Products,
            product => product.Identity == ProductIdentity.OperationalContext);
        Assert.Equal([OrchestrationArtifactPaths.OperationalContext], product.StorageRepresentations);
        Assert.Contains(
            snapshot.StageStates,
            stage => stage.Workflow == WorkflowIdentity.Plan &&
                stage.Stage == new WorkflowStageIdentity("Execution Preparation") &&
                stage.State == WorkflowResolutionState.Active);
        Assert.Contains(
            snapshot.WorkflowStates,
            state => state.Workflow == WorkflowIdentity.Plan &&
                state.State == WorkflowResolutionState.Resumable &&
                state.CurrentStage == new WorkflowStageIdentity("Execution Preparation"));
        CanonicalTransitionRunRecord run = Assert.Single(snapshot.TransitionRuns,
            item => item.Transition == new WorkflowTransitionIdentity("GenerateOperationalContext"));
        IReadOnlyList<EffectWorkItem> effects = await new CanonicalEffectWorkStore(repository)
            .ReadPlanAsync(new TransitionRunIdentity(run.RunId), CancellationToken.None);
        Assert.Equal(
            [
                "transition-effect:persist-operational-context",
                "filesystem:write:.agents/operational_context.md",
                "filesystem:write:.LoopRelay/evidence/local-artifacts/GenerateOperationalContext.md",
                "publication:derived-git-commit:Plan:GenerateOperationalContext:.agents/operational_context.md:nested-commit",
                "publication:derived-git-push:Plan:GenerateOperationalContext:.agents/operational_context.md:nested-push",
                "publication:derived-git-push:Plan:GenerateOperationalContext:.agents/operational_context.md:parent-gitlink-commit",
                "publication:derived-git-push:Plan:GenerateOperationalContext:.agents/operational_context.md:parent-push",
            ],
            effects.OrderBy(effect => effect.Intent.Order)
                .Select(effect => effect.Intent.SemanticOperationKey));
        Assert.All(effects, effect => Assert.Equal(EffectLifecycle.Succeeded, effect.State));
    }

    [Fact]
    public async Task Prompt_transitions_record_agent_session_and_turn_rows()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-session-spine").FullName;
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
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult select = await RunEvalAsync(composition, "Evaluation Foundation", "SelectEvaluationIntent");
        TransitionRuntimeResult dependencies = await RunEvalAsync(composition, "Dependency Inventory", "CreateEvalDependencyInventory");

        Assert.True(select.AttemptCompleted, select.Explanation);
        Assert.True(dependencies.AttemptCompleted, dependencies.Explanation);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        AgentSessionRecord session = Assert.Single(await store.ReadAgentSessionsAsync());
        Assert.StartsWith("ses_", session.SessionId, StringComparison.Ordinal);
        // Provider identity comes from the runtime's capability declaration (M7): for the
        // injected fake, the fake IS the provider this evidence describes.
        Assert.Equal("test", session.Provider);
        Assert.Equal(SessionRole.OperationalExecution.ToString(), session.Role);
        Assert.Null(session.ProviderThreadId);
        Assert.True(Guid.TryParse(session.LegacySessionGuid, out Guid legacyGuid));
        Assert.NotEqual(Guid.Empty, legacyGuid);
        Assert.NotNull(session.CompletedAt);
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();
        CanonicalTransitionRunRecord inventoryRun = Assert.Single(
            snapshot.TransitionRuns,
            item => item.Transition == new WorkflowTransitionIdentity("CreateEvalDependencyInventory"));
        AttemptRecord attempt = Assert.Single(
            await store.ReadAttemptsAsync(),
            item => item.TransitionRunId == inventoryRun.RunId);
        Assert.Equal(attempt.AttemptId, session.AttemptId);
        AgentTurnRecord turn = Assert.Single(await store.ReadAgentTurnsAsync());
        Assert.StartsWith("turn_", turn.TurnId, StringComparison.Ordinal);
        Assert.Equal(session.SessionId, turn.SessionId);
        Assert.Equal(1, turn.TurnIndex);
        // M7 turn evidence: terminal state, usage, and the sha of the exact transport text.
        Assert.Equal(AgentTurnState.Completed.ToString(), turn.State);
        Assert.NotNull(turn.PromptSha256);
        Assert.NotNull(turn.PromptTokens);
        Assert.NotNull(turn.OutputTokens);
        Assert.Null(turn.DiagnosticsKind);
        Assert.Null(turn.Diagnostics);

        // Prompt Authority owns one immutable fact before dispatch. Runtime evidence joins it by
        // the exact prompt hash and cannot mutate it or back-fill runtime identities into it.
        CanonicalRenderedPromptRecord fact = Assert.Single(
            await store.ReadRenderedPromptsAsync(),
            item => item.PromptIdentity == "CreateEvalDependencyInventory");
        Assert.Null(fact.SessionId);
        Assert.Null(fact.TurnId);
        Assert.Equal(turn.PromptSha256, fact.RenderedSha256);
        Assert.Equal(inventoryRun.RunId, fact.TransitionRunId);
    }

    [Fact]
    public async Task Cancelled_turns_leave_terminal_turn_evidence_instead_of_vanishing_from_the_spine()
    {
        // M7: a turn that ends by exception (caller cancellation, transport failure) still
        // records a terminal turn row — otherwise its rendered fact would claim a send happened
        // while the turn spine showed nothing.
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-cancelled-turn").FullName;
        await WriteAsync(repo, ".agents/evals/e1.md", "# Eval Intent\n\nEvaluate the capability.");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(
            repository, new CancellingOneShotRuntime());

        TransitionRuntimeResult select = await RunEvalAsync(composition, "Evaluation Foundation", "SelectEvaluationIntent");
        Assert.True(select.AttemptCompleted, select.Explanation);
        // The executor converts the send's OperationCanceledException into the typed Cancelled
        // execution result — cancellation never collapses into generic failure.
        TransitionRuntimeResult cancelled = await RunEvalAsync(
            composition, "Dependency Inventory", "CreateEvalDependencyInventory");
        Assert.Equal(RuntimeOutcomeKind.Cancelled, cancelled.Outcome);

        var store = new CanonicalWorkflowPersistenceStore(repository);
        AgentSessionRecord session = Assert.Single(await store.ReadAgentSessionsAsync());
        Assert.NotNull(session.CompletedAt);
        AgentTurnRecord turn = Assert.Single(await store.ReadAgentTurnsAsync());
        Assert.Equal(session.SessionId, turn.SessionId);
        Assert.Equal("Canceled", turn.State);
        Assert.Equal("Cancelled", turn.DiagnosticsKind);
        Assert.Equal("cancelled mid-send", turn.Diagnostics);
        CanonicalRenderedPromptRecord fact = Assert.Single(
            await store.ReadRenderedPromptsAsync(),
            item => item.PromptIdentity == "CreateEvalDependencyInventory");
        Assert.Null(fact.SessionId);
        Assert.Null(fact.TurnId);
        Assert.Equal(turn.PromptSha256, fact.RenderedSha256);
    }

    [Fact]
    public async Task EvalRoadmap_prompt_transition_renders_generated_prompt_asset_before_executor_integration()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-eval-prompt").FullName;
        await WriteAsync(repo, ".agents/eval-architectural-catalog.md", "# Architectural Catalog");
        await WriteProjectContextAsync(repo);
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
                WorkflowIdentity.EvalRoadmap,
                new WorkflowStageIdentity("Eval DAG"),
                new WorkflowTransitionIdentity("CreateEvalDag")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Contains("Prompt execution integration is not wired", result.Explanation, StringComparison.Ordinal);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        EvalPromptAsset asset = EvalPromptAssetCatalog.GetByTransition(new WorkflowTransitionIdentity("CreateEvalDag"));
        CanonicalRenderedPromptRecord fact = Assert.Single(
            await new CanonicalWorkflowPersistenceStore(repository).ReadRenderedPromptsAsync());
        Assert.Equal(asset.PromptIdentity, fact.PromptIdentity);
        Assert.Equal(asset.SourceHash, fact.TemplateSourceHash);
    }

    [Fact]
    public async Task Execute_commit_evaluation_stall_persists_canonical_evidence()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-execute-stall").FullName;
        await WriteAsync(repo, ".agents/milestones/m1.md", "# Milestone 1\n\n- [ ] Implement Execute runtime.");
        await WriteAsync(
            repo,
            ".LoopRelay/evidence/execute-stall/state.md",
            """
            # Execute Stall State

            | Field | Value |
            |---|---|
            | Consecutive No-Progress Count | 2 |
            | Stalled | False |
            """);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        await WriteRepositoryChangesProductAsync(repository);
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        var git = new FakeProcessRunner
        {
            Handler = (workingDirectory, args) => args.SequenceEqual(["status", "--porcelain"])
                ? FakeProcessRunner.Ok(" M .agents\n")
                : FakeProcessRunner.Ok(),
        };
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime, git);

        TransitionRuntimeResult result = await RunExecuteAsync(
            composition,
            "Execution Continuity",
            "EvaluateCommit");

        Assert.Equal(RuntimeOutcomeKind.Stalled, result.Outcome);
        Assert.Equal(TransitionDurableState.Stalled, result.DurableState);
        Assert.Contains(".LoopRelay/evidence/execute-stall/state.md", result.Evidence);
        await composition.DisposeAsync();
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.TransitionRuns, run =>
            run.Transition == new WorkflowTransitionIdentity("EvaluateCommit") &&
            run.State == TransitionDurableState.Stalled &&
            run.Outcome == RuntimeOutcomeKind.Stalled);
        await AssertEffectStateAsync(repository, "record-commit-evaluation", EffectLifecycle.Stalled);
        Assert.Contains(snapshot.Warnings, warning =>
            warning.Workflow == WorkflowIdentity.Execute &&
            warning.Transition == new WorkflowTransitionIdentity("EvaluateCommit") &&
            warning.Category == WarningCategory.Repository &&
            warning.Evidence.Contains(".LoopRelay/evidence/execute-stall/state.md"));
        Assert.Contains(snapshot.WorkflowStates, state =>
            state.Workflow == WorkflowIdentity.Execute &&
            state.State == WorkflowResolutionState.Active &&
            state.Outcome == RuntimeOutcomeKind.Stalled);
        Assert.Contains(snapshot.StageStates, state =>
            state.Workflow == WorkflowIdentity.Execute &&
            state.Stage == new WorkflowStageIdentity("Execution Continuity") &&
            state.State == WorkflowResolutionState.Active);
    }

    [Fact]
    public async Task Verify_execute_entry_contract_completes_plan_and_persists_execution_readiness()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-verify").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan");
        await WriteAsync(repo, ".agents/operational_context.md", "# Operational Context");
        await WriteAsync(repo, ".agents/details.md", "# Details");
        await WriteAsync(repo, ".agents/milestones/m1.md", "# Milestone\n\n- [ ] Implement capability.");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var composition = LoopRelayCompositionRoot.CreateForTests(new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        });
        RepositoryObservation before = await composition.ObserveAsync(CancellationToken.None);
        WorkflowResolutionResult beforeResolution = composition.Resolve(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            before);

        TransitionRuntimeResult result = await RunPlanAsync(
            composition,
            "Workflow Completion",
            "VerifyExecuteEntryContract");

        RepositoryObservation after = await composition.ObserveAsync(CancellationToken.None);
        WorkflowResolutionResult afterResolution = composition.Resolve(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            after);

        Assert.Equal(new WorkflowStageIdentity("Workflow Completion"), beforeResolution.SelectedStage);
        Assert.Contains(beforeResolution.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("VerifyExecuteEntryContract") &&
            transition.State == TransitionEligibilityState.Eligible);
        Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation);
        Assert.Equal(TransitionDurableState.Completed, result.DurableState);
        ObservedWorkflowState state = Assert.Single(
            after.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.Plan);
        Assert.Equal(WorkflowResolutionState.Completed, state.State);
        Assert.Null(state.CurrentStage);
        Assert.Contains(
            after.Products,
            product => product.Product.Identity == ProductIdentity.ExecutionReadiness &&
                product.Product.ValidationState == ProductValidationState.Valid &&
                product.GateUsable);
        Assert.Equal(WorkflowResolutionState.Completed, afterResolution.WorkflowState);
        Assert.Equal(RepositoryClassification.Completed, afterResolution.Classification);
    }
}
