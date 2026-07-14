using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Runtime;

public sealed class CausalRuntimeContractTests
{
    [Fact]
    public void Canonical_transition_context_mints_attempts_under_its_run_and_workflow()
    {
        CanonicalTransitionExecutionContext execution = NewExecutionContext();

        CanonicalCausalContext attempt = execution.BeginAttempt(
            TransitionRunIdentity.New(),
            AttemptIdentity.New());

        Assert.Equal(execution.Workspace, attempt.Workspace);
        Assert.Equal(execution.Run, attempt.Run);
        Assert.Equal(execution.WorkflowInstance, attempt.WorkflowInstance);
    }

    [Fact]
    public void Legacy_context_requires_an_explicit_compatibility_source()
    {
        Assert.Throws<ArgumentException>(() => new LegacyTransitionExecutionContext(
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            ""));
    }

    [Fact]
    public void Rendered_prompt_fact_rejects_a_content_hash_mismatch()
    {
        CanonicalCausalContext attempt = NewAttempt();

        Assert.Throws<ArgumentException>(() => new RenderedPromptFact(
            RenderedPromptFactIdentity.New(),
            attempt,
            "rendered",
            "wrong",
            new PromptTemplateIdentity("template.test.v1"),
            "source-hash",
            new PolicyIdentity("policy_test"),
            new PromptPolicyProfileIdentity("prompt-policy.test.v1"),
            ConsumedInputManifestIdentity.New(),
            [],
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Runtime_dispatch_rejects_a_prompt_from_another_attempt()
    {
        PolicyIdentity policy = new("policy_test");
        PersistedRenderedPromptFact prompt = Persist(NewPrompt(NewAttempt(), policy));
        var dispatch = new AuthorizedPromptDispatch(
            PromptDispatchIdentity.New(),
            prompt.Fact.Identity,
            prompt.PersistenceIdentity,
            Authorization(NewAttempt(), policy));
        var runtime = new LoadingPromptRuntimeDispatcher(new SinglePromptReader(prompt), new NoOpTransport());

        await Assert.ThrowsAsync<ArgumentException>(() => runtime.DispatchAsync(dispatch, CancellationToken.None));
    }

    [Fact]
    public async Task Runtime_dispatch_rejects_a_different_session_policy()
    {
        CanonicalCausalContext attempt = NewAttempt();
        PersistedRenderedPromptFact prompt = Persist(NewPrompt(attempt, new PolicyIdentity("policy_one")));
        var dispatch = new AuthorizedPromptDispatch(
            PromptDispatchIdentity.New(),
            prompt.Fact.Identity,
            prompt.PersistenceIdentity,
            Authorization(attempt, new PolicyIdentity("policy_two")));
        var runtime = new LoadingPromptRuntimeDispatcher(new SinglePromptReader(prompt), new NoOpTransport());

        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.DispatchAsync(dispatch, CancellationToken.None));
    }

    private static CanonicalTransitionExecutionContext NewExecutionContext() =>
        new(
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            WorkspaceIdentity.New(),
            RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            new PolicyIdentity("policy_test"),
            new RuntimeProfileIdentity("runtime_profile_test"),
            new PromptPolicyProfileIdentity("prompt-policy.test.v1"));

    private static CanonicalCausalContext NewAttempt() =>
        NewExecutionContext().BeginAttempt(TransitionRunIdentity.New(), AttemptIdentity.New());

    private static RenderedPromptFact NewPrompt(
        CanonicalCausalContext causality,
        PolicyIdentity policy)
    {
        const string content = "rendered";
        return new RenderedPromptFact(
            RenderedPromptFactIdentity.New(),
            causality,
            content,
            RenderedPromptFact.ComputeContentHash(content),
            new PromptTemplateIdentity("template.test.v1"),
            "source-hash",
            policy,
            new PromptPolicyProfileIdentity("prompt-policy.test.v1"),
            ConsumedInputManifestIdentity.New(),
            [],
            DateTimeOffset.UtcNow);
    }

    private static PersistedRenderedPromptFact Persist(RenderedPromptFact fact) =>
        new(
            fact,
            RenderedPromptPersistenceIdentity.New(),
            1,
            DateTimeOffset.UtcNow);

    private static PromptDispatchAuthorization Authorization(
        CanonicalCausalContext causality,
        PolicyIdentity policy) => new(
        causality,
        policy,
        new PromptPolicyProfileIdentity("prompt-policy.test.v1"),
        new RuntimeProfileIdentity("runtime_profile_test"),
        new WorkflowTransitionIdentity("transition_test"),
        "snapshot_test");

    private sealed class SinglePromptReader(PersistedRenderedPromptFact prompt) : IRenderedPromptFactReader
    {
        public Task<PersistedRenderedPromptFact?> ReadAsync(
            RenderedPromptFactIdentity identity,
            CancellationToken cancellationToken) => Task.FromResult<PersistedRenderedPromptFact?>(prompt);
    }

    private sealed class NoOpTransport : IProviderPromptTransport
    {
        public Task<PromptExecutionResult> DispatchAsync(
            PersistedRenderedPromptFact prompt,
            AuthorizedPromptDispatch dispatch,
            CancellationToken cancellationToken) => Task.FromResult(new PromptExecutionResult(
                PromptExecutionStatus.Completed, "ok", TimeSpan.Zero, new Dictionary<string, string>()));
    }
}
