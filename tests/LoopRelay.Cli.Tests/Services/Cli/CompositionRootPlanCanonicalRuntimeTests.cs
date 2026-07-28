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
}
