using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Interactions;
using Xunit;

namespace LoopRelay.Orchestration.Tests.Interactions;

public sealed class InteractionBrokerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-12T12:00:00Z");

    [Fact]
    public async Task Request_is_persisted_before_presentation_and_survives_restart()
    {
        Repository repository = CreateRepository();
        InteractionRequest request = Request();
        var broker = new InteractionBroker(new CanonicalInteractionStore(repository), new FrozenTimeProvider(Now));

        InteractionAggregate persisted = await broker.CreateAsync(new CreateInteractionCommand(request));

        Assert.Equal(InteractionLifecycle.Persisted, persisted.State);
        Assert.Equal([InteractionLifecycle.Persisted], persisted.Events.Select(item => item.Lifecycle));
        var restarted = new InteractionBroker(new CanonicalInteractionStore(repository), new FrozenTimeProvider(Now));
        InteractionAggregate outstanding = Assert.Single(await restarted.ListAsync(new ListInteractionsQuery()));
        Assert.Equal(request.Identity, outstanding.Request.Identity);
        Assert.Equal(request.Policy.ResponseJsonSchema, outstanding.Request.Policy.ResponseJsonSchema);

        InteractionAggregate presented = await restarted.PresentAsync(request.Identity, outstanding.RowVersion);
        Assert.Equal(InteractionLifecycle.Presented, presented.State);
        Assert.Equal([InteractionLifecycle.Persisted, InteractionLifecycle.Presented],
            presented.Events.Select(item => item.Lifecycle));
    }

    [Fact]
    public async Task Valid_response_is_immutable_and_only_resolution_authorizes_resume()
    {
        Repository repository = CreateRepository();
        var broker = new InteractionBroker(new CanonicalInteractionStore(repository), new FrozenTimeProvider(Now));
        InteractionAggregate aggregate = await CreateAndPresentAsync(broker);

        InteractionResponseResult accepted = await broker.RespondAsync(Respond(aggregate, """{"accept":true}""", "response-1"));

        Assert.True(accepted.Accepted);
        Assert.False(accepted.IdempotentDuplicate);
        Assert.Equal(InteractionLifecycle.Validated, accepted.Aggregate!.State);
        Assert.False(accepted.Aggregate.ResumeAuthorized);
        Assert.Equal([InteractionLifecycle.Persisted, InteractionLifecycle.Presented,
            InteractionLifecycle.Responded, InteractionLifecycle.Validated],
            accepted.Aggregate.Events.Select(item => item.Lifecycle));

        InteractionAggregate resolved = await broker.ResolveAsync(new ResolveInteractionCommand(
            aggregate.Request.Identity, accepted.Aggregate.RowVersion, ["resolver-authorized"]));
        Assert.Equal(InteractionLifecycle.ResumeAuthorized, resolved.State);
        Assert.True(resolved.ResumeAuthorized);
        Assert.Empty(await broker.ListAsync(new ListInteractionsQuery()));
    }

    [Fact]
    public async Task Invalid_and_late_responses_are_typed_rejections_and_leave_state_unchanged()
    {
        Repository repository = CreateRepository();
        var broker = new InteractionBroker(new CanonicalInteractionStore(repository), new FrozenTimeProvider(Now));
        InteractionAggregate aggregate = await CreateAndPresentAsync(broker);

        InteractionResponseResult invalid = await broker.RespondAsync(Respond(
            aggregate, """{"accept":"yes"}""", "invalid"));
        Assert.Equal(InteractionRejectionReason.SchemaInvalid, invalid.Rejection);
        Assert.Equal(InteractionLifecycle.Presented, invalid.Aggregate!.State);
        Assert.Null(invalid.Aggregate.AcceptedResponse);

        InteractionRequest lateRequest = Request(policy: InteractionCategoryPolicyRegistry.Resolve(
            InteractionCategory.DirtyInputCommitOffer, "dirty-policy", Now).WithDeadline(Now.AddMinutes(-1)));
        InteractionAggregate late = await broker.CreateAsync(new CreateInteractionCommand(lateRequest));
        late = await broker.PresentAsync(lateRequest.Identity, late.RowVersion);
        InteractionResponseResult lateResult = await broker.RespondAsync(Respond(late, """{"accept":true}""", "late"));
        Assert.Equal(InteractionRejectionReason.Late, lateResult.Rejection);
        Assert.Equal(InteractionLifecycle.Presented, lateResult.Aggregate!.State);
        Assert.Null(lateResult.Aggregate.AcceptedResponse);
    }

    [Fact]
    public async Task Mutation_response_without_declared_trust_evidence_is_rejected()
    {
        Repository repository = CreateRepository();
        var broker = new InteractionBroker(new CanonicalInteractionStore(repository), new FrozenTimeProvider(Now));
        InteractionAggregate aggregate = await CreateAndPresentAsync(broker);

        InteractionResponseResult result = await broker.RespondAsync(new RespondInteractionCommand(
            aggregate.Request.Identity, """{"accept":true}""", "response-untrusted", "anonymous",
            ["responder-authentication"], aggregate.RowVersion));

        Assert.Equal(InteractionRejectionReason.MissingTrustEvidence, result.Rejection);
        Assert.Equal(InteractionLifecycle.Presented, result.Aggregate!.State);
        Assert.Null(result.Aggregate.AcceptedResponse);
    }

    [Fact]
    public async Task Identical_duplicate_returns_existing_response_and_conflicting_duplicate_cannot_replace_it()
    {
        Repository repository = CreateRepository();
        var broker = new InteractionBroker(new CanonicalInteractionStore(repository), new FrozenTimeProvider(Now));
        InteractionAggregate aggregate = await CreateAndPresentAsync(broker);
        InteractionResponseResult first = await broker.RespondAsync(Respond(
            aggregate, """{"accept":true}""", "response-1"));

        InteractionResponseResult duplicate = await broker.RespondAsync(new RespondInteractionCommand(
            aggregate.Request.Identity, """{"accept":true}""", "same-semantics-new-delivery-key", "operator-1",
            ["responder-authentication", "mutation-authorization"], ExpectedRowVersion: 0));
        Assert.True(duplicate.Accepted);
        Assert.True(duplicate.IdempotentDuplicate);
        Assert.Equal(first.Response!.Identity, duplicate.Response!.Identity);

        InteractionResponseResult conflict = await broker.RespondAsync(new RespondInteractionCommand(
            aggregate.Request.Identity, """{"accept":false}""", "response-2", "operator-1",
            ["responder-authentication", "mutation-authorization"], first.Aggregate!.RowVersion));
        Assert.Equal(InteractionRejectionReason.SemanticIdempotencyConflict, conflict.Rejection);
        Assert.Equal(first.Response.Identity, conflict.Aggregate!.AcceptedResponse!.Identity);
        Assert.Equal(InteractionLifecycle.Validated, conflict.Aggregate.State);
    }

    [Fact]
    public async Task Compare_and_set_conflict_cancellation_and_expiration_are_durable()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalInteractionStore(repository);
        var broker = new InteractionBroker(store, new FrozenTimeProvider(Now));
        InteractionAggregate aggregate = await CreateAndPresentAsync(broker);

        InteractionResponseResult conflict = await broker.RespondAsync(new RespondInteractionCommand(
            aggregate.Request.Identity, """{"accept":true}""", "response-1", "operator-1",
            ["responder-authentication", "mutation-authorization"], aggregate.RowVersion - 1));
        Assert.Equal(InteractionRejectionReason.CompareAndSetConflict, conflict.Rejection);
        Assert.Null(conflict.Aggregate!.AcceptedResponse);
        Assert.Equal(InteractionLifecycle.Presented, conflict.Aggregate.State);

        InteractionAggregate cancelled = await broker.CancelAsync(new CancelInteractionCommand(
            aggregate.Request.Identity, "Operator declined the action.", conflict.Aggregate.RowVersion));
        Assert.Equal(InteractionLifecycle.Cancelled, cancelled.State);

        InteractionRequest expiring = Request();
        InteractionAggregate expiringAggregate = await broker.CreateAsync(new CreateInteractionCommand(expiring));
        expiringAggregate = await broker.PresentAsync(expiring.Identity, expiringAggregate.RowVersion);
        InteractionAggregate expired = await store.AppendEventAsync(new InteractionLifecycleEvent(
            InteractionEventIdentity.New(), expiring.Identity, InteractionLifecycle.Expired,
            "Declared policy deadline elapsed.", ["deadline-evaluation"], Now), expiringAggregate.RowVersion);
        Assert.Equal(InteractionLifecycle.Expired, expired.State);
        Assert.DoesNotContain(expired, await broker.ListAsync(new ListInteractionsQuery()));
    }

    [Fact]
    public void Category_policy_has_no_hidden_timeout_or_default_and_declares_trust_evidence()
    {
        InteractionCategoryPolicy policy = InteractionCategoryPolicyRegistry.Resolve(
            InteractionCategory.DirtyInputCommitOffer, "dirty-policy", Now);

        Assert.Equal(InteractionDeadlineBehavior.None, policy.DeadlineBehavior);
        Assert.Null(policy.Deadline);
        Assert.Null(policy.DefaultResponseJson);
        Assert.Equal("InteractionBroker/DirtyInputResolver", policy.ResolverOwner);
        Assert.Equal(["responder-authentication", "mutation-authorization"], policy.RequiredTrustEvidence);
    }

    private static async Task<InteractionAggregate> CreateAndPresentAsync(InteractionBroker broker)
    {
        InteractionRequest request = Request();
        InteractionAggregate aggregate = await broker.CreateAsync(new CreateInteractionCommand(request));
        return await broker.PresentAsync(request.Identity, aggregate.RowVersion);
    }

    private static RespondInteractionCommand Respond(
        InteractionAggregate aggregate,
        string response,
        string idempotency) => new(
        aggregate.Request.Identity, response, idempotency, "operator-1",
        ["responder-authentication", "mutation-authorization"], aggregate.RowVersion);

    private static InteractionRequest Request(InteractionCategoryPolicy? policy = null)
    {
        var causality = new CanonicalCausalContext(
            WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(), AttemptIdentity.New());
        return new InteractionRequest(
            InteractionRequestIdentity.New(), InteractionCategory.DirtyInputCommitOffer,
            new InteractionCausalSubject(causality, "declared-input-surface", "surface:roadmap"),
            "Commit the declared dirty input surface?", """{"surface":"roadmap"}""",
            policy ?? InteractionCategoryPolicyRegistry.Resolve(
                InteractionCategory.DirtyInputCommitOffer, "dirty-policy", Now),
            ["git-status:dirty", "declared-surface:roadmap"], $"dirty-offer:{Guid.NewGuid():N}", Now);
    }

    private static Repository CreateRepository()
    {
        string root = Directory.CreateTempSubdirectory("canonical-interaction-store").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(root), Path = root };
    }

    private sealed class FrozenTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

file static class InteractionPolicyTestExtensions
{
    public static InteractionCategoryPolicy WithDeadline(
        this InteractionCategoryPolicy policy,
        DateTimeOffset deadline) => policy with
    {
        DeadlineBehavior = InteractionDeadlineBehavior.ExpiresAt,
        Deadline = deadline,
    };
}
