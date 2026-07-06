using System.Text;
using System.Text.Json;
using LoopRelay.Continuity.Abstractions;
using LoopRelay.Continuity.Models;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.DecisionSessions.Abstractions;
using LoopRelay.DecisionSessions.Models;
using LoopRelay.DecisionSessions.Persistence;
using LoopRelay.Reasoning.Abstractions;
using LoopRelay.Reasoning.Models;

namespace LoopRelay.DecisionSessions.Services;

public sealed class DecisionSessionEvidenceReader(
    IDecisionRepository decisionRepository,
    IReasoningRepository reasoningRepository,
    IOperationalContextProposalStore operationalContextProposalStore,
    IArtifactService artifactService) : IDecisionSessionEvidenceReader
{
    public async Task<DecisionSessionEvidence> ReadAsync(Repository repository, DecisionSession? activeSession, DateTimeOffset measuredAt)
    {
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
        IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);
        IReadOnlyList<ReasoningEvent> reasoningEvents = await reasoningRepository.ListEventsAsync(repository);
        IReadOnlyList<ReasoningThread> reasoningThreads = await reasoningRepository.ListThreadsAsync(repository);
        IReadOnlyList<ReasoningRelationship> reasoningRelationships = await reasoningRepository.ListRelationshipsAsync(repository);
        IReadOnlyList<OperationalContextProposal> operationalContexts = await operationalContextProposalStore.ListAsync(repository, includeContent: true);
        IReadOnlyList<OperationalContextArtifactEvidence> operationalContextArtifacts = await ReadOperationalContextArtifactsAsync(repository);

        DecisionSessionEvidenceSource[] sources =
        [
            SizeOf("decisions", decisions, decisions.Select(decision => decision.Metadata.UpdatedAt).DefaultIfEmpty()),
            SizeOf("decision-candidates", candidates, candidates.SelectMany(candidate => candidate.History.Select(history => history.Timestamp)).DefaultIfEmpty()),
            SizeOf("decision-proposals", proposals, proposals.SelectMany(proposal => proposal.History.Select(history => history.Timestamp)).DefaultIfEmpty()),
            SizeOf("reasoning-events", reasoningEvents, reasoningEvents.Select(reasoningEvent => reasoningEvent.CreatedAt).DefaultIfEmpty()),
            SizeOf("reasoning-threads", reasoningThreads, reasoningThreads.Select(thread => thread.UpdatedAt).DefaultIfEmpty()),
            SizeOf("reasoning-relationships", reasoningRelationships, reasoningRelationships.Select(relationship => relationship.CreatedAt).DefaultIfEmpty()),
            SizeOf("operational-context-proposals", operationalContexts, operationalContexts.Select(context => context.GeneratedAt).DefaultIfEmpty()),
            SizeOf("operational-context-artifacts", operationalContextArtifacts, [])
        ];

        DateTimeOffset sessionStartedAt = activeSession?.ActivatedAt ?? activeSession?.CreatedAt ?? measuredAt;
        DateTimeOffset lastActivityAt = LastActivityAt(activeSession, sources, sessionStartedAt);
        long operationalContextRevisionCount = operationalContexts.Count + operationalContextArtifacts.Count;
        long evidenceItemCount = decisions.Count +
            candidates.Count +
            proposals.Count +
            reasoningEvents.Count +
            reasoningThreads.Count +
            reasoningRelationships.Count +
            operationalContextRevisionCount;
        List<string> warnings = [];
        if (activeSession is null)
        {
            warnings.Add("No active decision session exists; metrics are repository evidence only.");
        }

        warnings.AddRange(sources
            .Where(source => source.ItemCount == 0)
            .Select(source => $"Missing evidence source: {source.Source}."));

        return new DecisionSessionEvidence(
            repository.Id,
            sessionStartedAt,
            lastActivityAt,
            evidenceItemCount,
            decisions.Count,
            candidates.Count,
            proposals.Count,
            reasoningEvents.Count,
            reasoningThreads.Count,
            reasoningRelationships.Count,
            operationalContextRevisionCount,
            sources,
            warnings);
    }

    private async Task<IReadOnlyList<OperationalContextArtifactEvidence>> ReadOperationalContextArtifactsAsync(Repository repository)
    {
        IReadOnlyList<Artifact> artifacts = (await artifactService.DiscoverAsync(repository))
            .Where(artifact => artifact.Family == ArtifactFamily.OperationalContext)
            .OrderBy(artifact => artifact.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var evidence = new List<OperationalContextArtifactEvidence>();
        foreach (Artifact artifact in artifacts)
        {
            try
            {
                string content = await artifactService.LoadAsync(repository, artifact.RelativePath);
                evidence.Add(new OperationalContextArtifactEvidence(artifact.RelativePath, artifact.VersionKind.ToString(), content));
            }
            catch (FileNotFoundException)
            {
                evidence.Add(new OperationalContextArtifactEvidence(artifact.RelativePath, artifact.VersionKind.ToString(), string.Empty));
            }
        }

        return evidence;
    }

    private static DecisionSessionEvidenceSource SizeOf<T>(string source, IReadOnlyList<T> items, IEnumerable<DateTimeOffset> timestamps)
    {
        string serialized = JsonSerializer.Serialize(items, DecisionSessionJson.Options);
        DateTimeOffset[] timestampArray = timestamps
            .Where(timestamp => timestamp != default)
            .ToArray();
        List<string> notes = [];
        if (items.Count == 0)
        {
            notes.Add("No evidence items were found for this source.");
        }

        return new DecisionSessionEvidenceSource(
            source,
            items.Count,
            Encoding.UTF8.GetByteCount(serialized),
            serialized.Length,
            serialized,
            timestampArray.Length == 0 ? null : timestampArray.Max(),
            notes);
    }

    private static DateTimeOffset LastActivityAt(
        DecisionSession? activeSession,
        IReadOnlyList<DecisionSessionEvidenceSource> sources,
        DateTimeOffset fallback)
    {
        var timestamps = new List<DateTimeOffset>();
        timestamps.AddRange(sources.Select(source => source.LastActivityAt).OfType<DateTimeOffset>());
        if (activeSession?.Metadata.UpdatedAt is not null)
        {
            timestamps.Add(activeSession.Metadata.UpdatedAt.Value);
        }

        return timestamps.DefaultIfEmpty(fallback).Max();
    }

    private sealed record OperationalContextArtifactEvidence(string RelativePath, string VersionKind, string Content);
}
