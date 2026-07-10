using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Orchestration.Tests.Recovery;

public sealed class RecoveryPlannerDeterminismTests
{
    [Fact]
    public void RegistrationAndSourceEnumerationOrderDoNotChangeThePlanDigest()
    {
        RecoveryPlanningInput input = Input();
        IRecoveryMechanism[] mechanisms =
        [
            new RepositoryReconstructionMechanism(),
            new RolloutReconstructionMechanism(),
            new ThreadReadReconstructionMechanism(),
        ];
        var planner = new RecoveryPlanner();

        RecoveryPlan first = planner.Plan(input, mechanisms);
        RecoveryPlan second = planner.Plan(
            input with { Sources = input.Sources.Reverse().ToArray() },
            mechanisms.Reverse().ToArray());

        Assert.Equal(first.Digest, second.Digest);
        Assert.Equal("ThreadReadReconstruction", first.Mechanism.Identity);
        Assert.Equal("envelope-digest", first.EnvelopeDigest);
    }

    [Fact]
    public void RankingIsPolicyDataRatherThanCatalogOrder()
    {
        RecoveryPlanningInput input = Input() with
        {
            Policy = new Dictionary<string, string>
            {
                ["policy-version"] = "test-ranking.v1",
                ["rank:RepositoryReconstruction@1"] = "1",
                ["rank:ThreadReadReconstruction@1"] = "100",
                ["rank:RolloutReconstruction@1"] = "200",
            },
        };

        RecoveryPlan plan = new RecoveryPlanner().Plan(input,
        [
            new ThreadReadReconstructionMechanism(),
            new RepositoryReconstructionMechanism(),
            new RolloutReconstructionMechanism(),
        ]);

        Assert.Equal("RepositoryReconstruction", plan.Mechanism.Identity);
        Assert.Equal(RecoveryCompleteness.RepositoryOnly, plan.ExpectedCompleteness);
    }

    [Theory]
    [InlineData("DeterministicProtocolFailure", false)]
    [InlineData("AuthenticationFailure", false)]
    [InlineData("UnknownOutcome", false)]
    [InlineData("UnavailableSession", true)]
    public void NonReplacementFailuresFailClosed(string classification, bool turnSubmitted)
    {
        RecoveryPlanningInput input = Input() with
        {
            Failure = new RecoveryFailure(classification, "thread/resume", null, null, "redacted", turnSubmitted),
        };

        Assert.Throws<RecoveryPlanningException>(() => new RecoveryPlanner().Plan(
            input, [new RepositoryReconstructionMechanism()]));
    }

    private static RecoveryPlanningInput Input()
    {
        SessionOperationSupportDescriptor Supported(string protocol) => new(
            SessionOperationSupport.Supported, protocol,
            new Dictionary<string, SessionParameterSupport>(), "test", "test", "none", "exact", "fixture");
        SessionContinuityProfile profile = new(
            "codex", "test", "0.142.5", "codex", "v2", "schema",
            new Dictionary<string, bool> { ["experimentalApi"] = true },
            new Dictionary<string, string>(),
            new Dictionary<SessionContinuityOperation, SessionOperationSupportDescriptor>
            {
                [SessionContinuityOperation.ConversationRead] = Supported("thread/read"),
                [SessionContinuityOperation.ConversationWrite] = Supported("turn/start"),
            },
            256_000, "fixture", "fixture", negotiatedAt: DateTimeOffset.UnixEpoch);
        RecoverySourceDescriptor Source(int order, string kind, RecoveryCompleteness completeness) => new(
            order, kind, kind, new string((char)('a' + order), 64), "boundary", "normalizer.v1",
            completeness, [], new Dictionary<string, string>());
        return new RecoveryPlanningInput(
            new RecoveryFailure("UnavailableSession", "thread/resume", null, profile.Digest, "redacted", false),
            "scope", profile,
            [
                Source(0, "ThreadRead", RecoveryCompleteness.Full),
                Source(1, "RolloutSalvage", RecoveryCompleteness.Selective),
                Source(2, "Repository", RecoveryCompleteness.RepositoryOnly),
            ],
            new Dictionary<string, string>
            {
                ["policy-version"] = "test-ranking.v1",
                ["rank:ThreadReadReconstruction@1"] = "1",
                ["rank:RolloutReconstruction@1"] = "2",
                ["rank:RepositoryReconstruction@1"] = "3",
            },
            100_000,
            "envelope-digest",
            new Dictionary<string, string> { ["schema"] = "recovery-envelope.v1" });
    }
}
