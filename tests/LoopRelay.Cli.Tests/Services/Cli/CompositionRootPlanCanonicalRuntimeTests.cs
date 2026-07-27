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

public sealed class CompositionRootPlanCanonicalRuntimeTests : CompositionRootTestBase
{
    [Fact]
    public async Task Plan_workflow_transitions_run_through_canonical_runtime()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-full-runtime").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Contains("Prompt identity: WritePlan", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v1\n\nImplement capability.");
            return new AgentTurnResult(0, AgentTurnState.Completed, "wrote plan", AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("AdversarialPlanReview", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(0, AgentTurnState.Completed, ValidAdversarialProjection(), AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Equal("read-only", spec.Sandbox.Identifier);
            Assert.Contains("# Adversarial Plan Review Projection", prompt, StringComparison.Ordinal);
            Assert.Contains("# Plan v1", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("{projectContextProjection}", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(
                1,
                AgentTurnState.Completed,
                "# Review\n\n## Verdict\n\n- CONDITIONAL PASS: tighten the plan.",
                AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Contains("Adversarial Review", prompt, StringComparison.Ordinal);
            Assert.Contains("tighten the plan", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v2\n\nImplement capability with gates.");
            return new AgentTurnResult(2, AgentTurnState.Completed, "revised plan", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, _, _) =>
        {
            Assert.Equal("collect-details", spec.OperationPermissionProfile?.Label);
            File.WriteAllText(Path.Combine(repo, ".agents", "details.md"), "# Details\n\nShared implementation detail.");
            return new AgentTurnResult(3, AgentTurnState.Completed, "details", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, _, _) =>
        {
            Assert.Equal("extract-milestones", spec.OperationPermissionProfile?.Label);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v3\n\nImplement capability with milestone gates.");
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "milestones"));
            File.WriteAllText(
                Path.Combine(repo, ".agents", "milestones", "m1.md"),
                "# Milestone 1\n\n- [ ] Implement canonical Plan runtime.");
            return new AgentTurnResult(4, AgentTurnState.Completed, "milestones", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, _, _) =>
        {
            Assert.Equal("extract-details", spec.OperationPermissionProfile?.Label);
            File.WriteAllText(Path.Combine(repo, ".agents", "details.md"), "# Details\n\nRefined shared implementation detail.");
            return new AgentTurnResult(5, AgentTurnState.Completed, "refined", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult write = await RunPlanAsync(composition, "Planning", "WriteExecutablePlan");
        TransitionRuntimeResult projection = await RunPlanAsync(composition, "Plan Validation", "GenerateAdversarialProjection");
        await AssertPlanStageAsync(repository, "Plan Validation", "GenerateAdversarialProjection");
        // RunAdversarialReview declares `.agents/plan.md` and `.agents/projections/` as
        // clean-input surfaces, so the plan and projection just produced must be committed.
        await GitWorkspace.CommitAgentsInputsAsync(repo);
        TransitionRuntimeResult review = await RunPlanAsync(composition, "Plan Validation", "RunAdversarialReview");
        await AssertPlanStageAsync(repository, "Plan Validation", "RunAdversarialReview");
        TransitionRuntimeResult revise = await RunPlanAsync(composition, "Plan Validation", "RevisePlan");
        await AssertPlanStageAsync(repository, "Execution Preparation", "RevisePlan");
        // GenerateOperationalContext declares `.agents/plan.md`, so the revised plan must be committed.
        await GitWorkspace.CommitAgentsInputsAsync(repo);
        TransitionRuntimeResult operationalContext = await RunPlanAsync(composition, "Execution Preparation", "GenerateOperationalContext");
        TransitionRuntimeResult details = await RunPlanAsync(composition, "Execution Preparation", "CollectExecutionDetails");
        await AssertPlanStageAsync(repository, "Execution Preparation", "CollectExecutionDetails");
        TransitionRuntimeResult milestones = await RunPlanAsync(composition, "Execution Preparation", "GenerateExecutionMilestones");
        await AssertPlanStageAsync(repository, "Execution Preparation", "GenerateExecutionMilestones");
        // RefineExecutionDetails declares `.agents/details.md` and `.agents/milestones/`, so the
        // collected details and generated milestones must be committed before refinement reads them.
        await GitWorkspace.CommitAgentsInputsAsync(repo);
        TransitionRuntimeResult refine = await RunPlanAsync(composition, "Execution Preparation", "RefineExecutionDetails");
        await AssertPlanStageAsync(repository, "Workflow Completion", "RefineExecutionDetails");
        TransitionRuntimeResult verify = await RunPlanAsync(composition, "Workflow Completion", "VerifyExecuteEntryContract");

        Assert.All(
            [write, projection, review, revise, operationalContext, details, milestones, refine, verify],
            result => Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation));
        Assert.Single(runtime.OneShotCalls);
        Assert.Equal(5, runtime.OpenSessions);
        Assert.Equal(5, runtime.ClosedSessions);
        Assert.Equal(6, runtime.SessionCalls.Count);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.AdversarialProjection &&
            product.StorageRepresentations.Contains(PlanPromptContext.AdversarialPlanReviewProjectionPath));
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.AdversarialReview &&
            product.StorageRepresentations.Contains(".LoopRelay/evidence/plan/adversarial-review.md"));
        Assert.Contains(snapshot.WorkflowStates, state =>
            state.Workflow == WorkflowIdentity.Plan &&
            state.State == WorkflowResolutionState.Completed &&
            state.CurrentStage is null);
        Assert.Contains(snapshot.Products, product => product.Identity == ProductIdentity.ExecutionReadiness);
    }

    [Fact]
    public async Task Plan_scoped_artifact_milestone_without_checkboxes_rolls_back_declared_writes()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-scoped-rollback").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan original\n\nImplement capability.");
        await WriteAsync(repo, ".agents/details.md", "# Details\n\nUniversal detail.");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan rewritten without valid milestones.");
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "milestones"));
            File.WriteAllText(Path.Combine(repo, ".agents", "milestones", "m1.md"), "# Milestone without checkbox");
            return new AgentTurnResult(0, AgentTurnState.Completed, "claimed success", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            Request(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Execution Preparation"),
                new WorkflowTransitionIdentity("GenerateExecutionMilestones")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Equal(TransitionDurableState.Failed, result.DurableState);
        Assert.Contains("no trackable checkboxes", result.Explanation, StringComparison.Ordinal);
        Assert.Equal("# Plan original\n\nImplement capability.", await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md")));
        Assert.False(File.Exists(Path.Combine(repo, ".agents", "milestones", "m1.md")));
        Assert.Equal(1, runtime.OpenSessions);
        Assert.Equal(1, runtime.ClosedSessions);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.DoesNotContain(snapshot.Products, product => product.Identity == ProductIdentity.ExecutionMilestoneSet);
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
}
