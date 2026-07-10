using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Orchestration.Tests.Recovery;

public sealed class RecoveryRuntimeTests
{
    [Fact]
    public async Task UnavailableOriginalCreatesSeedsAndAtomicallyActivatesOneReplacement()
    {
        Fixture fixture = await Fixture.CreateAsync();
        var continuity = new FakeContinuityRuntime(fixture.Profile);
        var runtime = new RecoveryRuntime(
            fixture.Store,
            continuity,
            new RecoverySourceCatalog([new FixedSource()]),
            new RecoveryPlanner(),
            new RecoveryMechanismCatalog([new RepositoryReconstructionMechanism()]),
            new FixedEnvelopeFactory(),
            new FrozenTimeProvider(Fixture.Now));

        RecoveryRuntimeResult result = await runtime.RunAsync(fixture.Request());

        Assert.Equal(RecoveryRuntimeOutcome.ReplacementRepositoryOnly, result.Outcome);
        Assert.True(result.Seeded);
        Assert.Equal("thread-child", result.ProviderSession!.ThreadId);
        Assert.Equal(1, continuity.CreateCalls);
        Assert.Equal(1, continuity.SeedCalls);
        Assert.Equal("marker-attempt", continuity.LastMarker);
        ActiveStateReadResult active = await fixture.Store.ReadActiveAsync(fixture.Scope.ScopeId);
        Assert.Equal("thread-child", active.Lineage!.ProviderSessionId);
        Assert.Equal("RepositoryOnly", active.Lineage.Mechanism);
        Assert.Equal("Authoritative", active.Lineage.AuthorityState);
        Assert.Equal(RecoveryAttemptStatus.RecoveryCompleted, result.Attempt!.Status);
        Assert.NotNull(await fixture.Store.ReadPlanAsync(result.Plan!.Digest));

        await result.Session!.DisposeAsync();
    }

    [Fact]
    public async Task SyntheticSupportedForkActivatesWithoutAnImplicitContextTurn()
    {
        Fixture fixture = await Fixture.CreateAsync();
        var continuity = new FakeContinuityRuntime(fixture.Profile) { ForkEnabled = true };
        var runtime = new RecoveryRuntime(
            fixture.Store,
            continuity,
            new RecoverySourceCatalog([new FixedSource()]),
            new RecoveryPlanner(),
            new RecoveryMechanismCatalog(
            [
                new RepositoryReconstructionMechanism(),
                new NativeForkRecoveryMechanism(),
            ]),
            new FixedEnvelopeFactory(),
            new FrozenTimeProvider(Fixture.Now));
        RecoveryRuntimeRequest request = fixture.Request() with
        {
            Policy = new Dictionary<string, string>
            {
                ["rank:NativeFork@1"] = "1",
                ["rank:RepositoryReconstruction@1"] = "100",
            },
        };

        RecoveryRuntimeResult result = await runtime.RunAsync(request);

        Assert.Equal(RecoveryRuntimeOutcome.ReplacementNativeFork, result.Outcome);
        Assert.False(result.Seeded);
        Assert.Equal(1, continuity.ForkCalls);
        Assert.Equal(0, continuity.CreateCalls);
        Assert.Equal(0, continuity.SeedCalls);
        ActiveStateReadResult active = await fixture.Store.ReadActiveAsync(fixture.Scope.ScopeId);
        Assert.Equal("NativeFork", active.Lineage!.Mechanism);
        Assert.Equal("thread-child", active.Lineage.ProviderSessionId);
        await result.Session!.DisposeAsync();
    }

    [Theory]
    [InlineData(SessionResumeOutcome.DeterministicProtocolFailure, RecoveryRuntimeOutcome.ProtocolRepairRequired)]
    [InlineData(SessionResumeOutcome.AuthenticationFailure, RecoveryRuntimeOutcome.FailedClosed)]
    [InlineData(SessionResumeOutcome.UnknownOutcome, RecoveryRuntimeOutcome.UnknownOutcome)]
    public async Task NonReplacementFailuresPreserveTheOriginalAndNeverCreate(
        SessionResumeOutcome resumeOutcome,
        RecoveryRuntimeOutcome expected)
    {
        Fixture fixture = await Fixture.CreateAsync();
        var continuity = new FakeContinuityRuntime(fixture.Profile) { OriginalResumeOutcome = resumeOutcome };
        var runtime = new RecoveryRuntime(
            fixture.Store,
            continuity,
            new RecoverySourceCatalog([new FixedSource()]),
            new RecoveryPlanner(),
            new RecoveryMechanismCatalog([new RepositoryReconstructionMechanism()]),
            new FixedEnvelopeFactory(),
            new FrozenTimeProvider(Fixture.Now));

        RecoveryRuntimeResult result = await runtime.RunAsync(fixture.Request());

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(0, continuity.CreateCalls);
        ActiveStateReadResult active = await fixture.Store.ReadActiveAsync(fixture.Scope.ScopeId);
        Assert.Equal("thread-original", active.Lineage!.ProviderSessionId);
    }

