using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Services;

public sealed class ReasoningReconstructionService(
    IRepositoryService repositoryService,
    IReasoningRepository reasoningRepository,
    IReasoningGraphService graphService)
    : IReasoningReconstructionService
{
    private const string ReportIdFormat = "yyyyMMddHHmmssfffffff";

    public async Task<ReasoningReconstruction> ReconstructAsync(Guid repositoryId, ReasoningQuery query)
    {
        ValidateQuery(query);
        Repository repository = await GetRepositoryAsync(repositoryId);
        ReasoningTrace trace = query.Direction == ReasoningTraceDirection.Forward
            ? await graphService.TraceForwardAsync(repositoryId, query.Target)
            : await graphService.TraceBackwardAsync(repositoryId, query.Target);
        IReadOnlyList<ReasoningEvent> events = await reasoningRepository.ListEventsAsync(repository);
        IReadOnlyList<ReasoningRelationship> relationships = await reasoningRepository.ListRelationshipsAsync(repository);
        IReadOnlyList<ReasoningThread> threads = await reasoningRepository.ListThreadsAsync(repository);
        EvidenceContext context = BuildEvidence(trace, events, relationships, threads);
        if (query.HistoricalAt is not null)
        {
            context = BuildHistoricalEvidence(query, context, events);
        }

        string summary = BuildSummary(query, context);
        string details = BuildDetails(query, context);
        var diagnostics = trace.Diagnostics.ToList();
        if (query.HistoricalAt is not null)
        {
            diagnostics.Add($"Historical reconstruction used events visible at or before {query.HistoricalAt:O}.");
        }

        if (context.Evidence.Count == 0)
        {
            diagnostics.Add("No cited reasoning evidence was found for the requested trace.");
        }

        ReasoningReconstructionConfidence confidenceRationale = BuildConfidenceRationale(context, trace);
        ReasoningReconstructionScope scope = BuildScope(query, context, events);

        return new ReasoningReconstruction(
            repositoryId,
            DateTimeOffset.UtcNow,
            query,
            new ReasoningNarrative(summary, details),
            confidenceRationale.Level,
            confidenceRationale,
            scope,
            trace,
            context.Evidence,
            diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    public async Task<ReasoningReconstructionReport> RunReconstructionAsync(Guid repositoryId, ReasoningQuery query)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        ReasoningReconstruction reconstruction = await ReconstructAsync(repositoryId, query);
        var report = new ReasoningReconstructionReport(
            CreateReportId(),
            repositoryId,
            DateTimeOffset.UtcNow,
            reconstruction,
            ["Persisted only because a reconstruction run was explicitly requested."]);
        return await reasoningRepository.SaveReconstructionReportAsync(repository, report);
    }

    public async Task<IReadOnlyList<ReasoningReconstructionReport>> ListReportsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await reasoningRepository.ListReconstructionReportsAsync(repository);
    }

    private static EvidenceContext BuildEvidence(
        ReasoningTrace trace,
        IReadOnlyList<ReasoningEvent> events,
        IReadOnlyList<ReasoningRelationship> relationships,
        IReadOnlyList<ReasoningThread> threads)
    {
        var evidence = new List<ReasoningReconstructionEvidence>();
        var eventsById = events.ToDictionary(reasoningEvent => reasoningEvent.Id, StringComparer.Ordinal);
        var relationshipsById = relationships.ToDictionary(relationship => relationship.Id, StringComparer.Ordinal);
        var threadsById = threads.ToDictionary(thread => thread.Id, StringComparer.Ordinal);

        foreach (ReasoningGraphNode node in trace.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal))
        {
            if (node.Kind == ReasoningReferenceKind.ReasoningEvent && eventsById.TryGetValue(node.ReferenceId, out ReasoningEvent? reasoningEvent))
            {
                evidence.Add(new ReasoningReconstructionEvidence(
                    "Event",
                    reasoningEvent.Id,
                    $"{reasoningEvent.Type}: {reasoningEvent.Title}",
                    reasoningEvent.Narrative.Summary,
                    new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id),
                    reasoningEvent.Provenance));
            }
            else if (node.Kind == ReasoningReferenceKind.ReasoningThread && threadsById.TryGetValue(node.ReferenceId, out ReasoningThread? thread))
            {
                evidence.Add(new ReasoningReconstructionEvidence(
                    "Thread",
                    thread.Id,
                    $"{thread.Theme}: {thread.Title}",
                    thread.Summary,
                    new ReasoningReference(ReasoningReferenceKind.ReasoningThread, thread.Id),
                    null));
            }
            else if (node.Reference is not null)
            {
                evidence.Add(new ReasoningReconstructionEvidence(
                    "Reference",
                    node.ReferenceId,
                    $"{node.Kind}: {node.Label}",
                    node.Reference.Excerpt ?? node.Label,
                    node.Reference,
                    null));
            }
        }

        foreach (ReasoningGraphRelationship graphRelationship in trace.Relationships.OrderBy(relationship => relationship.Id, StringComparer.Ordinal))
        {
            if (graphRelationship.RelationshipId is not null &&
                relationshipsById.TryGetValue(graphRelationship.RelationshipId, out ReasoningRelationship? relationship))
            {
                evidence.Add(new ReasoningReconstructionEvidence(
                    "Relationship",
                    relationship.Id,
                    relationship.Type.ToString(),
                    relationship.Narrative.Summary,
                    relationship.Target,
                    relationship.Provenance));
            }
            else
            {
                evidence.Add(new ReasoningReconstructionEvidence(
                    "GraphRelationship",
                    graphRelationship.Id,
                    graphRelationship.Type.ToString(),
                    graphRelationship.Label,
                    null,
                    null));
            }
        }

        return new EvidenceContext(evidence);
    }

    private static EvidenceContext BuildHistoricalEvidence(
        ReasoningQuery query,
        EvidenceContext traceContext,
        IReadOnlyList<ReasoningEvent> events)
    {
        DateTimeOffset historicalAt = query.HistoricalAt!.Value;
        ReasoningEventFamily? family = FamilyFor(query.Category);
        IEnumerable<ReasoningEvent> timelineEvents = events
            .Where(reasoningEvent => reasoningEvent.CreatedAt <= historicalAt)
            .Where(reasoningEvent => family is null || reasoningEvent.Family == family)
            .Where(reasoningEvent => IsVisibleAt(reasoningEvent, query.Category))
            .OrderBy(reasoningEvent => reasoningEvent.CreatedAt)
            .ThenBy(reasoningEvent => reasoningEvent.Id, StringComparer.Ordinal);

        var evidence = new List<ReasoningReconstructionEvidence>();
        foreach (ReasoningEvent reasoningEvent in timelineEvents)
        {
            evidence.Add(new ReasoningReconstructionEvidence(
                "Event",
                reasoningEvent.Id,
                $"{reasoningEvent.Type}: {reasoningEvent.Title}",
                reasoningEvent.Narrative.Summary,
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id),
                reasoningEvent.Provenance));
        }

        evidence.AddRange(traceContext.RelationshipEvidence);
        evidence.AddRange(traceContext.ReferenceEvidence);
        evidence.AddRange(traceContext.ThreadEvidence);

        return new EvidenceContext(evidence
            .GroupBy(item => $"{item.Kind}:{item.Id}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray());
    }

    private static string BuildSummary(ReasoningQuery query, EvidenceContext context)
    {
        string target = $"{query.Target.Kind} {query.Target.Id}";
        string category = query.Category.ToString().ToLowerInvariant();
        if (context.EventEvidence.Count == 0 && context.RelationshipEvidence.Count == 0)
        {
            return $"No reasoning trace currently explains {target} for this {category} question.";
        }

        string leadingEvent = context.EventEvidence.FirstOrDefault()?.Title ?? context.RelationshipEvidence.First().Title;
        return $"The {category} question about {target} is reconstructed from {context.EventEvidence.Count} event(s) and {context.RelationshipEvidence.Count} relationship edge(s), led by {leadingEvent}.";
    }

    private static string BuildDetails(ReasoningQuery query, EvidenceContext context)
    {
        var lines = new List<string>
        {
            $"Question: {query.Question}",
            $"Target: {query.Target.Kind} {query.Target.Id}",
            $"Trace direction: {query.Direction}",
            $"Scale diagnostics: {context.Evidence.Count} evidence item(s), {context.EventEvidence.Count} event(s), {context.RelationshipEvidence.Count} relationship edge(s), {context.ReferenceEvidence.Count} external reference(s), {context.ThreadEvidence.Count} thread(s).",
            $"Evidence summary: {context.EventEvidence.Count} event(s), {context.RelationshipEvidence.Count} relationship edge(s), {context.ReferenceEvidence.Count} external reference(s), {context.ThreadEvidence.Count} thread(s)."
        };
        if (query.HistoricalAt is not null)
        {
            lines.Add($"Historical point: {query.HistoricalAt:O}");
            lines.Add("Historical state is derived from event timelines and is not a persisted lifecycle state.");
        }

        if (context.Evidence.Count == 0)
        {
            lines.Add("Events:");
            lines.Add("- None");
            return string.Join(Environment.NewLine, lines);
        }

        AppendEvidenceSection(lines, "Events", context.EventEvidence);
        AppendEvidenceSection(lines, "Relationships", context.RelationshipEvidence);
        AppendEvidenceSection(lines, "External References", context.ReferenceEvidence);
        AppendEvidenceSection(lines, "Threads", context.ThreadEvidence);

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendEvidenceSection(
        ICollection<string> lines,
        string heading,
        IReadOnlyList<ReasoningReconstructionEvidence> evidence)
    {
        lines.Add($"{heading}:");
        if (evidence.Count == 0)
        {
            lines.Add("- None");
            return;
        }

        foreach (ReasoningReconstructionEvidence item in evidence)
        {
            lines.Add($"- {item.Kind} {item.Id}: {item.Title} - {item.Summary}");
        }
    }

    private static ReasoningReconstructionConfidence BuildConfidenceRationale(EvidenceContext context, ReasoningTrace trace)
    {
        bool eventEvidencePresent = context.EventEvidence.Count > 0;
        bool relationshipEvidencePresent = context.RelationshipEvidence.Count > 0;
        bool traceDiagnosticsPresent = trace.Diagnostics.Count > 0;
        var missingEvidence = new List<string>();
        var whyNotHigher = new List<string>();

        if (!eventEvidencePresent)
        {
            missingEvidence.Add("No event evidence was reachable for the requested trace.");
            whyNotHigher.Add("High confidence requires at least one reachable reasoning event.");
        }

        if (!relationshipEvidencePresent)
        {
            missingEvidence.Add("No relationship evidence was reachable for the requested trace.");
            whyNotHigher.Add("High confidence requires at least one reachable reasoning relationship.");
        }

        if (traceDiagnosticsPresent)
        {
            whyNotHigher.Add("Trace diagnostics were present during reconstruction.");
        }

        if (context.EventEvidence.Count > 0 && context.RelationshipEvidence.Count > 0 && trace.Diagnostics.Count == 0)
        {
            return new ReasoningReconstructionConfidence(
                "High",
                "Event evidence and relationship evidence were both reachable, and the trace reported no diagnostics.",
                eventEvidencePresent,
                relationshipEvidencePresent,
                traceDiagnosticsPresent,
                missingEvidence,
                whyNotHigher);
        }

        if (context.EventEvidence.Count > 0 || context.RelationshipEvidence.Count > 0)
        {
            return new ReasoningReconstructionConfidence(
                "Medium",
                "Reconstruction found partial reasoning evidence but did not satisfy the complete high-confidence evidence threshold.",
                eventEvidencePresent,
                relationshipEvidencePresent,
                traceDiagnosticsPresent,
                missingEvidence,
                whyNotHigher);
        }

        return new ReasoningReconstructionConfidence(
            "Low",
            "Reconstruction did not find cited event or relationship evidence for the requested trace.",
            eventEvidencePresent,
            relationshipEvidencePresent,
            traceDiagnosticsPresent,
            missingEvidence,
            whyNotHigher);
    }

    private static ReasoningReconstructionScope BuildScope(
        ReasoningQuery query,
        EvidenceContext context,
        IReadOnlyList<ReasoningEvent> events)
    {
        ReasoningReference? source = context.Evidence
            .Select(evidence => evidence.Reference)
            .FirstOrDefault(reference => reference is not null && !IsSameReference(reference, query.Target));
        IReadOnlyList<ReasoningReconstructionEvidence> unreachableEvidence = query.HistoricalAt is null
            ? []
            : BuildHistoricalUnreachableEvidence(query, events);

        return new ReasoningReconstructionScope(
            query.Direction,
            query.Target,
            source,
            query.HistoricalAt,
            context.Evidence,
            unreachableEvidence);
    }

    private static IReadOnlyList<ReasoningReconstructionEvidence> BuildHistoricalUnreachableEvidence(
        ReasoningQuery query,
        IReadOnlyList<ReasoningEvent> events)
    {
        DateTimeOffset historicalAt = query.HistoricalAt!.Value;
        ReasoningEventFamily? family = FamilyFor(query.Category);
        return events
            .Where(reasoningEvent => reasoningEvent.CreatedAt > historicalAt)
            .Where(reasoningEvent => family is null || reasoningEvent.Family == family)
            .OrderBy(reasoningEvent => reasoningEvent.CreatedAt)
            .ThenBy(reasoningEvent => reasoningEvent.Id, StringComparer.Ordinal)
            .Select(reasoningEvent => new ReasoningReconstructionEvidence(
                "Event",
                reasoningEvent.Id,
                $"{reasoningEvent.Type}: {reasoningEvent.Title}",
                reasoningEvent.Narrative.Summary,
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id),
                reasoningEvent.Provenance))
            .ToArray();
    }

    private static bool IsSameReference(ReasoningReference? first, ReasoningReference second)
    {
        return first is not null &&
            first.Kind == second.Kind &&
            string.Equals(first.Id, second.Id, StringComparison.Ordinal);
    }

    private static ReasoningEventFamily? FamilyFor(ReasoningQueryCategory category)
    {
        return category switch
        {
            ReasoningQueryCategory.Hypothesis => ReasoningEventFamily.Hypothesis,
            ReasoningQueryCategory.Alternative => ReasoningEventFamily.Alternative,
            ReasoningQueryCategory.Contradiction => ReasoningEventFamily.Contradiction,
            ReasoningQueryCategory.Direction => ReasoningEventFamily.Direction,
            ReasoningQueryCategory.Assumption => ReasoningEventFamily.AssumptionEvolution,
            ReasoningQueryCategory.Decision => ReasoningEventFamily.DecisionEvolution,
            _ => null
        };
    }

    private static bool IsVisibleAt(ReasoningEvent reasoningEvent, ReasoningQueryCategory category)
    {
        return category switch
        {
            ReasoningQueryCategory.Hypothesis => reasoningEvent.Type is not ReasoningEventType.HypothesisInvalidated and not ReasoningEventType.HypothesisRetired,
            ReasoningQueryCategory.Contradiction => reasoningEvent.Type is not ReasoningEventType.ContradictionResolved,
            ReasoningQueryCategory.Direction => reasoningEvent.Type is not ReasoningEventType.DirectionAbandoned,
            ReasoningQueryCategory.Assumption => reasoningEvent.Type is not ReasoningEventType.AssumptionInvalidated and not ReasoningEventType.AssumptionReplaced,
            _ => true
        };
    }

    private static void ValidateQuery(ReasoningQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Question))
        {
            throw new ReasoningValidationException("Reasoning reconstruction question is required.");
        }

        if (string.IsNullOrWhiteSpace(query.Target.Id))
        {
            throw new ReasoningValidationException("Reasoning reconstruction target id is required.");
        }
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static string CreateReportId()
    {
        return $"reconstruction.{DateTime.UtcNow.ToString(ReportIdFormat, System.Globalization.CultureInfo.InvariantCulture)}";
    }

    private sealed record EvidenceContext(IReadOnlyList<ReasoningReconstructionEvidence> Evidence)
    {
        public IReadOnlyList<ReasoningReconstructionEvidence> EventEvidence { get; } =
            Evidence.Where(item => item.Kind == "Event").ToArray();

        public IReadOnlyList<ReasoningReconstructionEvidence> RelationshipEvidence { get; } =
            Evidence.Where(item => item.Kind is "Relationship" or "GraphRelationship").ToArray();

        public IReadOnlyList<ReasoningReconstructionEvidence> ReferenceEvidence { get; } =
            Evidence.Where(item => item.Kind == "Reference").ToArray();

        public IReadOnlyList<ReasoningReconstructionEvidence> ThreadEvidence { get; } =
            Evidence.Where(item => item.Kind == "Thread").ToArray();
    }
}
