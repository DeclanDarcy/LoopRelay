using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Runtime;

public sealed class PromptDispatchGatewayTests
{
    [Fact]
    public async Task PrepareFailsClosedWhenPromptPersistenceFails()
    {
        var prompts = new RecordingPromptStore { Failure = new InvalidOperationException("store unavailable") };
        var lifecycle = new RecordingLifecycleStore();
        var runtime = new RecordingRuntime();
        var gateway = new PromptDispatchGateway(prompts, lifecycle, runtime);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.PrepareAsync(Composition(), Authorization(), CancellationToken.None));

        Assert.Empty(lifecycle.Events);
        Assert.Equal(0, runtime.CallCount);
    }

    [Fact]
    public async Task PrepareFailsClosedWhenDispatchIntentPersistenceFails()
    {
        var lifecycle = new RecordingLifecycleStore { FailOn = PromptDispatchState.Planned };
        var runtime = new RecordingRuntime();
        var gateway = new PromptDispatchGateway(new RecordingPromptStore(), lifecycle, runtime);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.PrepareAsync(Composition(), Authorization(), CancellationToken.None));

        Assert.Equal(0, runtime.CallCount);
    }

    [Fact]
    public async Task SuccessfulDispatchRecordsTheCanonicalLifecycleInOrder()
    {
        var lifecycle = new RecordingLifecycleStore();
        var runtime = new RecordingRuntime();
        var gateway = new PromptDispatchGateway(new RecordingPromptStore(), lifecycle, runtime);

        PreparedPromptDispatch prepared = await gateway.PrepareAsync(
            Composition(), Authorization(), CancellationToken.None);
        PromptExecutionResult result = await gateway.DispatchAsync(prepared, CancellationToken.None);

        Assert.Equal(PromptExecutionStatus.Completed, result.Status);
        Assert.Equal(1, runtime.CallCount);
        Assert.Equal(
            [PromptDispatchState.Planned, PromptDispatchState.Authorized,
             PromptDispatchState.Started, PromptDispatchState.Observed],
            lifecycle.Events.Select(item => item.State));
        Assert.All(lifecycle.Events, item => Assert.Equal(prepared.Dispatch.Dispatch, item.Dispatch));
    }

    [Fact]
    public async Task RuntimeExceptionRecordsUnknownAndRequiresReconciliation()
    {
        var lifecycle = new RecordingLifecycleStore();
        var runtime = new RecordingRuntime { Failure = new IOException("connection dropped") };
        var gateway = new PromptDispatchGateway(new RecordingPromptStore(), lifecycle, runtime);
        PreparedPromptDispatch prepared = await gateway.PrepareAsync(
            Composition(), Authorization(), CancellationToken.None);

        PromptDispatchUnknownException exception = await Assert.ThrowsAsync<PromptDispatchUnknownException>(() =>
            gateway.DispatchAsync(prepared, CancellationToken.None));

        Assert.Equal(prepared.Dispatch.Dispatch, exception.Dispatch.Dispatch);
        Assert.Equal(PromptDispatchState.Unknown, lifecycle.Events[^1].State);
    }

    [Fact]
    public async Task LoadingRuntimeDispatchesTheExactPersistedBytesByIdentity()
    {
        PromptDispatchAuthorization authorization = Authorization();
        PromptComposition composition = Composition();
        var store = new RecordingPromptStore();
        var lifecycle = new RecordingLifecycleStore();
        var transport = new RecordingTransport();
        var gateway = new PromptDispatchGateway(
            store,
            lifecycle,
            new LoadingPromptRuntimeDispatcher(store, transport));

        PreparedPromptDispatch prepared = await gateway.PrepareAsync(
            composition, authorization, CancellationToken.None);
        await gateway.DispatchAsync(prepared, CancellationToken.None);

        Assert.NotNull(transport.Prompt);
        Assert.Equal(composition.RenderedContent, transport.Prompt.Fact.RenderedContent);
        Assert.Equal(prepared.Dispatch.Prompt, transport.Dispatch!.Prompt);
        Assert.DoesNotContain(
            typeof(AuthorizedPromptDispatch).GetProperties(),
            property => property.PropertyType == typeof(string) && property.Name.Contains("Prompt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PolicyMismatchIsRejectedBeforePersistence()
    {
        var prompts = new RecordingPromptStore();
        var gateway = new PromptDispatchGateway(prompts, new RecordingLifecycleStore(), new RecordingRuntime());
        PromptDispatchAuthorization valid = Authorization();
        var authorization = new PromptDispatchAuthorization(
            valid.Causality,
            new PolicyIdentity("policy-other"),
            valid.PolicyProfile,
            valid.RuntimeProfile,
            valid.Transition,
            valid.InputSnapshotHash);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gateway.PrepareAsync(Composition(), authorization, CancellationToken.None));

        Assert.Equal(0, prompts.AppendCount);
    }

    private static PromptComposition Composition() => new(
        PromptCompositionIdentity.New(),
        new PromptTemplateIdentity("template-test"),
        "source-hash",
        new PolicyIdentity("policy-test"),
        new PromptPolicyProfileIdentity("prompt-profile-test"),
        ConsumedInputManifestIdentity.New(),
        [ConsumedInputFile.FromContent("roadmap.md", "roadmap")],
        new Dictionary<string, string> { ["projectContext"] = "context" },
        "exact provider-visible prompt");

    private static PromptDispatchAuthorization Authorization() => new(
        new CanonicalCausalContext(
            WorkspaceIdentity.New(),
            RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(),
            AttemptIdentity.New()),
        new PolicyIdentity("policy-test"),
        new PromptPolicyProfileIdentity("prompt-profile-test"),
        new RuntimeProfileIdentity("runtime-profile-test"),
        new WorkflowTransitionIdentity("transition-test"),
        "snapshot-hash");

    private sealed class RecordingPromptStore : IRenderedPromptStore, IRenderedPromptFactReader
    {
        private PersistedRenderedPromptFact? _persisted;
        public Exception? Failure { get; init; }
        public int AppendCount { get; private set; }

        public Task<PersistedRenderedPromptFact> AppendAsync(RenderedPromptFact fact, CancellationToken cancellationToken)
        {
            AppendCount++;
            if (Failure is not null) throw Failure;
            _persisted = new PersistedRenderedPromptFact(
                fact, RenderedPromptPersistenceIdentity.New(), 1, DateTimeOffset.UtcNow);
            return Task.FromResult(_persisted);
        }

        public Task<PersistedRenderedPromptFact?> ReadAsync(
            RenderedPromptFactIdentity prompt,
            CancellationToken cancellationToken) =>
            Task.FromResult(_persisted?.Fact.Identity == prompt ? _persisted : null);
    }

    private sealed class RecordingLifecycleStore : IPromptDispatchLifecycleStore
    {
        public PromptDispatchState? FailOn { get; init; }
        public List<PromptDispatchLifecycleEvent> Events { get; } = [];

        public Task AppendAsync(PromptDispatchLifecycleEvent dispatchEvent, CancellationToken cancellationToken)
        {
            if (dispatchEvent.State == FailOn) throw new InvalidOperationException("lifecycle unavailable");
            Events.Add(dispatchEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRuntime : IPromptRuntimeDispatcher
    {
        public int CallCount { get; private set; }
        public Exception? Failure { get; init; }

        public Task<PromptExecutionResult> DispatchAsync(
            AuthorizedPromptDispatch dispatch,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (Failure is not null) throw Failure;
            return Task.FromResult(new PromptExecutionResult(
                PromptExecutionStatus.Completed, "output", TimeSpan.Zero, new Dictionary<string, string>()));
        }
    }

    private sealed class RecordingTransport : IProviderPromptTransport
    {
        public PersistedRenderedPromptFact? Prompt { get; private set; }
        public AuthorizedPromptDispatch? Dispatch { get; private set; }

        public Task<PromptExecutionResult> DispatchAsync(
            PersistedRenderedPromptFact prompt,
            AuthorizedPromptDispatch dispatch,
            CancellationToken cancellationToken)
        {
            Prompt = prompt;
            Dispatch = dispatch;
            return Task.FromResult(new PromptExecutionResult(
                PromptExecutionStatus.Completed, "output", TimeSpan.Zero, new Dictionary<string, string>()));
        }
    }
}