    private sealed class FixedSource : IRecoverySource
    {
        public string Kind => "Repository";

        public Task<RecoverySourceObservation?> ObserveAsync(RecoverySourceRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = new RecoverySourceDescriptor(
                2, Kind, "repository-products", new string('c', 64), "snapshot", "repository.v1",
                RecoveryCompleteness.RepositoryOnly, [], new Dictionary<string, string> { ["scope"] = request.ScopeId });
            return Task.FromResult<RecoverySourceObservation?>(new RecoverySourceObservation(
                descriptor,
                [new SessionContentRecord(0, "repository", "repository", "accepted plan", null, new Dictionary<string, string>())]));
        }
    }

    private sealed class FixedEnvelopeFactory : IRecoveryEnvelopeFactory
    {
        public RecoveryEnvelopePayload Build(
            string attemptId,
            string scopeId,
            ProviderSessionReference original,
            RecoveryFailure failure,
            IReadOnlyList<RecoverySourceObservation> sources,
            SessionContinuityProfile profile,
            int contextBudget) =>
            new("marker-attempt", "recovery envelope; reply marker-attempt", new string('d', 64),
                new Dictionary<string, string> { ["schema"] = "recovery-envelope.v1" });
    }

    private sealed class FakeContinuityRuntime(SessionContinuityProfile profile) : IAgentSessionContinuityRuntime
    {
        public SessionResumeOutcome OriginalResumeOutcome { get; init; } = SessionResumeOutcome.UnavailableSession;
        public int CreateCalls { get; private set; }
        public int SeedCalls { get; private set; }
        public int ForkCalls { get; private set; }
        public bool ForkEnabled { get; init; }
        public string? LastMarker { get; private set; }

        public Task<SessionResumeResult> ResumeSessionAsync(SessionResumeRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Original.ThreadId == "thread-child")
            {
                IAgentSession child = new FakeSession(request.SessionSpec, "thread-child");
                return Task.FromResult(new SessionResumeResult(
                    SessionResumeOutcome.SuccessfulResume, child, request.Original, request.Original,
                    SessionOperationStage.Completed, null, Transport(), profile.Digest, request.Attempt));
            }

            var failure = new SessionOperationFailure(
                OriginalResumeOutcome.ToString(), "thread/resume", 1, null, default, "redacted", false, false);
            return Task.FromResult(new SessionResumeResult(
                OriginalResumeOutcome, null, request.Original, null, SessionOperationStage.OperationResponse,
                failure, Transport(), profile.Digest, request.Attempt));
        }

        public Task<SessionCreateResult> CreateSessionAsync(SessionCreateRequest request, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            IAgentSession child = new FakeSession(request.SessionSpec, "thread-child");
            return Task.FromResult(new SessionCreateResult(
                true, child, new ProviderSessionReference("codex", "thread-child"),
                SessionOperationStage.Completed, null, Transport(requestSubmitted: true), profile.Digest));
        }

        public Task<SessionSeedResult> SeedSessionAsync(SessionSeedRequest request, CancellationToken cancellationToken = default)
        {
            SeedCalls++;
            LastMarker = request.Marker;
            return Task.FromResult(new SessionSeedResult(
                true, null, "turn-seed", $"accepted {request.Marker}", Transport(requestSubmitted: true, turnSubmitted: true)));
        }

        public Task<SessionContinuityNegotiationResult> NegotiateAsync(SessionContinuityNegotiationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SessionContinuityNegotiationResult(profile, true, "fixture"));
        public Task<SessionContentResult> ReadSessionAsync(SessionContentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SessionContentResult(false, null, null));
        public Task<SessionForkResult> ForkSessionAsync(SessionForkRequest request, CancellationToken cancellationToken = default)
        {
            ForkCalls++;
            if (!ForkEnabled)
            {
                return Task.FromResult(new SessionForkResult(false, null, request.Parent, null, null));
            }
            IAgentSession childSession = new FakeSession(request.SessionSpec, "thread-child");
            return Task.FromResult(new SessionForkResult(
                true, childSession, request.Parent,
                new ProviderSessionReference("codex", "thread-child"), null,
                Transport(requestSubmitted: true), "history-digest"));
        }
        public Task<SessionReconcileResult> ReconcileAsync(SessionReconcileRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SessionReconcileResult(false, null, null));

