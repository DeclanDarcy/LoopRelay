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

public sealed class CompositionRootLedgerHandoffAndRevisionTests : CompositionRootTestBase
{
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
    public async Task EvalRoadmap_milestone_deep_dive_stops_on_empty_active_epic_context()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-eval-context").FullName;
        Directory.CreateDirectory(Path.Combine(repo, ".agents"));
        await File.WriteAllTextAsync(Path.Combine(repo, ".agents", "epic.md"), "   ");
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
                new WorkflowStageIdentity("Milestone Specification"),
                new WorkflowTransitionIdentity("GenerateMilestoneDeepDivesForEpic")));

        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, result.Outcome);
        Assert.Equal(TransitionDurableState.InputUnsatisfied, result.DurableState);
        Assert.Contains("Active Epic prompt context is empty", result.Explanation, StringComparison.Ordinal);
        Assert.Contains(".agents/epic.md", result.Evidence);
    }

    [Fact]
    public async Task Execute_handoff_blocks_precisely_when_exact_implementation_thread_resume_fails()
    {
        (string repo, Repository repository, FakeAgentRuntime runtime, FakeProcessRunner process) = await PrepareExecuteContinuityCaseAsync(
            "cc-cli-unified-execute-warm-resume-fail");
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "# Decisions\n\nCreate src/feature.cs.", AgentTokenUsage.Zero)));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            Directory.CreateDirectory(Path.Combine(repo, "src"));
            File.WriteAllText(Path.Combine(repo, "src", "feature.cs"), "feature\n");
            return new AgentTurnResult(1, AgentTurnState.Completed, "implemented", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot first = LoopRelayCompositionRoot.CreateForTests(repository, runtime, process);
        await RunPlanAsync(first, "Workflow Completion", "VerifyExecuteEntryContract");
        await RunExecuteAsync(first, "Execution Readiness", "VerifyExecutionReadiness");
        await RunExecuteAsync(first, "Implementation Planning", "GenerateDecision");
        await RunExecuteAsync(first, "Implementation", "ExecuteImplementationSlice");
        ExecutionWarmSessionContinuity checkpoint = Assert.IsType<ExecutionWarmSessionContinuity>(
            await new CanonicalCheckpointStore(repository).ReadAsync<ExecutionWarmSessionContinuity>(CanonicalCheckpointKeys.ExecutionWarmSession, CancellationToken.None));
        await first.DisposeAsync();
        runtime.FailResume = true;

        await using LoopRelayCompositionRoot restarted = LoopRelayCompositionRoot.CreateForTests(repository, runtime, process);
        TransitionRuntimeResult handoff = await RunExecuteAsync(
            restarted, "Execution Continuity", "GenerateHandoff");

        Assert.Equal(RuntimeOutcomeKind.RecoveryRequired, handoff.Outcome);
        Assert.Equal(TransitionDurableState.ProviderOutcomeUnknown, handoff.DurableState);
        Assert.Contains("could not resume the exact execution thread", handoff.Explanation, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(repo, ".agents", "handoffs", "handoff.md")));
        await AssertCanonicalRecoveryPlanAsync(
            repository, $"execute:{checkpoint.ProviderThreadId}", CanonicalRecoveryAction.ResumeSession);
    }

    [Fact]
    public async Task Plan_revision_resumes_exact_authoring_thread_after_composition_restart()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-restart").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(repo), Path = repo };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v1");
            return new AgentTurnResult(0, AgentTurnState.Completed, "wrote plan", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot first = LoopRelayCompositionRoot.CreateForTests(repository, runtime);
        TransitionRuntimeResult write = await RunPlanAsync(first, "Planning", "WriteExecutablePlan");
        PlanWarmSessionContinuity checkpoint = Assert.IsType<PlanWarmSessionContinuity>(
            await new CanonicalCheckpointStore(repository).ReadAsync<PlanWarmSessionContinuity>(CanonicalCheckpointKeys.PlanWarmSession, CancellationToken.None));
        Assert.Null(Assert.Single(runtime.OpenedSpecs).ResumeThreadId);
        await first.DisposeAsync();

        await WriteAdversarialReviewProductAsync(repository, "tighten after restart");
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.NotNull(spec.ResumeThreadId);
            Assert.Contains("tighten after restart", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v2");
            return new AgentTurnResult(1, AgentTurnState.Completed, "revised plan", AgentTokenUsage.Zero);
        }));
        await using LoopRelayCompositionRoot restarted = LoopRelayCompositionRoot.CreateForTests(repository, runtime);
        TransitionRuntimeResult revise = await RunPlanAsync(restarted, "Plan Validation", "RevisePlan");

        Assert.Equal(RuntimeOutcomeKind.Completed, write.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, revise.Outcome);
        Assert.Equal(2, runtime.OpenSessions);
        Assert.Equal(2, runtime.SessionCalls.Count);
        Assert.NotNull(runtime.OpenedSpecs[^1].ResumeThreadId);
        Assert.Equal("# Plan v2", await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md")));
        await AssertCanonicalRecoveryPlanAsync(
            repository, $"plan:{checkpoint.ProviderThreadId}", CanonicalRecoveryAction.ResumeSession);
        Assert.Null(await new CanonicalCheckpointStore(repository).ReadAsync<PlanWarmSessionContinuity>(CanonicalCheckpointKeys.PlanWarmSession, CancellationToken.None));
    }
}
