using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Tests.Recovery;

public sealed class NativeForkRecoveryMechanismTests
{
    [Theory]
    [InlineData(0, RecoveryMechanismExecutionStatus.KnownFailure, false)]
    [InlineData(1, RecoveryMechanismExecutionStatus.Succeeded, true)]
    [InlineData(2, RecoveryMechanismExecutionStatus.UnknownOutcome, false)]
    public async Task UnknownForkResponseReconciliationIsCardinalitySafe(
        int candidateCount,
        RecoveryMechanismExecutionStatus expectedStatus,
        bool expectedSpecificChild)
    {
        SessionContinuityProfile profile = Profile(SessionOperationSupport.Supported);
        RecoveryPlan plan = Plan(profile);
        ProviderSessionReference[] candidates = Enumerable.Range(0, candidateCount)
            .Select(index => new ProviderSessionReference("codex", $"child-{index}"))
            .ToArray();
        var runtime = new ReconcileRuntime(candidates);
        RecoveryMechanismExecutionRequest request = Request(profile, plan, runtime);
        var previous = new RecoveryMechanismExecutionResult(
            RecoveryMechanismExecutionStatus.UnknownOutcome, null, null, null, null, null, "response lost");

        RecoveryMechanismExecutionResult result = await new NativeForkRecoveryMechanism()
            .ReconcileAsync(request, previous);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedSpecificChild, result.Replacement is not null);
    }

    [Theory]
    [InlineData(SessionOperationSupport.Unknown)]
    [InlineData(SessionOperationSupport.Unsupported)]
    public async Task NonSupportedProfileIssuesNoForkRequest(SessionOperationSupport support)
    {
        SessionContinuityProfile profile = Profile(support);
        var runtime = new ReconcileRuntime([]);
        RecoveryPlan plan = Plan(profile);
        RecoveryMechanismExecutionResult result = await new NativeForkRecoveryMechanism().ExecuteAsync(
            Request(profile, plan, runtime));

        Assert.Equal(RecoveryMechanismExecutionStatus.KnownFailure, result.Status);
        Assert.Equal(0, runtime.ForkCalls);
    }

    private static RecoveryMechanismExecutionRequest Request(
        SessionContinuityProfile profile,
        RecoveryPlan plan,
        IAgentSessionContinuityRuntime runtime)
    {
        var spec = new AgentSessionSpec(
            SessionIdentity.New(), "repo", SessionRole.Decision,
            new SandboxProfile("read-only", false, false, false),
            AgentModel.Gpt56Sol,
            AgentEffort.High,
            AgentConfigurationAuthority.Brain,
            "/repo");
        return new RecoveryMechanismExecutionRequest(
            RecoveryMechanismExecutionPhase.CreateReplacement,
            plan,
            new SessionCreateRequest(spec, profile, plan.IdempotencyIdentity),
            new ProviderSessionReference("codex", "parent"),
            [], null, string.Empty, runtime);
    }

    private static RecoveryPlan Plan(SessionContinuityProfile profile) => new(
        "plan-native", "recovery-plan.v1", "planner.v1", "policy.v1",
        new RecoveryMechanismKey("NativeFork", "1"), [], [], null,
        new Dictionary<string, string>(), RecoveryActivationStrategy.NativeClone,
        "exact-parent-child-and-certified-fidelity.v1",
        "enumerate-exact-parent-children-by-correlation.v1",
        RecoveryCompleteness.Full, [], profile.Digest,
        new Dictionary<string, string>(), "fork-idempotency", 0, "fail-closed");

    private static SessionContinuityProfile Profile(SessionOperationSupport fork) => new(
        "codex", "test", "0.142.5", "codex", "v2", "schema",
        new Dictionary<string, bool>(), new Dictionary<string, string>(),
        new Dictionary<SessionContinuityOperation, SessionOperationSupportDescriptor>
        {
            [SessionContinuityOperation.Fork] = new SessionOperationSupportDescriptor(
                fork, "thread/fork", new Dictionary<string, SessionParameterSupport>(),
                "clone", "stable-parent-child", "none", "enumerate-exact-children", "fixture"),
        },
        null, "unknown", "fixture", negotiatedAt: DateTimeOffset.UnixEpoch);

    private sealed class ReconcileRuntime(IReadOnlyList<ProviderSessionReference> candidates)
        : IAgentSessionContinuityRuntime
    {
        public int ForkCalls { get; private set; }
        public Task<SessionForkResult> ForkSessionAsync(SessionForkRequest request, CancellationToken cancellationToken = default)
        {
            ForkCalls++;
            return Task.FromResult(new SessionForkResult(false, null, request.Parent, null, null));
        }
        public Task<SessionReconcileResult> ReconcileAsync(SessionReconcileRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SessionReconcileResult(
                candidates.Count == 1,
                candidates.Count == 1 ? candidates[0] : null,
                null,
                candidates));
        public Task<SessionContinuityNegotiationResult> NegotiateAsync(SessionContinuityNegotiationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SessionCreateResult> CreateSessionAsync(SessionCreateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SessionResumeResult> ResumeSessionAsync(SessionResumeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SessionContentResult> ReadSessionAsync(SessionContentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SessionSeedResult> SeedSessionAsync(SessionSeedRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
