using CommandCenter.Core.Repositories;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Services;

namespace CommandCenter.DecisionSessions.Tests;

public sealed class DecisionSessionTransferEligibilityTests
{
    [Fact]
    public async Task EligibilityIsNotApplicableWhenPolicyContinues()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreatePolicy(harness.Repository.Id, active, DecisionSessionLifecycleDecision.Continue, now),
            CreateEvidenceReader(CreateEvidence(harness.Repository.Id, now, operationalContextRevisionCount: 1)),
            now);

        DecisionSessionTransferEligibilitySnapshot snapshot = await service.CheckAsync(harness.Repository.Id);

        Assert.Equal(DecisionSessionTransferEligibilityStatus.NotApplicable, snapshot.Eligibility.Status);
        Assert.Equal(DecisionSessionLifecycleDecision.Continue, snapshot.Eligibility.PolicyEvaluation.Decision);
        Assert.Contains(snapshot.Eligibility.Findings, finding => finding.Code == "policy-continue");
    }

    [Fact]
    public async Task EligibilityIsBlockedWhenNoActiveSessionExists()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreatePolicy(harness.Repository.Id, null, DecisionSessionLifecycleDecision.Transfer, now),
            CreateEvidenceReader(CreateEvidence(harness.Repository.Id, now, operationalContextRevisionCount: 1)),
            now);

        DecisionSessionTransferEligibilitySnapshot snapshot = await service.CheckAsync(harness.Repository.Id);

        Assert.Equal(DecisionSessionTransferEligibilityStatus.Blocked, snapshot.Eligibility.Status);
        Assert.Contains(snapshot.Eligibility.Findings, finding => finding.Code == "no-active-session");
    }

    [Fact]
    public async Task EligibilityIsBlockedForDuplicateActiveSessions()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession first = DecisionSession.Create(harness.Repository.Id, "test", now)
            with { State = DecisionSessionState.Active, ActivatedAt = now };
        DecisionSession second = DecisionSession.Create(harness.Repository.Id, "test", now.AddSeconds(1))
            with { State = DecisionSessionState.Active, ActivatedAt = now.AddSeconds(1) };
        await harness.WriteRegistryAsync([first, second]);
        var service = CreateService(
            harness,
            CreatePolicy(harness.Repository.Id, first, DecisionSessionLifecycleDecision.Transfer, now),
            CreateEvidenceReader(CreateEvidence(harness.Repository.Id, now, operationalContextRevisionCount: 1)),
            now);

        DecisionSessionTransferEligibilitySnapshot snapshot = await service.CheckAsync(harness.Repository.Id);

        Assert.Equal(DecisionSessionTransferEligibilityStatus.Blocked, snapshot.Eligibility.Status);
        Assert.Contains(snapshot.Eligibility.Findings, finding => finding.Code == "duplicate-active-sessions");
    }

    [Fact]
    public async Task TransferPolicyRemainsTransferWhenEligibilityIsBlocked()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreatePolicy(harness.Repository.Id, active, DecisionSessionLifecycleDecision.Transfer, now),
            CreateEvidenceReader(CreateEvidence(harness.Repository.Id, now, operationalContextRevisionCount: 0)),
            now);

        DecisionSessionTransferEligibilitySnapshot snapshot = await service.CheckAsync(harness.Repository.Id);

        Assert.Equal(DecisionSessionTransferEligibilityStatus.Blocked, snapshot.Eligibility.Status);
        Assert.Equal(DecisionSessionLifecycleDecision.Transfer, snapshot.Eligibility.PolicyEvaluation.Decision);
        Assert.Contains(snapshot.Eligibility.Findings, finding => finding.Code == "operational-context-unavailable");
        DecisionSession? stillActive = await harness.RepositoryStore.GetActiveAsync(harness.Repository);
        Assert.Equal(active.Id, stillActive?.Id);
    }

    [Fact]
    public async Task EligibilityIsBlockedWhenContinuityArtifactPreflightFails()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreatePolicy(harness.Repository.Id, active, DecisionSessionLifecycleDecision.Transfer, now),
            CreateEvidenceReader(CreateEvidence(harness.Repository.Id, now, operationalContextRevisionCount: 1, evidenceItemCount: 0)),
            now);

        DecisionSessionTransferEligibilitySnapshot snapshot = await service.CheckAsync(harness.Repository.Id);

        Assert.Equal(DecisionSessionTransferEligibilityStatus.Blocked, snapshot.Eligibility.Status);
        Assert.Contains(snapshot.Eligibility.Findings, finding => finding.Code == "continuity-artifact-preflight-failed");
    }

    [Fact]
    public async Task EligibilityIsDeferredWhenSourceSessionIsTransferPending()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSession transferPending = DecisionSession.Create(harness.Repository.Id, "test", now)
            with { State = DecisionSessionState.TransferPending, ActivatedAt = now };
        await harness.WriteRegistryAsync([transferPending]);
        var service = CreateService(
            harness,
            CreatePolicy(harness.Repository.Id, transferPending, DecisionSessionLifecycleDecision.Transfer, now),
            CreateEvidenceReader(CreateEvidence(harness.Repository.Id, now, operationalContextRevisionCount: 1)),
            now);

        DecisionSessionTransferEligibilitySnapshot snapshot = await service.CheckAsync(harness.Repository.Id);

        Assert.Equal(DecisionSessionTransferEligibilityStatus.Deferred, snapshot.Eligibility.Status);
        Assert.Equal(transferPending.Id, snapshot.Eligibility.SourceSessionId);
        Assert.Contains(snapshot.Eligibility.Findings, finding => finding.Code == "transfer-pending");
    }

    [Fact]
    public async Task EligibilityIsDeferredWhenRepositoryEvidenceIsTemporarilyUnavailable()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreatePolicy(harness.Repository.Id, active, DecisionSessionLifecycleDecision.Transfer, now),
            new ThrowingEvidenceReader(new IOException("locked")),
            now);

        DecisionSessionTransferEligibilitySnapshot snapshot = await service.CheckAsync(harness.Repository.Id);

        Assert.Equal(DecisionSessionTransferEligibilityStatus.Deferred, snapshot.Eligibility.Status);
        Assert.Contains(snapshot.Eligibility.Findings, finding => finding.Code == "repository-unavailable");
    }

    // Phase 3 retarget (refactor-lazy-sqlite.md): transfer eligibility is computed fresh on every read and is
    // NEVER persisted as a file (no cached row at all — it is entirely a function of current registry/policy/
    // evidence state). The preserved invariant is the substantive one: a transfer policy with complete evidence
    // resolves to Eligible with the "eligible" finding. The removed assertion was that a snapshot FILE was
    // written, which no longer happens (and is what makes the byte-identical endpoint listing stay empty).
    [Fact]
    public async Task EligibleTransferPolicyComputesEligibleSnapshotWithoutPersistingAFile()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        DecisionSession active = await CreateActiveSessionAsync(harness);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var service = CreateService(
            harness,
            CreatePolicy(harness.Repository.Id, active, DecisionSessionLifecycleDecision.Transfer, now),
            CreateEvidenceReader(CreateEvidence(harness.Repository.Id, now, operationalContextRevisionCount: 2)),
            now);

        DecisionSessionTransferEligibilitySnapshot snapshot = await service.CheckAsync(harness.Repository.Id);
        string? persisted = await harness.Store.ReadAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.TransferEligibilitySnapshotJson()));

        Assert.Equal(DecisionSessionTransferEligibilityStatus.Eligible, snapshot.Eligibility.Status);
        Assert.Null(persisted);
        Assert.Contains(snapshot.Eligibility.Findings, finding => finding.Code == "eligible");
    }

    private static async Task<DecisionSession> CreateActiveSessionAsync(DecisionSessionTestHarness harness)
    {
        DecisionSession created = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        return await harness.Registry.ActivateSessionAsync(harness.Repository.Id, created.Id);
    }

    private static DecisionSessionTransferEligibilityService CreateService(
        DecisionSessionTestHarness harness,
        IDecisionSessionLifecyclePolicy policy,
        IDecisionSessionEvidenceReader evidenceReader,
        DateTimeOffset now)
    {
        return new DecisionSessionTransferEligibilityService(
            harness.RepositoryService,
            harness.RepositoryStore,
            harness.Recovery,
            policy,
            evidenceReader,
            new FixedTimeProvider(now));
    }

    private static FixedPolicy CreatePolicy(
        Guid repositoryId,
        DecisionSession? activeSession,
        DecisionSessionLifecycleDecision decision,
        DateTimeOffset generatedAt)
    {
        activeSession ??= DecisionSession.Create(repositoryId, "test", generatedAt);
        var metrics = new DecisionSessionMetrics(
            100,
            400,
            1,
            1,
            1,
            1,
            0,
            0,
            1,
            generatedAt,
            generatedAt);
        var statistics = new DecisionSessionStatistics(TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.Zero, 100m, 1m);
        var cache = new DecisionSessionCacheMetrics(TimeSpan.FromHours(1), 0.1m, generatedAt.AddHours(1));
        var economics = new DecisionSessionEconomics(0.2m, 0.8m, 0.2m, 0.2m, 0.5m, 0.2m, 0.1m);
        var coherence = new DecisionSessionCoherence(0.3m, 0.8m, 0.5m, 0.5m, 0.8m);
        var evaluation = new DecisionSessionLifecycleEvaluation(
            decision,
            decision == DecisionSessionLifecycleDecision.Continue ? 0.8m : 0.2m,
            decision == DecisionSessionLifecycleDecision.Transfer ? 0.8m : 0.2m,
            $"Policy decided {decision}.",
            [],
            generatedAt);
        var diagnostics = new DecisionSessionLifecycleDiagnostics(
            repositoryId,
            generatedAt,
            new DecisionSessionLifecycleInputs(activeSession, metrics, statistics, cache, economics, coherence),
            new ReuseScoreAssessment(evaluation.ReuseScore, 0m, 0m, 0m, 0m),
            new TransferScoreAssessment(evaluation.TransferScore, 0m, 0m, 0m, 0m, 0m),
            [],
            []);
        return new FixedPolicy(new DecisionSessionLifecycleSnapshot(repositoryId, evaluation, diagnostics, generatedAt));
    }

    private static FixedEvidenceReader CreateEvidenceReader(DecisionSessionEvidence evidence)
    {
        return new FixedEvidenceReader(evidence);
    }

    private static DecisionSessionEvidence CreateEvidence(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        long operationalContextRevisionCount,
        long? evidenceItemCount = null)
    {
        return new DecisionSessionEvidence(
            repositoryId,
            generatedAt.AddHours(-1),
            generatedAt,
            evidenceItemCount ?? 3 + operationalContextRevisionCount,
            1,
            0,
            0,
            1,
            1,
            1,
            operationalContextRevisionCount,
            [],
            []);
    }

    private sealed class FixedPolicy(DecisionSessionLifecycleSnapshot snapshot) : IDecisionSessionLifecyclePolicy
    {
        public Task<DecisionSessionLifecycleSnapshot> EvaluateAsync(Guid repositoryId)
        {
            return Task.FromResult(snapshot);
        }
    }

    private sealed class FixedEvidenceReader(DecisionSessionEvidence evidence) : IDecisionSessionEvidenceReader
    {
        public Task<DecisionSessionEvidence> ReadAsync(Repository repository, DecisionSession? activeSession, DateTimeOffset measuredAt)
        {
            return Task.FromResult(evidence);
        }
    }

    private sealed class ThrowingEvidenceReader(Exception exception) : IDecisionSessionEvidenceReader
    {
        public Task<DecisionSessionEvidence> ReadAsync(Repository repository, DecisionSession? activeSession, DateTimeOffset measuredAt)
        {
            throw exception;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
