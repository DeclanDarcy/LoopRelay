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

public sealed class CompositionRootTraditionalRoadmapAndSpineTests : CompositionRootTestBase
{
    [Fact]
    public async Task TraditionalRoadmap_accepts_inline_code_file_markers_from_provider_output()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-traditional-full-runtime").FullName;
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        EnqueueTraditionalOutput(runtime, "BootstrapRoadmapCompletionContext", "# Roadmap Completion Context");
        EnqueueTraditionalOutput(runtime, "SelectStrategicInitiative", "# Strategic Initiative Selection");
        EnqueueTraditionalOutput(runtime, "CreateNewEpic", ValidEvalActiveEpic());
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("Generate Milestone Deep Dives For Epic", prompt, StringComparison.Ordinal);
            Assert.Contains("## Active Epic", prompt, StringComparison.Ordinal);
            Assert.Contains("Source: .agents/epic.md", prompt, StringComparison.Ordinal);
            Assert.Contains("including the leading dot", prompt, StringComparison.Ordinal);
            Assert.Contains("never `agents/epic.md`", prompt, StringComparison.Ordinal);
            Assert.Contains("# Epic:", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(3, AgentTurnState.Completed, """
                # Milestone Deep Dive Bundle

                # FILE: `.agents/specs/m1.md`

                # Milestone Spec: Implement the roadmap capability

                ## Purpose

                Implement the roadmap capability.

                # FILE: `.agents/specs/m2.md`

                # Milestone Spec: Verify the roadmap capability

                ## Purpose

                Verify the roadmap capability.

                # FILE: `.agents/specs/m3.md`

                # Milestone Spec: Harden the roadmap capability

                ## Purpose

                Harden the roadmap capability.
                """, AgentTokenUsage.Zero);
        }));
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
        TransitionRuntimeResult specs = await RunTraditionalAsync(
            composition,
            "Milestone Specification",
            "GenerateMilestoneDeepDivesForEpic");
        TransitionRuntimeResult verify = await RunTraditionalAsync(
            composition,
            "Workflow Completion",
            "VerifyPlanEntryContract");

        Assert.All(
            [context, selection, epic, specs, verify],
            result => Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation));
        Assert.Equal(4, runtime.OneShotCalls.Count);
        Assert.Equal(ValidEvalActiveEpic(), await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "epic.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.WorkflowStates, state =>
            state.Workflow == WorkflowIdentity.TraditionalRoadmap &&
            state.State == WorkflowResolutionState.Completed &&
            state.CurrentStage is null);
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.PreparedEpic &&
            product.StorageRepresentations.Contains(".agents/epic.md"));
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.MilestoneSpecificationSet &&
            product.StorageRepresentations.Contains(".agents/specs/m1.md") &&
            product.StorageRepresentations.Contains(".agents/specs/m2.md") &&
            product.StorageRepresentations.Contains(".agents/specs/m3.md"));
        string effectEvidencePath = Path.Combine(
            repo,
            ".LoopRelay",
            "evidence",
            "traditional-roadmap-effects",
            "CreateEpic.md");
        Assert.True(File.Exists(effectEvidencePath));
        string effectEvidence = await File.ReadAllTextAsync(effectEvidencePath);
        Assert.Contains("Transition Ordering", effectEvidence, StringComparison.Ordinal);
        Assert.Contains("Prompt Execution Sequencing", effectEvidence, StringComparison.Ordinal);
        Assert.Contains("Selection Provenance", effectEvidence, StringComparison.Ordinal);
        Assert.Contains("Recovery Intent", effectEvidence, StringComparison.Ordinal);
        await AssertEffectStateAsync(repository, "persist-prepared-epic", EffectLifecycle.Succeeded);
        IReadOnlyList<EffectWorkItem> publicationPlan = await new CanonicalEffectWorkStore(repository)
            .ReadPlanAsync(epic.TransitionRun!.Value, CancellationToken.None);
        Assert.Contains(publicationPlan, item => item.Intent.Executor == GitEffectExecutorKeys.NestedRepositoryCommit &&
            item.Intent.Requiredness == EffectRequiredness.BlockingLocal);
        Assert.Contains(publicationPlan, item => item.Intent.Executor == GitEffectExecutorKeys.NestedRepositoryPush &&
            item.Intent.Requiredness == EffectRequiredness.RequiredAsync);
        Assert.Contains(publicationPlan, item => item.Intent.Executor == GitEffectExecutorKeys.ParentGitlinkCommit &&
            item.Intent.Requiredness == EffectRequiredness.BlockingLocal);
        Assert.Contains(publicationPlan, item => item.Intent.Executor == GitEffectExecutorKeys.ParentRepositoryPush &&
            item.Intent.Requiredness == EffectRequiredness.RequiredAsync);
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
