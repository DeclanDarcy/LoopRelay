using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Orchestration.Tests.Recovery;

public sealed class RecoveryMechanismTests
{
    [Fact]
    public void CatalogResolutionIsStableAndRejectsUnknownVersion()
    {
        var catalog = new RecoveryMechanismCatalog(
        [
            new RepositoryReconstructionMechanism(),
            new ThreadReadReconstructionMechanism(),
            new RolloutReconstructionMechanism(),
        ]);

        Assert.Equal(
            ["RepositoryReconstruction", "RolloutReconstruction", "ThreadReadReconstruction"],
            catalog.All.Select(mechanism => mechanism.Key.Identity));
        Assert.IsType<ThreadReadReconstructionMechanism>(catalog.Resolve(new RecoveryMechanismKey("ThreadReadReconstruction", "1")));
        Assert.Throws<InvalidOperationException>(() =>
            catalog.Resolve(new RecoveryMechanismKey("ThreadReadReconstruction", "2")));
    }

    [Theory]
    [InlineData(SessionOperationSupport.Unknown)]
    [InlineData(SessionOperationSupport.Unsupported)]
    public void TextualMechanismsAreIneligibleWhenWriteSupportIsNotProven(SessionOperationSupport write)
    {
        RecoveryPlanningInput input = Input(write: write, read: SessionOperationSupport.Supported, sourceKind: "ThreadRead");

        RecoveryMechanismEligibility result = new ThreadReadReconstructionMechanism().EvaluateEligibility(input);

        Assert.False(result.Eligible);
    }

    [Fact]
    public void ThreadReadRequiresReadSupportWhileRepositoryDoesNot()
    {
        RecoveryPlanningInput threadInput = Input(
            write: SessionOperationSupport.Supported, read: SessionOperationSupport.Unknown, sourceKind: "ThreadRead");
        RecoveryPlanningInput repositoryInput = Input(
            write: SessionOperationSupport.Supported, read: SessionOperationSupport.Unknown, sourceKind: "Repository");

        Assert.False(new ThreadReadReconstructionMechanism().EvaluateEligibility(threadInput).Eligible);
        Assert.True(new RepositoryReconstructionMechanism().EvaluateEligibility(repositoryInput).Eligible);
    }

    [Fact]
    public void UnknownMaximumContextMakesTextualRecoveryIneligible()
    {
        RecoveryPlanningInput input = Input(
            write: SessionOperationSupport.Supported, read: SessionOperationSupport.Supported, sourceKind: "RolloutSalvage",
            maximumContext: null);

        Assert.False(new RolloutReconstructionMechanism().EvaluateEligibility(input).Eligible);
    }

    [Theory]
    [InlineData(SessionOperationSupport.Unknown)]
    [InlineData(SessionOperationSupport.Unsupported)]
    public void NativeForkIsProfileGated(SessionOperationSupport fork)
    {
        RecoveryPlanningInput input = Input(
            SessionOperationSupport.Supported, SessionOperationSupport.Supported, "Repository",
            fork: fork);

        Assert.False(new NativeForkRecoveryMechanism().EvaluateEligibility(input).Eligible);
    }

    [Fact]
    public void NativeForkRequiresStableCertifiedIdentityAndReconciliation()
    {
        RecoveryPlanningInput input = Input(
            SessionOperationSupport.Supported, SessionOperationSupport.Supported, "Repository",
            fork: SessionOperationSupport.Supported);

        RecoveryMechanismEligibility result = new NativeForkRecoveryMechanism().EvaluateEligibility(input);

        Assert.True(result.Eligible);
        Assert.Equal(RecoveryCompleteness.Full, result.Completeness);
    }

    private static RecoveryPlanningInput Input(
        SessionOperationSupport write,
        SessionOperationSupport read,
        string sourceKind,
        int? maximumContext = 256_000,
        SessionOperationSupport fork = SessionOperationSupport.Unknown)
    {
        SessionContinuityProfile profile = Profile(write, read, maximumContext, fork);
        var source = new RecoverySourceDescriptor(
            0, sourceKind, "source", new string('b', 64), "boundary", "normalizer.v1",
            sourceKind == "Repository" ? RecoveryCompleteness.RepositoryOnly : RecoveryCompleteness.Full,
            [], new Dictionary<string, string>());
        IReadOnlyList<RecoverySourceDescriptor> sources = sourceKind == "Repository"
            ? [source]
            :
            [
                source,
                new RecoverySourceDescriptor(
                    2, "Repository", "repository", new string('r', 64), "snapshot", "repository.v1",
                    RecoveryCompleteness.RepositoryOnly, [], new Dictionary<string, string>()),
            ];
        return new RecoveryPlanningInput(
            new RecoveryFailure("UnavailableSession", "thread/resume", null, profile.Digest, "redacted", false),
            "scope", profile, sources, new Dictionary<string, string>(), 100_000,
            new string('e', 64), new Dictionary<string, string>());
    }

    private static SessionContinuityProfile Profile(
        SessionOperationSupport write,
        SessionOperationSupport read,
        int? maximumContext,
        SessionOperationSupport fork)
    {
        SessionOperationSupportDescriptor Descriptor(SessionOperationSupport status, string protocol) =>
            new(status, protocol, new Dictionary<string, SessionParameterSupport>(), "test", "test", "none", "test", "test");
        return new SessionContinuityProfile(
            "codex", "test", "test", "codex", "v2", "schema",
            new Dictionary<string, bool> { ["experimentalApi"] = true }, new Dictionary<string, string>(),
            new Dictionary<SessionContinuityOperation, SessionOperationSupportDescriptor>
            {
                [SessionContinuityOperation.ConversationRead] = Descriptor(read, "thread/read"),
                [SessionContinuityOperation.ConversationWrite] = Descriptor(write, "turn/start"),
                [SessionContinuityOperation.Fork] = new SessionOperationSupportDescriptor(
                    fork, "thread/fork", new Dictionary<string, SessionParameterSupport>(),
                    "clone", "stable-parent-child", "none", "enumerate-exact-children", "fixture"),
            },
            maximumContext, maximumContext is null ? "unknown" : "test", "test", negotiatedAt: DateTimeOffset.UnixEpoch);
    }
}
