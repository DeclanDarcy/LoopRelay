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
}
