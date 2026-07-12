using LoopRelay.Core.Models.Identity;

namespace LoopRelay.Core.Tests.Models.Identity;

public sealed class CanonicalCausalContextTests
{
    [Fact]
    public void Turn_requires_a_canonical_session()
    {
        Assert.Throws<ArgumentException>(() => new CanonicalCausalContext(
            WorkspaceIdentity.New(),
            RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(),
            AttemptIdentity.New(),
            turn: TurnIdentity.New()));
    }

    [Fact]
    public void RequireSameAttempt_rejects_a_different_causal_tree()
    {
        CanonicalCausalContext left = NewContext();
        CanonicalCausalContext right = NewContext();

        Assert.Throws<ArgumentException>(() => left.RequireSameAttempt(right, nameof(right)));
    }

    [Fact]
    public void Session_and_turn_evidence_preserve_attempt_ownership()
    {
        CanonicalCausalContext attempt = NewContext();
        AgentSessionIdentity session = AgentSessionIdentity.New();
        CanonicalCausalContext turn = attempt.WithTurn(session, TurnIdentity.New());

        Assert.True(attempt.BelongsToSameAttempt(turn));
        Assert.Equal(session, turn.Session);
        Assert.NotNull(turn.Turn);
    }

    private static CanonicalCausalContext NewContext() =>
        new(
            WorkspaceIdentity.New(),
            RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(),
            AttemptIdentity.New());
}
