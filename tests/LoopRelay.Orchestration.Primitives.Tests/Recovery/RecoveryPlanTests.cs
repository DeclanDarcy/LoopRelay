using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Orchestration.Tests.Recovery;

public sealed class RecoveryPlanTests
{
    [Fact]
    public void CanonicalDigestIsStableAcrossDictionaryAndOmissionOrdering()
    {
        RecoveryPlan first = Plan(
            constraints: new Dictionary<string, string> { ["z"] = "2", ["a"] = "1" },
            omissions: ["z", "a"]);
        RecoveryPlan second = Plan(
            constraints: new Dictionary<string, string> { ["a"] = "1", ["z"] = "2" },
            omissions: ["a", "z"]);

        Assert.Equal(first.Digest, second.Digest);
        Assert.Equal(RecoveryPlanSerializer.Serialize(first), RecoveryPlanSerializer.Serialize(second));
        Assert.Matches("^[a-f0-9]{64}$", first.Digest);
    }

    [Fact]
    public void MechanismVersionAndProfileAreDigestMaterial()
    {
        RecoveryPlan baseline = Plan();
        RecoveryPlan changedMechanism = Plan(mechanism: new RecoveryMechanismKey("RepositoryReconstruction", "2"));
        RecoveryPlan changedProfile = Plan(profileDigest: new string('f', 64));

        Assert.NotEqual(baseline.Digest, changedMechanism.Digest);
        Assert.NotEqual(baseline.Digest, changedProfile.Digest);
    }

    internal static RecoveryPlan Plan(
        RecoveryMechanismKey? mechanism = null,
        IReadOnlyDictionary<string, string>? constraints = null,
        IReadOnlyList<string>? omissions = null,
        string? profileDigest = null) =>
        new(
            "plan-1",
            "recovery-plan.v1",
            "planner.v1",
            "policy.v1",
            mechanism ?? new RecoveryMechanismKey("RepositoryReconstruction", "1"),
            ["eligible", "rank=1"],
            [new RecoverySourceDescriptor(
                0, "Repository", ".agents", new string('b', 64), "products:v1", "normalizer.v1",
                RecoveryCompleteness.RepositoryOnly, ["provider-history"],
                new Dictionary<string, string> { ["verified"] = "true" })],
            new string('c', 64),
            new Dictionary<string, string> { ["format"] = "recovery-envelope.v1" },
            RecoveryActivationStrategy.EagerCreateAndInject,
            "marker-and-digest",
            "thread-read-marker",
            RecoveryCompleteness.RepositoryOnly,
            omissions ?? ["provider-history"],
            profileDigest ?? new string('a', 64),
            constraints ?? new Dictionary<string, string> { ["conversationWrite"] = "Supported" },
            "idem-plan-1",
            1,
            "fail-closed");
}
