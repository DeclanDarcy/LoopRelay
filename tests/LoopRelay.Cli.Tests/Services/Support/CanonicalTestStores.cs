using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Decisions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Cli.Tests.Services.Support;

internal static class CanonicalTestStores
{
    public static IDecisionPromptTurnDispatcher DecisionPromptDispatcher { get; } =
        new TestDecisionPromptTurnDispatcher();
    public static LoopArtifacts CreateLoopArtifacts(IArtifactStore store, Repository repository) =>
        new(store, repository, new MemoryHistoryStore(), new MemoryRecommendationStore());

    public static LoopArtifacts CreateLoopArtifacts(
        IArtifactStore store,
        Repository repository,
        ILoopHistoryStore history) =>
        new(store, repository, history, new MemoryRecommendationStore());

    public static (ExecutionAuthorization Authorization, IExecutionAuthorizationResolver Resolver)
        ExecutionAuthorization()
    {
        CanonicalCausalContext causality = NewAttempt();
        var profile = new ResolvedRuntimeProfile(
            new RuntimeProfileIdentity("runtime_test"),
            "codex",
            AgentModel.Gpt56Terra,
            AgentEffort.High,
            "persistent-session",
            "danger-full-access",
            "test",
            "never",
            "resume",
            TimeSpan.FromMinutes(30),
            "test",
            "fail-closed");
        var authorization = new ExecutionAuthorization(
            ExecutionAuthorizationIdentity.New(),
            new DecisionProductVersionIdentity("decision_test"),
            profile.Identity,
            RuntimeProfileEvaluationIdentity.New(),
            RenderedPromptFactIdentity.New(),
            ConsumedInputManifestIdentity.New(),
            causality);
        return (authorization, new FixedExecutionAuthorizationResolver(profile));
    }

    private static CanonicalCausalContext NewAttempt() => new(
        WorkspaceIdentity.New(),
        RunIdentity.New(),
        WorkflowInstanceIdentity.New(),
        TransitionRunIdentity.New(),
        AttemptIdentity.New());

    private sealed class FixedExecutionAuthorizationResolver(ResolvedRuntimeProfile profile)
        : IExecutionAuthorizationResolver
    {
        public Task<ResolvedRuntimeProfile> ResolveAsync(
            ExecutionAuthorization authorization,
            CancellationToken cancellationToken = default) => Task.FromResult(profile);
    }

    private sealed class TestDecisionPromptTurnDispatcher : IDecisionPromptTurnDispatcher
    {
        public async Task<DecisionPromptTurnResult> DispatchAsync(
            IAgentSession session,
            string promptIdentity,
            string? templateSourceHash,
            string renderedTemplate,
            IReadOnlyList<ConsumedInputFile> consumedInputs,
            Func<AgentStreamChunk, Task>? onChunk,
            CancellationToken cancellationToken)
        {
            AgentTurnResult result = await session.RunTurnAsync(
                renderedTemplate, onChunk, cancellationToken);
            CanonicalCausalContext causality = NewAttempt();
            return new DecisionPromptTurnResult(
                result,
                new AgentSessionIdentity(session.SessionId.ToString()),
                TurnIdentity.New(),
                PromptDispatchIdentity.New(),
                RenderedPromptFactIdentity.New(),
                causality);
        }
    }

    private sealed class MemoryHistoryStore : ILoopHistoryStore
    {
        private readonly List<LoopHistoryRecord> records = [];

        public Task<LoopHistoryRecord> AppendAsync(
            LoopHistoryAppendRequest request,
            CancellationToken cancellationToken = default)
        {
            var record = new LoopHistoryRecord(
                HistoryFactIdentity.New(),
                request.Kind,
                records.Count(item => item.Kind == request.Kind) + 1,
                DateTimeOffset.UtcNow,
                request.Content,
                LoopHistoryRecord.ComputeContentHash(request.Content),
                request.Causality,
                request.Evidence,
                request.Supersedes,
                $".agents/test-history/{request.Kind}/{records.Count + 1:0000}.md");
            records.Add(record);
            return Task.FromResult(record);
        }

        public Task<LoopHistoryRecord?> ReadLatestAsync(
            LoopHistoryKind kind,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(records.LastOrDefault(record => record.Kind == kind));
    }

    private sealed class MemoryRecommendationStore : IExecutionRecommendationEvidenceStore
    {
        private readonly List<ExecutionRecommendationEvidence> records = [];

        public Task AppendAsync(
            ExecutionRecommendationEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            records.Add(evidence);
            return Task.CompletedTask;
        }

        public Task<ExecutionRecommendationEvidence?> ReadAsync(
            ExecutionRecommendationIdentity identity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(records.FirstOrDefault(record => record.Identity == identity));

        public Task<ExecutionRecommendationEvidence?> ReadForDecisionAsync(
            DecisionProductVersionIdentity decisionProduct,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(records.LastOrDefault(record => record.DecisionProduct == decisionProduct));
    }
}
