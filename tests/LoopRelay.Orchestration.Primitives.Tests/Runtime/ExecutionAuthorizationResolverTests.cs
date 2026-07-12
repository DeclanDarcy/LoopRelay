using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Tests.Runtime;

public sealed class ExecutionAuthorizationResolverTests
{
    [Fact]
    public async Task ResolvesOnlyTheProfileBoundByDurablePolicyEvaluation()
    {
        DecisionProductVersionIdentity decision = DecisionProductVersionIdentity.New();
        ResolvedRuntimeProfile profile = Profile("runtime-allowed");
        var evaluation = new RuntimeProfileEvaluation(
            RuntimeProfileEvaluationIdentity.New(), null, decision, new PolicyIdentity("policy"),
            Capabilities(), RuntimeProfileEvaluationOutcome.Rejected,
            profile, ["fallback selected"], DateTimeOffset.UtcNow);
        var store = new Store(evaluation, profile);
        var authorization = new ExecutionAuthorization(
            ExecutionAuthorizationIdentity.New(), decision, profile.Identity, evaluation.Identity,
            RenderedPromptFactIdentity.New(), ConsumedInputManifestIdentity.New(), Causality());

        ResolvedRuntimeProfile resolved = await new ExecutionAuthorizationResolver(store, store)
            .ResolveAsync(authorization);

        Assert.Equal(profile, resolved);
    }

    [Fact]
    public async Task RejectsAuthorizationBoundToAnotherDecisionProduct()
    {
        ResolvedRuntimeProfile profile = Profile("runtime-allowed");
        var evaluation = new RuntimeProfileEvaluation(
            RuntimeProfileEvaluationIdentity.New(), null, DecisionProductVersionIdentity.New(),
            new PolicyIdentity("policy"), Capabilities(),
            RuntimeProfileEvaluationOutcome.Accepted, profile, ["accepted"], DateTimeOffset.UtcNow);
        var store = new Store(evaluation, profile);
        var authorization = new ExecutionAuthorization(
            ExecutionAuthorizationIdentity.New(), DecisionProductVersionIdentity.New(), profile.Identity,
            evaluation.Identity, RenderedPromptFactIdentity.New(), ConsumedInputManifestIdentity.New(), Causality());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ExecutionAuthorizationResolver(store, store).ResolveAsync(authorization));
    }

    private sealed class Store(RuntimeProfileEvaluation evaluation, ResolvedRuntimeProfile profile)
        : IRuntimeProfileEvaluationStore, IResolvedRuntimeProfileStore
    {
        public Task AppendAsync(RuntimeProfileEvaluation value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<RuntimeProfileEvaluation?> ReadAsync(
            RuntimeProfileEvaluationIdentity identity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<RuntimeProfileEvaluation?>(identity == evaluation.Identity ? evaluation : null);
        public Task AppendAsync(ResolvedRuntimeProfile value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<ResolvedRuntimeProfile?> ReadAsync(
            RuntimeProfileIdentity identity,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ResolvedRuntimeProfile?>(identity == profile.Identity ? profile : null);
    }

    private static ResolvedRuntimeProfile Profile(string identity) => new(
        new RuntimeProfileIdentity(identity), "codex", AgentModel.Gpt56Sol, AgentEffort.High,
        "persistent", "danger-full-access", "execution", "never", "resume",
        TimeSpan.FromMinutes(30), "default", "reconcile-before-retry");

    private static ProviderCapabilityEvidence Capabilities() => new(
        ProviderCapabilityEvidenceIdentity.New(), "codex",
        Enum.GetValues<AgentModel>(), AgentEffort.XHigh, DateTimeOffset.UtcNow);

    private static CanonicalCausalContext Causality() => new(
        WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
        TransitionRunIdentity.New(), AttemptIdentity.New());
}
