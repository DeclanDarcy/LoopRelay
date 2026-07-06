using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Reasoning.Abstractions;
using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Services;

public sealed class ReasoningGraphService(
    IRepositoryService repositoryService,
    IReasoningRepository reasoningRepository,
    IArtifactStore artifactStore)
    : IReasoningGraphService
{
    public async Task<ReasoningGraph> GetGraphAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await BuildGraphAsync(repository);
    }

    public async Task<ReasoningTrace> TraceBackwardAsync(Guid repositoryId, ReasoningReference target)
    {
        return await TraceAsync(repositoryId, target, ReasoningTraceDirection.Backward);
    }

    public async Task<ReasoningTrace> TraceForwardAsync(Guid repositoryId, ReasoningReference target)
    {
        return await TraceAsync(repositoryId, target, ReasoningTraceDirection.Forward);
    }

    private async Task<ReasoningTrace> TraceAsync(Guid repositoryId, ReasoningReference target, ReasoningTraceDirection direction)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        ReasoningGraph graph = await BuildGraphAsync(repository);
        string targetNodeId = NodeId(target.Kind, target.Id);
        if (!graph.Nodes.Any(node => string.Equals(node.Id, targetNodeId, StringComparison.Ordinal)))
        {
            throw new KeyNotFoundException($"Reasoning graph node was not found: {target.Kind}:{target.Id}");
        }

        var visitedNodes = new HashSet<string>(StringComparer.Ordinal) { targetNodeId };
        var visitedRelationships = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(targetNodeId);

        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            IEnumerable<ReasoningGraphRelationship> nextRelationships = direction == ReasoningTraceDirection.Backward
                ? graph.Relationships.Where(relationship => string.Equals(relationship.TargetNodeId, current, StringComparison.Ordinal))
                : graph.Relationships.Where(relationship => string.Equals(relationship.SourceNodeId, current, StringComparison.Ordinal));

            foreach (ReasoningGraphRelationship relationship in nextRelationships)
            {
                if (!visitedRelationships.Add(relationship.Id))
                {
                    continue;
                }

                string nextNode = direction == ReasoningTraceDirection.Backward
                    ? relationship.SourceNodeId
                    : relationship.TargetNodeId;
                if (visitedNodes.Add(nextNode))
                {
                    queue.Enqueue(nextNode);
                }
            }
        }

        return new ReasoningTrace(
            repositoryId,
            direction,
            target,
            graph.Nodes.Where(node => visitedNodes.Contains(node.Id)).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(),
            graph.Relationships.Where(relationship => visitedRelationships.Contains(relationship.Id)).OrderBy(relationship => relationship.Id, StringComparer.Ordinal).ToArray(),
            graph.Diagnostics,
            BuildGraphDiagnosticGroups(graph.Diagnostics, "Trace validation"));
    }

    private async Task<ReasoningGraph> BuildGraphAsync(Repository repository)
    {
        IReadOnlyList<ReasoningEvent> events = await reasoningRepository.ListEventsAsync(repository);
        IReadOnlyList<ReasoningThread> threads = await reasoningRepository.ListThreadsAsync(repository);
        IReadOnlyList<ReasoningRelationship> relationships = await reasoningRepository.ListRelationshipsAsync(repository);
        var nodes = new Dictionary<string, ReasoningGraphNode>(StringComparer.Ordinal);
        var graphRelationships = new Dictionary<string, ReasoningGraphRelationship>(StringComparer.Ordinal);
        var diagnostics = new List<string>();

        foreach (ReasoningEvent reasoningEvent in events)
        {
            AddNode(nodes, new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id), reasoningEvent.Title, true);
        }

        foreach (ReasoningThread thread in threads)
        {
            AddNode(nodes, new ReasoningReference(ReasoningReferenceKind.ReasoningThread, thread.Id), thread.Title, true);
        }

        var eventIds = events.Select(reasoningEvent => reasoningEvent.Id).ToHashSet(StringComparer.Ordinal);
        var threadIds = threads.Select(thread => thread.Id).ToHashSet(StringComparer.Ordinal);

        foreach (ReasoningEvent reasoningEvent in events)
        {
            string eventNodeId = NodeId(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id);
            foreach (ReasoningReference reference in reasoningEvent.References)
            {
                bool resolved = await IsResolvedAsync(repository, reference, eventIds, threadIds);
                AddNode(nodes, reference, LabelFor(reference), resolved);
                AddRelationship(
                    graphRelationships,
                    $"event-reference:{reference.Kind}:{reference.Id}:{reasoningEvent.Id}",
                    ReasoningRelationshipType.DerivesFrom,
                    NodeId(reference.Kind, reference.Id),
                    eventNodeId,
                    "Event reference",
                    "ReasoningEvent.References",
                    null);
                AddMissingReferenceDiagnostic(diagnostics, resolved, reference, reasoningEvent.Id);
            }

            if (!string.IsNullOrWhiteSpace(reasoningEvent.Provenance.RelativePath))
            {
                var provenanceReference = new ReasoningReference(
                    ReasoningReferenceKind.Artifact,
                    reasoningEvent.Provenance.RelativePath,
                    reasoningEvent.Provenance.RelativePath,
                    reasoningEvent.Provenance.Section,
                    reasoningEvent.Provenance.Excerpt,
                    reasoningEvent.Provenance.Fingerprint);
                bool resolved = await IsResolvedAsync(repository, provenanceReference, eventIds, threadIds);
                AddNode(nodes, provenanceReference, reasoningEvent.Provenance.RelativePath, resolved);
                AddRelationship(
                    graphRelationships,
                    $"event-provenance:{provenanceReference.Id}:{reasoningEvent.Id}",
                    ReasoningRelationshipType.DerivesFrom,
                    NodeId(provenanceReference.Kind, provenanceReference.Id),
                    eventNodeId,
                    "Event provenance",
                    reasoningEvent.Provenance.SourceKind,
                    null);
                AddMissingReferenceDiagnostic(diagnostics, resolved, provenanceReference, reasoningEvent.Id);
            }

            foreach (string threadId in reasoningEvent.ThreadIds)
            {
                AddThreadMembership(nodes, graphRelationships, diagnostics, eventIds, threadIds, reasoningEvent.Id, threadId, "ReasoningEvent.ThreadIds");
            }
        }

        foreach (ReasoningThread thread in threads)
        {
            foreach (string eventId in thread.EventIds)
            {
                AddThreadMembership(nodes, graphRelationships, diagnostics, eventIds, threadIds, eventId, thread.Id, "ReasoningThread.EventIds");
                if (!eventIds.Contains(eventId))
                {
                    diagnostics.Add($"Thread {thread.Id} references missing reasoning event {eventId}.");
                }
            }
        }

        foreach (ReasoningRelationship relationship in relationships)
        {
            bool sourceResolved = await IsResolvedAsync(repository, relationship.Source, eventIds, threadIds);
            bool targetResolved = await IsResolvedAsync(repository, relationship.Target, eventIds, threadIds);
            AddNode(nodes, relationship.Source, LabelFor(relationship.Source), sourceResolved);
            AddNode(nodes, relationship.Target, LabelFor(relationship.Target), targetResolved);
            AddRelationship(
                graphRelationships,
                $"persisted:{relationship.Id}",
                relationship.Type,
                NodeId(relationship.Source.Kind, relationship.Source.Id),
                NodeId(relationship.Target.Kind, relationship.Target.Id),
                relationship.Narrative.Summary,
                relationship.Provenance.SourceKind,
                relationship.Id);
            AddMissingRelationshipDiagnostic(diagnostics, sourceResolved, relationship.Source, relationship.Id, "source");
            AddMissingRelationshipDiagnostic(diagnostics, targetResolved, relationship.Target, relationship.Id, "target");
        }

        return new ReasoningGraph(
            repository.Id,
            DateTimeOffset.UtcNow,
            nodes.Values.OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(),
            graphRelationships.Values.OrderBy(relationship => relationship.Id, StringComparer.Ordinal).ToArray(),
            diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            BuildGraphDiagnosticGroups(diagnostics, "Graph validation"));
    }

    private static IReadOnlyList<ReasoningDiagnosticGroup> BuildGraphDiagnosticGroups(
        IReadOnlyList<string> diagnostics,
        string title)
    {
        string[] validationDiagnostics = diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (validationDiagnostics.Length == 0)
        {
            return [];
        }

        return
        [
            new ReasoningDiagnosticGroup(
                "validation",
                title,
                validationDiagnostics)
        ];
    }

    private static void AddThreadMembership(
        IDictionary<string, ReasoningGraphNode> nodes,
        IDictionary<string, ReasoningGraphRelationship> relationships,
        ICollection<string> diagnostics,
        ISet<string> eventIds,
        ISet<string> threadIds,
        string eventId,
        string threadId,
        string provenance)
    {
        var eventReference = new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, eventId);
        var threadReference = new ReasoningReference(ReasoningReferenceKind.ReasoningThread, threadId);
        AddNode(nodes, eventReference, eventId, eventIds.Contains(eventId));
        AddNode(nodes, threadReference, threadId, threadIds.Contains(threadId));
        AddRelationship(
            relationships,
            $"thread-membership:{eventId}:{threadId}",
            ReasoningRelationshipType.BelongsTo,
            NodeId(ReasoningReferenceKind.ReasoningEvent, eventId),
            NodeId(ReasoningReferenceKind.ReasoningThread, threadId),
            "Event belongs to thread",
            provenance,
            null);
        if (!threadIds.Contains(threadId))
        {
            diagnostics.Add($"Event {eventId} references missing reasoning thread {threadId}.");
        }
    }

    private static void AddNode(
        IDictionary<string, ReasoningGraphNode> nodes,
        ReasoningReference reference,
        string label,
        bool resolved)
    {
        string nodeId = NodeId(reference.Kind, reference.Id);
        if (!nodes.TryGetValue(nodeId, out ReasoningGraphNode? existing) || (!existing.Resolved && resolved))
        {
            nodes[nodeId] = new ReasoningGraphNode(nodeId, reference.Kind, reference.Id, label, resolved, reference);
        }
    }

    private static void AddRelationship(
        IDictionary<string, ReasoningGraphRelationship> relationships,
        string id,
        ReasoningRelationshipType type,
        string sourceNodeId,
        string targetNodeId,
        string label,
        string provenance,
        string? relationshipId)
    {
        relationships.TryAdd(id, new ReasoningGraphRelationship(id, type, sourceNodeId, targetNodeId, label, provenance, relationshipId));
    }

    private async Task<bool> IsResolvedAsync(
        Repository repository,
        ReasoningReference reference,
        ISet<string> eventIds,
        ISet<string> threadIds)
    {
        return reference.Kind switch
        {
            ReasoningReferenceKind.ReasoningEvent => eventIds.Contains(reference.Id),
            ReasoningReferenceKind.ReasoningThread => threadIds.Contains(reference.Id),
            ReasoningReferenceKind.Artifact when !string.IsNullOrWhiteSpace(reference.RelativePath) =>
                await artifactStore.ExistsAsync(Path.Combine(repository.Path, reference.RelativePath)),
            _ => true
        };
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static void AddMissingReferenceDiagnostic(
        ICollection<string> diagnostics,
        bool resolved,
        ReasoningReference reference,
        string eventId)
    {
        if (!resolved)
        {
            diagnostics.Add($"Event {eventId} references unresolved {reference.Kind} {reference.Id}.");
        }
    }

    private static void AddMissingRelationshipDiagnostic(
        ICollection<string> diagnostics,
        bool resolved,
        ReasoningReference reference,
        string relationshipId,
        string endpoint)
    {
        if (!resolved)
        {
            diagnostics.Add($"Relationship {relationshipId} has unresolved {endpoint} {reference.Kind} {reference.Id}.");
        }
    }

    private static string LabelFor(ReasoningReference reference)
    {
        return string.IsNullOrWhiteSpace(reference.RelativePath) ? reference.Id : reference.RelativePath;
    }

    private static string NodeId(ReasoningReferenceKind kind, string id)
    {
        return $"{kind}:{id}";
    }
}
