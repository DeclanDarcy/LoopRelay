using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Continuity.Services;
using CommandCenter.Core.Artifacts;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.DecisionSessions.Services;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.DecisionSessions.Tests;

public sealed class DecisionSessionMetricsTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1)]
    [InlineData("abcd", 1)]
    [InlineData("abcde", 2)]
    public void TokenEstimatorIsDeterministic(string text, long expectedTokens)
    {
        var estimator = new DeterministicTokenEstimator();

        Assert.Equal(expectedTokens, estimator.EstimateTokenCount(text));
        Assert.Equal(expectedTokens, estimator.EstimateTokenCount(text));
    }

    [Fact]
    public async Task MetricsAreGeneratedFromDecisionReasoningAndOperationalContextEvidence()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(harness.Store, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(harness.Store);
        DateTimeOffset measuredAt = DateTimeOffset.UtcNow;
        var service = CreateService(harness, decisionRepository, reasoningRepository, contextStore, measuredAt);
        DecisionSession session = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, session.Id);
        DateTimeOffset now = measuredAt;

        await decisionRepository.SaveDecisionAsync(harness.Repository, CreateDecision(harness.Repository.Id, now.AddMinutes(-10)));
        await decisionRepository.SaveCandidateAsync(harness.Repository, CreateCandidate(harness.Repository.Id, now.AddMinutes(-9)));
        await decisionRepository.SaveProposalAsync(harness.Repository, CreateProposal(harness.Repository.Id, now.AddMinutes(-8)));
        ReasoningEvent reasoningEvent = await reasoningRepository.CreateEventAsync(harness.Repository, EventCommand("Cache pressure observed"));
        ReasoningThread reasoningThread = await reasoningRepository.CreateThreadAsync(harness.Repository, new CreateReasoningThreadCommand(
            "Session lifecycle thread",
            ReasoningThreadTheme.DecisionEvolution,
            "Tracks decision session lifecycle evidence.",
            [reasoningEvent.Id],
            ["decision-session"]));
        await reasoningRepository.CreateRelationshipAsync(harness.Repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.Supports,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningThread, reasoningThread.Id),
            new ReasoningNarrative("The event supports the lifecycle thread."),
            new ReasoningProvenance("test", "test")));
        await contextStore.SaveAsync(
            harness.Repository,
            new OperationalContextProposal
            {
                ProposalId = "ctx-0001",
                RepositoryId = harness.Repository.Id,
                GeneratedAt = now.AddMinutes(-7),
                Status = OperationalContextProposalStatus.Pending,
                BaselineCurrentContextHash = "baseline",
                GeneratedContentHash = "generated"
            },
            "Operational context revision content.");
        await harness.Store.WriteAsync(Path.Combine(harness.Repository.Path, ".agents", "operational_context.md"), "Current operational context.");
        await harness.Store.WriteAsync(Path.Combine(harness.Repository.Path, ".agents", "operational_context.0001.md"), "Historical operational context.");

        DecisionSessionMetricsSnapshot snapshot = await service.GetMetricsAsync(harness.Repository.Id);

        Assert.Equal(1, snapshot.Metrics.DecisionCount);
        Assert.Equal(1, snapshot.Metrics.DecisionCandidateCount);
        Assert.Equal(1, snapshot.Metrics.DecisionProposalCount);
        Assert.Equal(1, snapshot.Metrics.ReasoningEventCount);
        Assert.Equal(1, snapshot.Metrics.ReasoningThreadCount);
        Assert.Equal(1, snapshot.Metrics.ReasoningRelationshipCount);
        Assert.Equal(3, snapshot.Metrics.OperationalContextRevisionCount);
        Assert.True(snapshot.Metrics.EstimatedTokenCount > 0);
        Assert.True(snapshot.Metrics.ContextByteSize > 0);
        Assert.True(snapshot.Statistics.ActivityRate > 0m);
        Assert.True(snapshot.Cache.EstimatedCacheMissRisk >= 0m);
        Assert.Contains(snapshot.Diagnostics.Sources, source => source.Source == "operational-context-proposals" && source.ItemCount == 1);
        Assert.Contains(snapshot.Diagnostics.Sources, source => source.Source == "operational-context-artifacts" && source.ItemCount == 2);
        Assert.Contains(snapshot.Diagnostics.Assumptions, assumption => assumption.Contains("Token count", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SameInputsProduceSameMetricsAndStatisticsWhenMeasuredAtIsFixed()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(harness.Store, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(harness.Store);
        DateTimeOffset measuredAt = DateTimeOffset.UtcNow;
        var service = CreateService(harness, decisionRepository, reasoningRepository, contextStore, measuredAt);
        DecisionSession session = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, session.Id);
        await decisionRepository.SaveDecisionAsync(harness.Repository, CreateDecision(harness.Repository.Id, measuredAt.AddMinutes(-10)));

        DecisionSessionMetricsSnapshot first = await service.GetMetricsAsync(harness.Repository.Id);
        DecisionSessionMetricsSnapshot second = await service.GetMetricsAsync(harness.Repository.Id);

        Assert.Equal(first.Metrics, second.Metrics);
        Assert.Equal(first.Statistics, second.Statistics);
        Assert.Equal(first.Activity, second.Activity);
        Assert.Equal(first.Growth, second.Growth);
        Assert.Equal(first.Cache, second.Cache);
    }

    [Fact]
    public async Task TtlAndCacheMissRiskIncreaseWithElapsedAndIdleDuration()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(harness.Store, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(harness.Store);
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        DecisionSession session = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, session.Id);
        await decisionRepository.SaveDecisionAsync(harness.Repository, CreateDecision(harness.Repository.Id, startedAt.AddMinutes(5)));

        DecisionSessionMetricsSnapshot early = await CreateService(
            harness,
            decisionRepository,
            reasoningRepository,
            contextStore,
            startedAt.AddMinutes(10)).GetMetricsAsync(harness.Repository.Id);
        DecisionSessionMetricsSnapshot later = await CreateService(
            harness,
            decisionRepository,
            reasoningRepository,
            contextStore,
            startedAt.AddMinutes(70)).GetMetricsAsync(harness.Repository.Id);

        Assert.True(later.Statistics.SessionElapsedDuration > early.Statistics.SessionElapsedDuration);
        Assert.True(later.Statistics.IdleDuration > early.Statistics.IdleDuration);
        Assert.True(later.Cache.EstimatedCacheMissRisk > early.Cache.EstimatedCacheMissRisk);
    }

    [Fact]
    public async Task ActivityAndGrowthIncreaseWhenEvidenceIncreases()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(harness.Store, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(harness.Store);
        DateTimeOffset measuredAt = DateTimeOffset.UtcNow;
        DecisionSession session = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, session.Id);
        var service = CreateService(harness, decisionRepository, reasoningRepository, contextStore, measuredAt);

        DecisionSessionMetricsSnapshot empty = await service.GetMetricsAsync(harness.Repository.Id);
        await decisionRepository.SaveDecisionAsync(harness.Repository, CreateDecision(harness.Repository.Id, measuredAt.AddMinutes(-5)));
        await harness.Store.WriteAsync(Path.Combine(harness.Repository.Path, ".agents", "operational_context.md"), new string('c', 512));
        DecisionSessionMetricsSnapshot populated = await service.GetMetricsAsync(harness.Repository.Id);

        Assert.True(populated.Activity.EvidenceItemCount > empty.Activity.EvidenceItemCount);
        Assert.True(populated.Statistics.ActivityRate > empty.Statistics.ActivityRate);
        Assert.True(populated.Growth.EvidenceByteSize > empty.Growth.EvidenceByteSize);
        Assert.True(populated.Statistics.GrowthRate > empty.Statistics.GrowthRate);
    }

    // Phase 3 retarget (refactor-lazy-sqlite.md): metrics is no longer persisted as a derived FILE, so there is
    // no "invalid persisted snapshot" to validate-and-rebuild. The preserved invariant is that the served
    // metrics snapshot is always freshly COMPUTED from authoritative source evidence — a leftover corrupt
    // analysis file cannot corrupt the result and is simply irrelevant to it.
    [Fact]
    public async Task MetricsIsAlwaysComputedFromAuthoritativeEvidenceRegardlessOfStaleAnalysisFile()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(harness.Store, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(harness.Store);
        DateTimeOffset measuredAt = DateTimeOffset.UtcNow;
        var service = CreateService(harness, decisionRepository, reasoningRepository, contextStore, measuredAt);
        DecisionSession session = await harness.Registry.CreateSessionAsync(harness.Repository.Id, "test");
        await harness.Registry.ActivateSessionAsync(harness.Repository.Id, session.Id);
        await decisionRepository.SaveDecisionAsync(harness.Repository, CreateDecision(harness.Repository.Id, measuredAt.AddMinutes(-5)));
        await harness.Store.WriteAsync(
            DecisionSessionArtifactPaths.Resolve(harness.Repository, DecisionSessionArtifactPaths.MetricsSnapshotJson()),
            "{ not valid json");

        DecisionSessionMetricsSnapshot snapshot = await service.GetMetricsAsync(harness.Repository.Id);

        Assert.Equal(1, snapshot.Metrics.DecisionCount);
        Assert.Equal(harness.Repository.Id, snapshot.RepositoryId);
    }

    [Fact]
    public async Task DiagnosticsExplainSourcesAssumptionsCacheRiskAndMissingEvidence()
    {
        DecisionSessionTestHarness harness = DecisionSessionTestHarness.Create();
        var decisionRepository = new InMemoryDecisionRepository();
        var reasoningRepository = new FileSystemReasoningRepository(harness.Store, new ReasoningArtifactProjectionService());
        var contextStore = new FileSystemOperationalContextProposalStore(harness.Store);
        var service = CreateService(harness, decisionRepository, reasoningRepository, contextStore, DateTimeOffset.UtcNow);

        DecisionSessionMetricsSnapshot snapshot = await service.GetMetricsAsync(harness.Repository.Id);

        Assert.Contains(snapshot.Diagnostics.Sources, source => source.Source == "decisions" && source.ByteCount > 0 && source.CharacterCount > 0);
        Assert.Contains(snapshot.Diagnostics.Assumptions, assumption => assumption.Contains("Cache TTL assumption", StringComparison.Ordinal));
        Assert.Contains(snapshot.Diagnostics.Assumptions, assumption => assumption.Contains("Cache risk uses elapsed contribution", StringComparison.Ordinal));
        Assert.Contains(snapshot.Diagnostics.Assumptions, assumption => assumption.Contains("Confidence", StringComparison.Ordinal));
        Assert.Contains(snapshot.Diagnostics.Warnings, warning => warning.Contains("Missing evidence source", StringComparison.Ordinal));
    }

    private static Decision CreateDecision(Guid repositoryId, DateTimeOffset timestamp)
    {
        return new Decision(
            new DecisionId("DEC-0001"),
            DecisionState.Open,
            DecisionClassification.Architectural,
            "Use decision sessions",
            "Decision sessions carry governance continuity.",
            new DecisionMetadata(repositoryId, timestamp, timestamp),
            null,
            [],
            [],
            [History(timestamp, "Open")]);
    }

    private static DecisionCandidate CreateCandidate(Guid repositoryId, DateTimeOffset timestamp)
    {
        return new DecisionCandidate(
            "CAND-0001",
            repositoryId,
            DecisionCandidateState.Discovered,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            "Measure session evidence",
            "Metrics should read authoritative evidence.",
            "source",
            [],
            [],
            [],
            [],
            [History(timestamp, DecisionCandidateState.Discovered.ToString())]);
    }

    private static DecisionProposal CreateProposal(Guid repositoryId, DateTimeOffset timestamp)
    {
        return new DecisionProposal(
            "PROP-0001",
            repositoryId,
            "CAND-0001",
            DecisionProposalState.Generated,
            "Add metrics",
            "Metrics are analysis-only.",
            [],
            [],
            null,
            [],
            [],
            [History(timestamp, DecisionProposalState.Generated.ToString())]);
    }

    private static DecisionHistoryEntry History(DateTimeOffset timestamp, string toState)
    {
        return new DecisionHistoryEntry(timestamp, "Created", null, toState, null, []);
    }

    private static CreateReasoningEventCommand EventCommand(string title)
    {
        return new CreateReasoningEventCommand(
            ReasoningEventFamily.DecisionEvolution,
            ReasoningEventType.DecisionReframed,
            title,
            new ReasoningNarrative("The session evidence changed."),
            [],
            new ReasoningProvenance("test", "test"),
            [],
            ["decision-session"]);
    }

    private static DecisionSessionMetricsService CreateService(
        DecisionSessionTestHarness harness,
        InMemoryDecisionRepository decisionRepository,
        FileSystemReasoningRepository reasoningRepository,
        FileSystemOperationalContextProposalStore contextStore,
        DateTimeOffset measuredAt)
    {
        var artifactService = new ArtifactService(harness.Store);
        var evidenceReader = new DecisionSessionEvidenceReader(decisionRepository, reasoningRepository, contextStore, artifactService);
        return new DecisionSessionMetricsService(
            harness.RepositoryService,
            harness.Registry,
            harness.RepositoryStore,
            evidenceReader,
            new DeterministicTokenEstimator(),
            new FixedTimeProvider(measuredAt));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
