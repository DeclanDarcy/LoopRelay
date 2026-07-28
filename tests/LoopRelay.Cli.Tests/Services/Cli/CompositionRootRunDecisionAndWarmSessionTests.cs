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

public sealed class CompositionRootRunDecisionAndWarmSessionTests : CompositionRootTestBase
{
    [Fact]
    public async Task Run_command_records_workflow_instance_and_attempt_rows_linked_to_the_run()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-run-spine").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Spec");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedTraditional),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        RunRecord run = Assert.Single(await store.ReadRunsAsync());
        Assert.StartsWith("run_", run.RunId, StringComparison.Ordinal);
        Assert.Equal("Active", run.Status);
        Assert.Null(run.CompletedAt);
        WorkflowInstanceRecord instance = Assert.Single(await store.ReadWorkflowInstancesAsync());
        Assert.StartsWith("wfi_", instance.WorkflowInstanceId, StringComparison.Ordinal);
        Assert.Equal(run.RunId, instance.RunId);
        Assert.Equal(WorkflowIdentity.TraditionalRoadmap, instance.Workflow);
        Assert.Equal("Active", instance.Status);
        Assert.Equal(WorkflowStopReason.TransitionCompleted.ToString(), instance.Outcome);
        Assert.NotNull(instance.CompletedAt);
        AttemptRecord attempt = Assert.Single(await store.ReadAttemptsAsync());
        Assert.StartsWith("att_", attempt.AttemptId, StringComparison.Ordinal);
        Assert.StartsWith("tr_", attempt.TransitionRunId, StringComparison.Ordinal);
        Assert.Equal(instance.WorkflowInstanceId, attempt.WorkflowInstanceId);
        Assert.Equal(run.RunId, attempt.RunId);
        Assert.Equal(1, attempt.AttemptIndex);
        Assert.Equal(RuntimeOutcomeKind.Completed.ToString(), attempt.Outcome);
        Assert.NotNull(attempt.CompletedAt);
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();
        CanonicalTransitionRunRecord transitionRun = Assert.Single(snapshot.TransitionRuns);
        Assert.Equal(transitionRun.RunId, attempt.TransitionRunId);
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
    public async Task Plan_warm_session_transitions_execute_and_reuse_one_authoring_session()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-session").FullName;
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
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Contains("Prompt identity: WritePlan", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v1");
            return new AgentTurnResult(0, AgentTurnState.Completed, "wrote plan", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Contains("Prompt identity: ReviewAndRevisePlan", prompt, StringComparison.Ordinal);
            Assert.Contains("Adversarial Review", prompt, StringComparison.Ordinal);
            Assert.Contains("tighten the plan", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v2");
            return new AgentTurnResult(1, AgentTurnState.Completed, "revised plan", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult write = await RunPlanAsync(composition, "Planning", "WriteExecutablePlan");
        await WriteAdversarialReviewProductAsync(repository, "tighten the plan");
        // RevisePlan declares `.agents/plan.md` as a clean-input surface, so the plan the
        // warm session just wrote must be committed before it is consumed.
        await GitWorkspace.CommitAgentsInputsAsync(repo);
        TransitionRuntimeResult revise = await RunPlanAsync(composition, "Plan Validation", "RevisePlan");

        Assert.Equal(RuntimeOutcomeKind.Completed, write.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, revise.Outcome);
        Assert.Equal(1, runtime.OpenSessions);
        Assert.Equal(1, runtime.ClosedSessions);
        Assert.Equal(2, runtime.SessionCalls.Count);
        Assert.Equal("# Plan v2", await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.ExecutablePlan &&
            product.ProducerTransition == new WorkflowTransitionIdentity("RevisePlan") &&
            product.CausalIdentity.Length == 64);
        await AssertEffectStateAsync(repository, "persist-draft-plan", EffectLifecycle.Succeeded);
        await AssertEffectStateAsync(repository, "persist-reviewed-plan", EffectLifecycle.Succeeded);
    }

    [Fact]
    public async Task Plan_warm_session_materializes_structurally_valid_returned_plan_when_tool_write_is_absent()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-returned-markdown").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(repo), Path = repo };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
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

        TransitionRuntimeResult result = await RunPlanAsync(composition, "Planning", "WriteExecutablePlan");

        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
        string plan = await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md"));
        Assert.StartsWith("# Executable Plan", plan, StringComparison.Ordinal);
        Assert.Contains("Milestone 1", plan, StringComparison.Ordinal);
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
}
