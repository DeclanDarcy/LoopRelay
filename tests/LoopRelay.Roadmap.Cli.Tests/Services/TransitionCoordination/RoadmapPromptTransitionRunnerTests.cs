using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Services.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;
using LoopRelay.Roadmap.Cli.Services.Splits;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using LoopRelay.Roadmap.Cli.Services.TransitionState;
using LoopRelay.Roadmap.Cli.Tests.Services.Cli;
using LoopRelay.Roadmap.Cli.Tests.Services.Projections;
using LoopRelay.Roadmap.Cli.Tests.Services.Support;
using DecisionLedgerStore = LoopRelay.Roadmap.Cli.Services.Decisions.DecisionLedgerStore;
using RoadmapStateStore = LoopRelay.Roadmap.Cli.Services.State.RoadmapStateStore;

namespace LoopRelay.Roadmap.Cli.Tests.Services.TransitionCoordination;

public sealed class RoadmapPromptTransitionRunnerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Operation_cancellation_is_not_converted_to_transition_failure(bool promotionCandidate)
    {
        using var repo = new TempRepo();
        var runtime = new CancellingAgentRuntime();
        var stateStore = new RoadmapStateStore(repo.Artifacts);
        RoadmapPromptTransitionRunner runner = CreateRunner(repo, runtime, stateStore);

        string prompt = promotionCandidate ? "CreateNewEpic" : "CreateRoadmapCompletionContext";
        string projectionPath = RoadmapArtifactPaths.ProjectionPaths[prompt];
        repo.Write(projectionPath, ProjectionSamples.Valid(prompt));
        if (promotionCandidate)
        {
            repo.Write(RoadmapArtifactPaths.Selection, "selection");
        }

        Task Act() => promotionCandidate
            ? runner.RunPromotionCandidateAsync(
                RoadmapState.NewEpicProposed,
                RoadmapState.ActiveEpicReady,
                prompt,
                projectionPath,
                "rendered context",
                "selection",
                [RoadmapArtifactPaths.ActiveEpic],
                CancellationToken.None)
            : runner.RunNormalAsync(
                RoadmapState.CoreReady,
                RoadmapState.RoadmapCompletionContextReady,
                prompt,
                projectionPath,
                "rendered context",
                "completed epic evidence",
                [RoadmapArtifactPaths.RoadmapCompletionContext],
                CancellationToken.None);

        await Assert.ThrowsAsync<OperationCanceledException>(Act);

        RoadmapStateDocument state = (await stateStore.LoadAsync())!;
        Assert.Equal(promotionCandidate ? RoadmapState.NewEpicProposed : RoadmapState.RoadmapCompletionContextReady, state.CurrentState);
        Assert.Equal(TransitionStatus.Started, state.LastTransition.Status);
        Assert.Equal(promotionCandidate ? "Prompt Started" : "Pending", state.LastTransition.Decision);
        Assert.Equal(promotionCandidate ? RoadmapArtifactPaths.ActiveEpic : RoadmapArtifactPaths.RoadmapCompletionContext, state.LastTransition.Output);
        Assert.Equal(1, runtime.OneShotCalls);

        string journal = repo.Read(RoadmapArtifactPaths.TransitionJournal);
        Assert.Contains("\"event\":\"TransitionStarted\"", journal, StringComparison.Ordinal);
        Assert.DoesNotContain("\"event\":\"TransitionFailed\"", journal, StringComparison.Ordinal);
    }

    private static RoadmapPromptTransitionRunner CreateRunner(
        TempRepo repo,
        IAgentRuntime runtime,
        RoadmapStateStore stateStore)
    {
        var executionPreparation = new ExecutionPreparationProvenanceService(
            repo.Artifacts,
            new ExecutionPreparationManifestStore(repo.Artifacts));
        var inputResolver = new TransitionInputResolver(repo.Artifacts, executionPreparation);
        var promptRunner = new RoadmapPromptRunner(runtime, repo.Repository, new TestConsole());
        var manifest = new ProjectionManifestStore(repo.Artifacts);
        var decisionLedger = new DecisionLedgerStore(repo.Artifacts);
        var journal = new TransitionJournalStore(repo.Artifacts);
        var persistence = new RoadmapTransitionPersistence(
            repo.Artifacts,
            manifest,
            stateStore,
            decisionLedger,
            journal,
            new SplitFamilyStore(repo.Artifacts));
        return new RoadmapPromptTransitionRunner(
            inputResolver,
            promptRunner,
            journal,
            persistence);
    }

    private sealed class CancellingAgentRuntime : IAgentRuntime
    {
        public int OneShotCalls { get; private set; }

        public Task<IAgentSession> OpenSessionAsync(
            AgentSessionSpec spec,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IAgentSession>(new CancellingAgentSession(spec));

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec,
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default)
        {
            OneShotCalls++;
            throw new OperationCanceledException("cancelled");
        }

        public ValueTask CloseSessionAsync(IAgentSession session) => ValueTask.CompletedTask;
    }

    private sealed class CancellingAgentSession(AgentSessionSpec spec) : IAgentSession
    {
        public SessionIdentity SessionId => spec.SessionId;
        public string RepositoryId => spec.RepositoryId;
        public SessionRole Role => spec.Role;
        public AgentSessionMode Mode => AgentSessionMode.Persistent;
        public AgentProcessState State => AgentProcessState.Exited;
        public int CompletedTurns => 0;
        public AgentTokenUsage TotalUsage => AgentTokenUsage.Zero;
        public string? ThreadId => null;

        public Task<AgentTurnResult> RunTurnAsync(
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException("cancelled");

        public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
