using System.Text;
using System.Text.Json;
using CommandCenter.Continuity.Abstractions;
using CommandCenter.Continuity.Models;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.DecisionSessions.Abstractions;
using CommandCenter.DecisionSessions.Models;
using CommandCenter.DecisionSessions.Persistence;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;

namespace CommandCenter.DecisionSessions.Services;

public sealed class DecisionSessionMetricsService(
    IRepositoryService repositoryService,
    IDecisionSessionRegistry sessionRegistry,
    IDecisionRepository decisionRepository,
    IReasoningRepository reasoningRepository,
    IOperationalContextProposalStore operationalContextProposalStore,
    ITokenEstimator tokenEstimator) : IDecisionSessionMetricsService
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(1);

    public async Task<DecisionSessionMetricsSnapshot> GetMetricsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionSession? activeSession = await sessionRegistry.GetActiveSessionAsync(repositoryId);
        DateTimeOffset measuredAt = DateTimeOffset.UtcNow;

        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
        IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);
        IReadOnlyList<ReasoningEvent> reasoningEvents = await reasoningRepository.ListEventsAsync(repository);
        IReadOnlyList<ReasoningThread> reasoningThreads = await reasoningRepository.ListThreadsAsync(repository);
        IReadOnlyList<ReasoningRelationship> reasoningRelationships = await reasoningRepository.ListRelationshipsAsync(repository);
        IReadOnlyList<OperationalContextProposal> operationalContexts = await operationalContextProposalStore.ListAsync(repository, includeContent: true);

        EvidenceSize decisionSize = SizeOf("decisions", decisions);
        EvidenceSize candidateSize = SizeOf("decision-candidates", candidates);
        EvidenceSize proposalSize = SizeOf("decision-proposals", proposals);
        EvidenceSize eventSize = SizeOf("reasoning-events", reasoningEvents);
        EvidenceSize threadSize = SizeOf("reasoning-threads", reasoningThreads);
        EvidenceSize relationshipSize = SizeOf("reasoning-relationships", reasoningRelationships);
        EvidenceSize contextSize = SizeOf("operational-context-proposals", operationalContexts);
        EvidenceSize[] sizes = [decisionSize, candidateSize, proposalSize, eventSize, threadSize, relationshipSize, contextSize];

        long totalBytes = sizes.Sum(size => size.ByteCount);
        long totalCharacters = sizes.Sum(size => size.CharacterCount);
        long estimatedTokens = tokenEstimator.EstimateTokenCount(string.Concat(sizes.Select(size => size.SerializedContent)));
        DateTimeOffset sessionStartedAt = activeSession?.ActivatedAt ?? activeSession?.CreatedAt ?? measuredAt;
        DateTimeOffset lastActivityAt = LastActivityAt(activeSession, decisions, candidates, proposals, reasoningEvents, reasoningThreads, reasoningRelationships, operationalContexts, sessionStartedAt);
        TimeSpan sessionAge = activeSession is null ? TimeSpan.Zero : NonNegative(measuredAt - activeSession.CreatedAt);
        TimeSpan elapsed = NonNegative(measuredAt - sessionStartedAt);
        TimeSpan idle = NonNegative(measuredAt - lastActivityAt);
        long evidenceItemCount = decisions.Count + candidates.Count + proposals.Count + reasoningEvents.Count + reasoningThreads.Count + reasoningRelationships.Count + operationalContexts.Count;
        decimal elapsedHours = Math.Max(1m, (decimal)elapsed.TotalHours);
        decimal activityRate = evidenceItemCount / elapsedHours;
        decimal growthRate = totalBytes / elapsedHours;
        decimal cacheMissRisk = CalculateCacheMissRisk(elapsed, idle);
        DateTimeOffset? cacheExpiresAt = lastActivityAt + DefaultCacheTtl;

        var metrics = new DecisionSessionMetrics(
            estimatedTokens,
            totalBytes,
            reasoningEvents.Count,
            reasoningThreads.Count,
            reasoningRelationships.Count,
            decisions.Count,
            candidates.Count,
            proposals.Count,
            operationalContexts.Count,
            lastActivityAt,
            measuredAt);
        var statistics = new DecisionSessionStatistics(sessionAge, elapsed, idle, growthRate, activityRate);
        var activity = new DecisionSessionActivity(evidenceItemCount, lastActivityAt, idle, activityRate);
        var growth = new DecisionSessionGrowth(totalBytes, estimatedTokens, elapsed, growthRate);
        var cache = new DecisionSessionCacheMetrics(DefaultCacheTtl, cacheMissRisk, cacheExpiresAt);
        var diagnostics = new DecisionSessionMetricsDiagnostics(
            repositoryId,
            measuredAt,
            sizes.Select(size => new DecisionSessionMetricsSourceDiagnostic(
                size.Source,
                size.ItemCount,
                size.ByteCount,
                size.CharacterCount,
                size.Notes)).ToArray(),
            [
                "Token count is estimated deterministically as (characterCount + 3) / 4.",
                $"Cache TTL is currently estimated with a fixed {DefaultCacheTtl.TotalMinutes:0}-minute assumption.",
                "Cache miss risk combines elapsed session duration and idle duration against the TTL assumption."
            ],
            activeSession is null ? ["No active decision session exists; metrics are repository evidence only."] : []);

        return new DecisionSessionMetricsSnapshot(repositoryId, metrics, statistics, activity, growth, cache, diagnostics, measuredAt);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static EvidenceSize SizeOf<T>(string source, IReadOnlyList<T> items)
    {
        string serialized = JsonSerializer.Serialize(items, DecisionSessionJson.Options);
        return new EvidenceSize(
            source,
            items.Count,
            Encoding.UTF8.GetByteCount(serialized),
            serialized.Length,
            serialized,
            []);
    }

    private static DateTimeOffset LastActivityAt(
        DecisionSession? activeSession,
        IReadOnlyList<Decision> decisions,
        IReadOnlyList<DecisionCandidate> candidates,
        IReadOnlyList<DecisionProposal> proposals,
        IReadOnlyList<ReasoningEvent> reasoningEvents,
        IReadOnlyList<ReasoningThread> reasoningThreads,
        IReadOnlyList<ReasoningRelationship> reasoningRelationships,
        IReadOnlyList<OperationalContextProposal> operationalContexts,
        DateTimeOffset fallback)
    {
        var timestamps = new List<DateTimeOffset>();
        timestamps.AddRange(decisions.Select(decision => decision.Metadata.UpdatedAt));
        timestamps.AddRange(candidates.SelectMany(candidate => candidate.History.Select(history => history.Timestamp)));
        timestamps.AddRange(proposals.SelectMany(proposal => proposal.History.Select(history => history.Timestamp)));
        timestamps.AddRange(reasoningEvents.Select(reasoningEvent => reasoningEvent.CreatedAt));
        timestamps.AddRange(reasoningThreads.Select(thread => thread.UpdatedAt));
        timestamps.AddRange(reasoningRelationships.Select(relationship => relationship.CreatedAt));
        timestamps.AddRange(operationalContexts.Select(context => context.GeneratedAt));
        if (activeSession?.Metadata.UpdatedAt is not null)
        {
            timestamps.Add(activeSession.Metadata.UpdatedAt.Value);
        }

        return timestamps.DefaultIfEmpty(fallback).Max();
    }

    private static TimeSpan NonNegative(TimeSpan duration)
    {
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    private static decimal CalculateCacheMissRisk(TimeSpan elapsed, TimeSpan idle)
    {
        decimal elapsedPressure = Clamp((decimal)(elapsed.TotalMinutes / DefaultCacheTtl.TotalMinutes));
        decimal idlePressure = Clamp((decimal)(idle.TotalMinutes / DefaultCacheTtl.TotalMinutes));
        return Clamp(decimal.Round((elapsedPressure * 0.4m) + (idlePressure * 0.6m), 4, MidpointRounding.AwayFromZero));
    }

    private static decimal Clamp(decimal value)
    {
        if (value < 0m)
        {
            return 0m;
        }

        return value > 1m ? 1m : value;
    }

    private sealed record EvidenceSize(
        string Source,
        long ItemCount,
        long ByteCount,
        long CharacterCount,
        string SerializedContent,
        IReadOnlyList<string> Notes);
}
