using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Services;

public sealed class ReasoningMaterializationReviewService(
    IRepositoryService repositoryService,
    IReasoningRepository reasoningRepository)
    : IReasoningMaterializationReviewService
{
    public async Task<ReasoningMaterializationReviewReport> RunReviewAsync(
        Guid repositoryId,
        ReasoningMaterializationReviewRequest? request = null)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<ReasoningEvent> events = await reasoningRepository.ListEventsAsync(repository);
        IReadOnlyList<ReasoningThread> threads = await reasoningRepository.ListThreadsAsync(repository);
        IReadOnlyList<ReasoningRelationship> relationships = await reasoningRepository.ListRelationshipsAsync(repository);
        IReadOnlyList<ReasoningMaterializationScenario> scenarios = request?.Scenarios ?? Array.Empty<ReasoningMaterializationScenario>();

        ReasoningConceptMaterializationReview[] conceptReviews =
        [
            ReviewEventFamilyConcept(ReasoningMaterializationConcept.Hypothesis, ReasoningEventFamily.Hypothesis, events, threads, relationships, scenarios),
            ReviewEventFamilyConcept(ReasoningMaterializationConcept.Alternative, ReasoningEventFamily.Alternative, events, threads, relationships, scenarios),
            ReviewEventFamilyConcept(ReasoningMaterializationConcept.Contradiction, ReasoningEventFamily.Contradiction, events, threads, relationships, scenarios),
            ReviewEventFamilyConcept(ReasoningMaterializationConcept.Direction, ReasoningEventFamily.Direction, events, threads, relationships, scenarios),
            ReviewThreadConcept(threads, events, relationships, scenarios)
        ];

        ReasoningTaxonomyMaterializationFinding[] taxonomyFindings = BuildTaxonomyFindings(events);
        var diagnostics = new List<string>();
        if (conceptReviews.Any(review => review.Recommendation == ReasoningMaterializationOutcome.PromoteToFirstClassEntity))
        {
            diagnostics.Add("A first-class promotion recommendation is advisory only and must be implemented in a separate slice.");
        }

        if (taxonomyFindings.Any(finding => finding.LifecycleRisk))
        {
            diagnostics.Add("One or more event families show lifecycle-like growth; review taxonomy before adding more event types.");
        }

        return new ReasoningMaterializationReviewReport(
            repositoryId,
            DateTimeOffset.UtcNow,
            conceptReviews,
            taxonomyFindings,
            diagnostics.Order(StringComparer.Ordinal).ToArray());
    }

    private static ReasoningConceptMaterializationReview ReviewEventFamilyConcept(
        ReasoningMaterializationConcept concept,
        ReasoningEventFamily family,
        IReadOnlyList<ReasoningEvent> events,
        IReadOnlyList<ReasoningThread> threads,
        IReadOnlyList<ReasoningRelationship> relationships,
        IReadOnlyList<ReasoningMaterializationScenario> scenarios)
    {
        ReasoningEvent[] familyEvents = events
            .Where(reasoningEvent => reasoningEvent.Family == family)
            .OrderBy(reasoningEvent => reasoningEvent.Id, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> eventIds = familyEvents.Select(reasoningEvent => reasoningEvent.Id).ToHashSet(StringComparer.Ordinal);
        int relationshipCount = relationships.Count(relationship =>
            IsReasoningEventReference(relationship.Source, eventIds) ||
            IsReasoningEventReference(relationship.Target, eventIds));
        int threadCount = threads.Count(thread => thread.EventIds.Any(eventIds.Contains));
        ReasoningMaterializationScenario[] conceptScenarios = ScenariosFor(scenarios, concept);
        int failedScenarioCount = conceptScenarios.Count(scenario => scenario.ReconstructionFailed);
        int repeatedWorkflowCount = conceptScenarios.Sum(scenario => Math.Max(0, scenario.RepeatedWorkflowCount));
        ReasoningMaterializationOutcome outcome = SelectOutcome(concept, failedScenarioCount, repeatedWorkflowCount);
        var evidence = new List<string>
        {
            $"{familyEvents.Length} {family} event(s) exist.",
            $"{relationshipCount} relationship(s) touch those events.",
            $"{threadCount} thread(s) include those events."
        };
        evidence.AddRange(conceptScenarios.Select(scenario =>
            $"{scenario.Question}: {(scenario.ReconstructionFailed ? "failed" : "reconstructable")} - {scenario.Evidence}"));

        var risks = new List<string>
        {
            $"{concept} must remain explanatory and cannot approve, reject, or mutate authoritative domain state."
        };
        if (concept == ReasoningMaterializationConcept.Direction)
        {
            risks.Add("Direction is especially likely to imply strategy authority if promoted without repeated reconstruction failure.");
        }

        if (failedScenarioCount > 0)
        {
            risks.Add("Failed reconstruction evidence should be addressed through query, trace, or report improvements before adding persistence.");
        }

        return new ReasoningConceptMaterializationReview(
            concept,
            outcome,
            BuildConceptSummary(concept, outcome, failedScenarioCount, repeatedWorkflowCount),
            evidence,
            risks);
    }

    private static ReasoningConceptMaterializationReview ReviewThreadConcept(
        IReadOnlyList<ReasoningThread> threads,
        IReadOnlyList<ReasoningEvent> events,
        IReadOnlyList<ReasoningRelationship> relationships,
        IReadOnlyList<ReasoningMaterializationScenario> scenarios)
    {
        ReasoningMaterializationScenario[] threadScenarios = ScenariosFor(scenarios, ReasoningMaterializationConcept.Thread);
        int failedScenarioCount = threadScenarios.Count(scenario => scenario.ReconstructionFailed);
        int repeatedWorkflowCount = threadScenarios.Sum(scenario => Math.Max(0, scenario.RepeatedWorkflowCount));
        ReasoningMaterializationOutcome outcome = failedScenarioCount >= 2 || repeatedWorkflowCount >= 3
            ? ReasoningMaterializationOutcome.AddReadModelReport
            : ReasoningMaterializationOutcome.RemainDerived;
        int eventsInThreads = threads.SelectMany(thread => thread.EventIds).Distinct(StringComparer.Ordinal).Count();
        int relationshipCount = relationships.Count(relationship =>
            relationship.Source.Kind == ReasoningReferenceKind.ReasoningThread ||
            relationship.Target.Kind == ReasoningReferenceKind.ReasoningThread);
        var evidence = new List<string>
        {
            $"{threads.Count} persisted thread(s) exist.",
            $"{eventsInThreads} distinct event membership link(s) exist across {events.Count} event(s).",
            $"{relationshipCount} relationship(s) directly reference threads."
        };
        evidence.AddRange(threadScenarios.Select(scenario =>
            $"{scenario.Question}: {(scenario.ReconstructionFailed ? "failed" : "reconstructable")} - {scenario.Evidence}"));

        return new ReasoningConceptMaterializationReview(
            ReasoningMaterializationConcept.Thread,
            outcome,
            outcome == ReasoningMaterializationOutcome.RemainDerived
                ? "Thread identity remains a reviewable grouping mechanism; no stronger thread authority is justified."
                : "Thread usage shows repeated reconstruction pressure; prefer a read-model review report before changing persistence.",
            evidence,
            [
                "Persisted threads can become too authoritative if treated as decisions, sessions, or current strategy.",
                "Thread demotion remains possible because graph traversal can rebuild grouping evidence from events and relationships."
            ]);
    }

    private static ReasoningMaterializationOutcome SelectOutcome(
        ReasoningMaterializationConcept concept,
        int failedScenarioCount,
        int repeatedWorkflowCount)
    {
        if (failedScenarioCount >= 2)
        {
            return ReasoningMaterializationOutcome.AddReadModelReport;
        }

        if (repeatedWorkflowCount >= 3)
        {
            return ReasoningMaterializationOutcome.AddDerivedCache;
        }

        return concept == ReasoningMaterializationConcept.Direction
            ? ReasoningMaterializationOutcome.RemainDerived
            : ReasoningMaterializationOutcome.RemainDerived;
    }

    private static string BuildConceptSummary(
        ReasoningMaterializationConcept concept,
        ReasoningMaterializationOutcome outcome,
        int failedScenarioCount,
        int repeatedWorkflowCount)
    {
        if (outcome == ReasoningMaterializationOutcome.RemainDerived)
        {
            return $"{concept} remains reconstructable as classification evidence; no first-class persistence is justified.";
        }

        return $"{concept} has {failedScenarioCount} failed reconstruction scenario(s) and {repeatedWorkflowCount} repeated workflow signal(s); recommendation is advisory and does not create a new artifact family.";
    }

    private static ReasoningTaxonomyMaterializationFinding[] BuildTaxonomyFindings(IReadOnlyList<ReasoningEvent> events)
    {
        return events
            .GroupBy(reasoningEvent => reasoningEvent.Family)
            .Select(group =>
            {
                ReasoningEventType[] eventTypes = group.Select(reasoningEvent => reasoningEvent.Type).Distinct().ToArray();
                bool lifecycleRisk = eventTypes.Length >= 4 && eventTypes.Any(IsLifecycleTerminalType);
                return new ReasoningTaxonomyMaterializationFinding(
                    group.Key,
                    eventTypes.Length,
                    lifecycleRisk,
                    lifecycleRisk
                        ? $"{group.Key} has enough event-type variety to resemble a hidden lifecycle; keep it explicitly derived or simplify the taxonomy."
                        : $"{group.Key} remains classification vocabulary.",
                    eventTypes.OrderBy(type => type.ToString(), StringComparer.Ordinal).Select(type => type.ToString()).ToArray());
            })
            .OrderBy(finding => finding.Family.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsLifecycleTerminalType(ReasoningEventType type)
    {
        string name = type.ToString();
        return name.EndsWith("Invalidated", StringComparison.Ordinal) ||
            name.EndsWith("Retired", StringComparison.Ordinal) ||
            name.EndsWith("Rejected", StringComparison.Ordinal) ||
            name.EndsWith("Selected", StringComparison.Ordinal) ||
            name.EndsWith("Resolved", StringComparison.Ordinal) ||
            name.EndsWith("Accepted", StringComparison.Ordinal) ||
            name.EndsWith("Recurred", StringComparison.Ordinal) ||
            name.EndsWith("Abandoned", StringComparison.Ordinal);
    }

    private static ReasoningMaterializationScenario[] ScenariosFor(
        IReadOnlyList<ReasoningMaterializationScenario> scenarios,
        ReasoningMaterializationConcept concept)
    {
        return scenarios.Where(scenario => scenario.Concept == concept).ToArray();
    }

    private static bool IsReasoningEventReference(ReasoningReference reference, ISet<string> eventIds)
    {
        return reference.Kind == ReasoningReferenceKind.ReasoningEvent && eventIds.Contains(reference.Id);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }
}
