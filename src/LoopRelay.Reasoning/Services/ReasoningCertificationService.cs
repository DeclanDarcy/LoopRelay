using LoopRelay.Core.Repositories;
using LoopRelay.Reasoning.Abstractions;
using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Services;

public sealed class ReasoningCertificationService(
    IRepositoryService repositoryService,
    IReasoningRepository reasoningRepository,
    IReasoningGraphService graphService,
    IReasoningQueryService queryService)
    : IReasoningCertificationService
{
    public async Task<ReasoningCertificationReport> GetCurrentCertificationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await BuildReportAsync(repository, "certification.current");
    }

    public async Task<ReasoningCertificationReport> RunCertificationAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        ReasoningCertificationReport report = await BuildReportAsync(repository, CreateReportId());
        return await reasoningRepository.SaveCertificationReportAsync(repository, report);
    }

    public async Task<IReadOnlyList<ReasoningCertificationReport>> ListReportsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await reasoningRepository.ListCertificationReportsAsync(repository);
    }

    private async Task<ReasoningCertificationReport> BuildReportAsync(Repository repository, string reportId)
    {
        IReadOnlyList<ReasoningEvent> events = await reasoningRepository.ListEventsAsync(repository);
        IReadOnlyList<ReasoningThread> threads = await reasoningRepository.ListThreadsAsync(repository);
        IReadOnlyList<ReasoningRelationship> relationships = await reasoningRepository.ListRelationshipsAsync(repository);
        ReasoningGraph graph = await graphService.GetGraphAsync(repository.Id);
        var evidence = new List<ReasoningCertificationEvidence>();
        var diagnostics = graph.Diagnostics
            .Where(IsExternalDiagnostic)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        AddBaselineEvidence(evidence, events, threads, relationships);
        AddEventImmutabilityEvidence(evidence, events);
        AddProvenanceEvidence(evidence, events, relationships);
        AddRelationshipIntegrityEvidence(evidence, graph);
        AddThreadNavigabilityEvidence(evidence, events, threads, graph);
        await AddQueryReproducibilityEvidenceAsync(evidence, repository, events, relationships);
        await AddOutcomeEvidenceAsync(evidence, repository, events, threads, relationships);

        bool passed = evidence.All(item => item.Passed);
        var result = new ReasoningCertificationResult(
            passed ? ReasoningCertificationResultKind.Passed : ReasoningCertificationResultKind.Failed,
            passed
                ? "Reasoning reconstruction is certifiable from repository artifacts."
                : "Reasoning reconstruction certification found failures.");

        return new ReasoningCertificationReport(
            reportId,
            repository.Id,
            DateTimeOffset.UtcNow,
            result,
            evidence.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray(),
            diagnostics);
    }

    private static void AddBaselineEvidence(
        ICollection<ReasoningCertificationEvidence> evidence,
        IReadOnlyList<ReasoningEvent> events,
        IReadOnlyList<ReasoningThread> threads,
        IReadOnlyList<ReasoningRelationship> relationships)
    {
        if (events.Count == 0 && threads.Count == 0 && relationships.Count == 0)
        {
            evidence.Add(Passed(
                "CERT-000",
                "No reasoning captured",
                "No reasoning artifacts exist, which is a valid baseline.",
                ["The repository has not captured reasoning trajectory artifacts yet."],
                []));
            return;
        }

        evidence.Add(Passed(
            "CERT-000",
            "Structured reasoning artifacts load",
            $"Loaded {events.Count} event(s), {threads.Count} thread(s), and {relationships.Count} relationship(s) from structured JSON.",
            ["Certification uses structured artifacts and does not depend on markdown projections."],
            []));
    }

    private static void AddProvenanceEvidence(
        ICollection<ReasoningCertificationEvidence> evidence,
        IReadOnlyList<ReasoningEvent> events,
        IReadOnlyList<ReasoningRelationship> relationships)
    {
        string[] missingEvents = events
            .Where(reasoningEvent => !HasProvenance(reasoningEvent.Provenance))
            .Select(reasoningEvent => reasoningEvent.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] missingRelationships = relationships
            .Where(relationship => !HasProvenance(relationship.Provenance))
            .Select(relationship => relationship.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        bool passed = missingEvents.Length == 0 && missingRelationships.Length == 0;
        evidence.Add(new ReasoningCertificationEvidence(
            "CERT-010",
            "Provenance completeness",
            passed,
            passed ? "Every reasoning event and relationship has provenance." : "Some reasoning artifacts lack provenance.",
            missingEvents.Select(id => $"Event {id} lacks complete provenance.")
                .Concat(missingRelationships.Select(id => $"Relationship {id} lacks complete provenance."))
                .DefaultIfEmpty("Source kind and captured-by are populated for all provenance-bearing artifacts.")
                .ToArray(),
            missingEvents.Select(id => new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, id))
                .Concat(missingRelationships.Select(id => new ReasoningReference(ReasoningReferenceKind.Artifact, id)))
                .ToArray()));
    }

    private static void AddEventImmutabilityEvidence(
        ICollection<ReasoningCertificationEvidence> evidence,
        IReadOnlyList<ReasoningEvent> events)
    {
        string[] duplicateIds = events
            .GroupBy(reasoningEvent => reasoningEvent.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        bool passed = duplicateIds.Length == 0;
        evidence.Add(new ReasoningCertificationEvidence(
            "CERT-005",
            "Event immutability",
            passed,
            passed ? "Reasoning events are represented as append-only records with stable IDs." : "Duplicate event IDs were loaded.",
            duplicateIds.Select(id => $"Duplicate reasoning event id loaded: {id}.")
                .DefaultIfEmpty("No mutation path or duplicate event identity was detected during repository reconstruction.")
                .ToArray(),
            events.Select(reasoningEvent => new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id)).ToArray()));
    }

    private static void AddRelationshipIntegrityEvidence(
        ICollection<ReasoningCertificationEvidence> evidence,
        ReasoningGraph graph)
    {
        string[] failures = graph.Diagnostics
            .Where(IsReasoningIntegrityDiagnostic)
            .Order(StringComparer.Ordinal)
            .ToArray();
        bool passed = failures.Length == 0;
        evidence.Add(new ReasoningCertificationEvidence(
            "CERT-020",
            "Relationship integrity",
            passed,
            passed ? "Reasoning relationships resolve required reasoning nodes." : "A required reasoning relationship endpoint is missing.",
            failures.DefaultIfEmpty("No missing reasoning event or thread endpoints were found.").ToArray(),
            []));
    }

    private static void AddThreadNavigabilityEvidence(
        ICollection<ReasoningCertificationEvidence> evidence,
        IReadOnlyList<ReasoningEvent> events,
        IReadOnlyList<ReasoningThread> threads,
        ReasoningGraph graph)
    {
        string[] failures = graph.Diagnostics
            .Where(diagnostic => diagnostic.StartsWith("Thread ", StringComparison.Ordinal) ||
                (diagnostic.StartsWith("Event ", StringComparison.Ordinal) &&
                    diagnostic.Contains("references missing reasoning thread", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        bool passed = failures.Length == 0;
        evidence.Add(new ReasoningCertificationEvidence(
            "CERT-030",
            "Thread navigability",
            passed,
            passed ? "Reasoning threads are navigable from their event memberships." : "A thread membership cannot be navigated.",
            failures.DefaultIfEmpty($"Loaded {threads.Count} thread(s) over {events.Count} event(s).").ToArray(),
            threads.Select(thread => new ReasoningReference(ReasoningReferenceKind.ReasoningThread, thread.Id)).ToArray()));
    }

    private async Task AddQueryReproducibilityEvidenceAsync(
        ICollection<ReasoningCertificationEvidence> evidence,
        Repository repository,
        IReadOnlyList<ReasoningEvent> events,
        IReadOnlyList<ReasoningRelationship> relationships)
    {
        if (events.Count == 0 && relationships.Count == 0)
        {
            evidence.Add(Passed(
                "CERT-040",
                "Query reproducibility",
                "No reasoning query is required before reasoning artifacts exist.",
                ["No reasoning artifacts exist."],
                []));
            return;
        }

        ReasoningReference target = FindDecisionTarget(relationships) ??
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, events.OrderBy(item => item.Id, StringComparer.Ordinal).Last().Id);
        var query = new ReasoningQuery(
            ReasoningQueryCategory.Decision,
            "Can the same reasoning trace be reconstructed reproducibly?",
            target);
        ReasoningQueryResult first = await queryService.RunQueryAsync(repository.Id, query);
        ReasoningQueryResult second = await queryService.RunQueryAsync(repository.Id, query);
        string[] firstSignature = QuerySignature(first);
        string[] secondSignature = QuerySignature(second);
        bool passed = firstSignature.SequenceEqual(secondSignature, StringComparer.Ordinal);
        evidence.Add(new ReasoningCertificationEvidence(
            "CERT-040",
            "Query reproducibility",
            passed,
            passed ? "Repeated reconstruction returned equivalent evidence." : "Repeated reconstruction returned different evidence.",
            passed
                ? [$"Reconstructed {first.Reconstruction.Evidence.Count} evidence item(s) twice."]
                : ["The query evidence signature changed between runs."],
            [target]));
    }

    private async Task AddOutcomeEvidenceAsync(
        ICollection<ReasoningCertificationEvidence> evidence,
        Repository repository,
        IReadOnlyList<ReasoningEvent> events,
        IReadOnlyList<ReasoningThread> threads,
        IReadOnlyList<ReasoningRelationship> relationships)
    {
        await AddOutcomeAsync(
            evidence,
            repository,
            "CERT-100",
            "Decision supersession reasoning",
            events.Any(item => item.Type == ReasoningEventType.DecisionSuperseded),
            ReasoningQueryCategory.Decision,
            "Why did this decision replace an earlier decision?",
            FindDecisionTarget(relationships) ?? FindEventTarget(events, ReasoningEventType.DecisionSuperseded));
        await AddOutcomeAsync(
            evidence,
            repository,
            "CERT-110",
            "Alternative rejection reasoning",
            events.Any(item => item.Type == ReasoningEventType.AlternativeRejected),
            ReasoningQueryCategory.Alternative,
            "Why was this alternative rejected?",
            FindEventTarget(events, ReasoningEventType.AlternativeSelected) ?? FindEventTarget(events, ReasoningEventType.AlternativeRejected));
        await AddOutcomeAsync(
            evidence,
            repository,
            "CERT-120",
            "Contradiction importance reasoning",
            events.Any(item => item.Family == ReasoningEventFamily.Contradiction),
            ReasoningQueryCategory.Contradiction,
            "Which contradiction changed the direction of work?",
            FindEventTarget(events, ReasoningEventType.DirectionShifted) ?? FindEventTarget(events, ReasoningEventFamily.Contradiction));
        await AddOutcomeAsync(
            evidence,
            repository,
            "CERT-130",
            "Assumption failure reasoning",
            events.Any(item => item.Type == ReasoningEventType.AssumptionInvalidated),
            ReasoningQueryCategory.Assumption,
            "What assumption failed?",
            FindEventTarget(events, ReasoningEventType.AssumptionInvalidated));
        await AddOutcomeAsync(
            evidence,
            repository,
            "CERT-140",
            "Direction emergence reasoning",
            events.Any(item => item.Family == ReasoningEventFamily.Direction),
            ReasoningQueryCategory.Direction,
            "How did the current strategy emerge?",
            FindDecisionTarget(relationships) ?? FindEventTarget(events, ReasoningEventFamily.Direction));
        await AddOutcomeAsync(
            evidence,
            repository,
            "CERT-150",
            "Cross-milestone thread reasoning",
            threads.Count > 0,
            ReasoningQueryCategory.Thread,
            "Can a reasoning thread be reconstructed across milestones?",
            threads.OrderBy(thread => thread.Id, StringComparer.Ordinal)
                .Select(thread => new ReasoningReference(ReasoningReferenceKind.ReasoningThread, thread.Id))
                .FirstOrDefault());
    }

    private async Task AddOutcomeAsync(
        ICollection<ReasoningCertificationEvidence> evidence,
        Repository repository,
        string id,
        string scenario,
        bool isApplicable,
        ReasoningQueryCategory category,
        string question,
        ReasoningReference? target)
    {
        if (!isApplicable || target is null)
        {
            evidence.Add(Passed(
                id,
                scenario,
                "No captured reasoning currently requires this outcome scenario.",
                ["Scenario is not applicable to the captured event families and types."],
                []));
            return;
        }

        ReasoningQueryResult result = await queryService.RunQueryAsync(
            repository.Id,
            new ReasoningQuery(category, question, target));
        string[] failureDiagnostics = result.Diagnostics
            .Concat(result.Reconstruction.Diagnostics)
            .Where(IsReasoningIntegrityDiagnostic)
            .Order(StringComparer.Ordinal)
            .ToArray();
        bool passed =
            result.Reconstruction.Evidence.Count > 0 &&
            result.Reconstruction.Confidence is "High" or "Medium" &&
            failureDiagnostics.Length == 0;

        evidence.Add(new ReasoningCertificationEvidence(
            id,
            scenario,
            passed,
            passed ? "Outcome scenario is answerable through generic reconstruction." : "Outcome scenario is not currently answerable.",
            passed
                ? [$"Answered with {result.Reconstruction.Evidence.Count} evidence item(s) at {result.Reconstruction.Confidence} confidence."]
                : failureDiagnostics.DefaultIfEmpty(result.Reconstruction.Narrative.Summary).ToArray(),
            [target]));
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static ReasoningCertificationEvidence Passed(
        string id,
        string scenario,
        string summary,
        IReadOnlyList<string> details,
        IReadOnlyList<ReasoningReference> references)
    {
        return new ReasoningCertificationEvidence(id, scenario, true, summary, details, references);
    }

    private static bool HasProvenance(ReasoningProvenance provenance)
    {
        return !string.IsNullOrWhiteSpace(provenance.SourceKind) &&
            !string.IsNullOrWhiteSpace(provenance.CapturedBy);
    }

    private static bool IsReasoningIntegrityDiagnostic(string diagnostic)
    {
        return diagnostic.Contains("missing reasoning event", StringComparison.Ordinal) ||
            diagnostic.Contains("missing reasoning thread", StringComparison.Ordinal) ||
            diagnostic.Contains("unresolved ReasoningEvent", StringComparison.Ordinal) ||
            diagnostic.Contains("unresolved ReasoningThread", StringComparison.Ordinal) ||
            (diagnostic.Contains("unresolved", StringComparison.Ordinal) &&
                (diagnostic.Contains("ReasoningEvent", StringComparison.Ordinal) ||
                    diagnostic.Contains("ReasoningThread", StringComparison.Ordinal)));
    }

    private static bool IsExternalDiagnostic(string diagnostic)
    {
        return !IsReasoningIntegrityDiagnostic(diagnostic);
    }

    private static ReasoningReference? FindDecisionTarget(IReadOnlyList<ReasoningRelationship> relationships)
    {
        return relationships
            .SelectMany(relationship => new[] { relationship.Target, relationship.Source })
            .Where(reference => reference.Kind == ReasoningReferenceKind.Decision)
            .OrderBy(reference => reference.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static ReasoningReference? FindEventTarget(IReadOnlyList<ReasoningEvent> events, ReasoningEventType type)
    {
        return events
            .Where(reasoningEvent => reasoningEvent.Type == type)
            .OrderBy(reasoningEvent => reasoningEvent.Id, StringComparer.Ordinal)
            .Select(reasoningEvent => new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id))
            .FirstOrDefault();
    }

    private static ReasoningReference? FindEventTarget(IReadOnlyList<ReasoningEvent> events, ReasoningEventFamily family)
    {
        return events
            .Where(reasoningEvent => reasoningEvent.Family == family)
            .OrderBy(reasoningEvent => reasoningEvent.Id, StringComparer.Ordinal)
            .Select(reasoningEvent => new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id))
            .FirstOrDefault();
    }

    private static string[] QuerySignature(ReasoningQueryResult result)
    {
        return result.Reconstruction.Evidence
            .Select(evidence => $"evidence:{evidence.Kind}:{evidence.Id}:{evidence.Title}:{evidence.Summary}")
            .Concat(result.Reconstruction.Trace.Relationships.Select(relationship =>
                $"relationship:{relationship.Id}:{relationship.Type}:{relationship.SourceNodeId}:{relationship.TargetNodeId}"))
            .Concat(result.Diagnostics.Select(diagnostic => $"query-diagnostic:{diagnostic}"))
            .Concat(result.Reconstruction.Diagnostics.Select(diagnostic => $"reconstruction-diagnostic:{diagnostic}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string CreateReportId()
    {
        return $"certification.{DateTimeOffset.UtcNow:yyyyMMddHHmmssfffffff}";
    }
}