        private static SessionTransportProgress Transport(bool requestSubmitted = false, bool turnSubmitted = false) =>
            new(requestSubmitted, requestSubmitted, requestSubmitted, turnSubmitted, null, null);
    }

    private sealed class FakeSession(AgentSessionSpec spec, string threadId) : IAgentSession
    {
        public SessionIdentity SessionId => spec.SessionId;
        public string RepositoryId => spec.RepositoryId;
        public SessionRole Role => spec.Role;
        public AgentSessionMode Mode => AgentSessionMode.Persistent;
        public AgentProcessState State => AgentProcessState.Running;
        public int CompletedTurns => 0;
        public AgentTokenUsage TotalUsage => AgentTokenUsage.Zero;
        public string? ThreadId => threadId;
        public Task<AgentTurnResult> RunTurnAsync(string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FrozenTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed record Fixture(
        Repository Repository,
        SqliteRecoveryStore Store,
        SessionContinuityProfile Profile,
        DecisionSessionScopeRecord Scope,
        DecisionSessionLineageNode Original,
        DecisionSessionActiveState Active)
    {
        public static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        public static async Task<Fixture> CreateAsync()
        {
            string path = Directory.CreateTempSubdirectory("looprelay-recovery-runtime-").FullName;
            var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
            var store = new SqliteRecoveryStore(repository);
            SessionContinuityProfile profile = CreateProfile();
            var scope = new DecisionSessionScopeRecord(
                "scope-runtime", "workspace", new string('a', 64), new string('b', 64),
                "Decision", "decision-session-scope.v1", "Active", Now, null);
            var original = new DecisionSessionLineageNode(
                "lineage-original", scope.ScopeId, "codex", "thread-original", null, "lineage-original",
                "Fresh", RecoveryCompleteness.Full, null, profile.Digest, null, Now, Now, null, "Authoritative");
            var active = new DecisionSessionActiveState(
                scope.ScopeId, original.LineageId,
                new DecisionSessionAccounting(0, 0, 0, 0, 0, 0, 0, null, 0),
                "policy", null, 0, Now);
            Assert.True((await store.CreateScopeAndActivateAsync(scope, original, active, profile)).Succeeded);
            return new Fixture(repository, store, profile, scope, original, active);
        }

        public RecoveryRuntimeRequest Request()
        {
            AgentSessionSpec Spec(string? resume) => new(
                SessionIdentity.New(), Repository.Id.ToString(), SessionRole.Decision,
                new SandboxProfile("read-only", false, false, false),
                new EffortProfile(AgentEffortLevel.High, "high"),
                Repository.Path, resumeThreadId: resume);
            return new RecoveryRuntimeRequest(
                Scope.ScopeId, "run-1", Spec("thread-original"), Spec(null), Profile,
                new Dictionary<string, string> { ["rank:RepositoryReconstruction@1"] = "1" },
                100_000, "ExecuteRestart");
        }

        private static SessionContinuityProfile CreateProfile()
        {
            SessionOperationSupportDescriptor Supported(string protocol) => new(
                SessionOperationSupport.Supported, protocol,
                new Dictionary<string, SessionParameterSupport>(), "test", "test", "none", "exact", "fixture");
            return new SessionContinuityProfile(
                "codex", "test", "0.142.5", "codex", "v2", "schema",
                new Dictionary<string, bool> { ["experimentalApi"] = true },
                new Dictionary<string, string>(),
                new Dictionary<SessionContinuityOperation, SessionOperationSupportDescriptor>
                {
                    [SessionContinuityOperation.Resume] = Supported("thread/resume"),
                    [SessionContinuityOperation.ConversationWrite] = Supported("turn/start"),
                    [SessionContinuityOperation.Fork] = new SessionOperationSupportDescriptor(
                        SessionOperationSupport.Supported, "thread/fork",
                        new Dictionary<string, SessionParameterSupport>(),
                        "clone", "stable-parent-child", "none", "enumerate-exact-children", "fixture"),
                },
                256_000, "fixture", "fixture", negotiatedAt: DateTimeOffset.UnixEpoch);
        }
    }
}
