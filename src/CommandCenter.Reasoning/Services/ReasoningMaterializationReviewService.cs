using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Services;

public sealed class ReasoningMaterializationReviewService(
    IRepositoryService repositoryService,
    IReasoningRepository reasoningRepository)
    : IReasoningMaterializationReviewService
{
    private const int FailedScenarioThreshold = 2;
    private const int RepeatedWorkflowThreshold = 3;
    private const int LifecycleEventTypeThreshold = 4;

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
        string branchReason = BuildBranchReason(outcome, failedScenarioCount, repeatedWorkflowCount);
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

        IReadOnlyList<string> elevatedRiskSignals = BuildElevatedRiskSignals(
            concept,
            failedScenarioCount,
            repeatedWorkflowCount,
            outcome);

        return new ReasoningConceptMaterializationReview(
            concept,
            outcome,
            BuildConceptSummary(concept, outcome, failedScenarioCount, repeatedWorkflowCount),
            failedScenarioCount,
            repeatedWorkflowCount,
            FailedScenarioThreshold,
            RepeatedWorkflowThreshold,
            branchReason,
            elevatedRiskSignals,
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
        ReasoningMaterializationOutcome outcome = failedScenarioCount >= FailedScenarioThreshold || repeatedWorkflowCount >= RepeatedWorkflowThreshold
            ? ReasoningMaterializationOutcome.AddReadModelReport
            : ReasoningMaterializationOutcome.RemainDerived;
        string branchReason = BuildBranchReason(outcome, failedScenarioCount, repeatedWorkflowCount);
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
            failedScenarioCount,
            repeatedWorkflowCount,
            FailedScenarioThreshold,
            RepeatedWorkflowThreshold,
            branchReason,
            BuildElevatedRiskSignals(ReasoningMaterializationConcept.Thread, failedScenarioCount, repeatedWorkflowCount, outcome),
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
        if (failedScenarioCount >= FailedScenarioThreshold)
        {
            return ReasoningMaterializationOutcome.AddReadModelReport;
        }

        if (repeatedWorkflowCount >= RepeatedWorkflowThreshold)
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
                ReasoningEventType[] terminalEventTypes = eventTypes.Where(IsLifecycleTerminalType).ToArray();
                bool terminalEventTypePresent = terminalEventTypes.Length > 0;
                bool lifecycleRisk = eventTypes.Length >= LifecycleEventTypeThreshold && terminalEventTypePresent;
                string riskReason = lifecycleRisk
                    ? $"Lifecycle risk is flagged because {eventTypes.Length} event type(s) meet or exceed threshold {LifecycleEventTypeThreshold} and terminal event types are present."
                    : $"Lifecycle risk is not flagged because the family has {eventTypes.Length} event type(s) against threshold {LifecycleEventTypeThreshold} and terminal event presence is {terminalEventTypePresent}.";
                return new ReasoningTaxonomyMaterializationFinding(
                    group.Key,
                    eventTypes.Length,
                    LifecycleEventTypeThreshold,
                    lifecycleRisk,
                    terminalEventTypePresent,
                    terminalEventTypes.OrderBy(type => type.ToString(), StringComparer.Ordinal).ToArray(),
                    riskReason,
                    lifecycleRisk
                        ? $"{group.Key} has enough event-type variety to resemble a hidden lifecycle; keep it explicitly derived or simplify the taxonomy."
                        : $"{group.Key} remains classification vocabulary.",
                    eventTypes.OrderBy(type => type.ToString(), StringComparer.Ordinal).Select(type => type.ToString()).ToArray());
            })
            .OrderBy(finding => finding.Family.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildBranchReason(
        ReasoningMaterializationOutcome outcome,
        int failedScenarioCount,
        int repeatedWorkflowCount)
    {
        return outcome switch
        {
            ReasoningMaterializationOutcome.AddReadModelReport =>
                failedScenarioCount >= FailedScenarioThreshold
                    ? $"Failed scenario count {failedScenarioCount} met threshold {FailedScenarioThreshold}; recommendation is a report, not a new authority."
                    : $"Repeated workflow count {repeatedWorkflowCount} met threshold {RepeatedWorkflowThreshold}; recommendation is a report for review.",
            ReasoningMaterializationOutcome.AddDerivedCache =>
                $"Repeated workflow count {repeatedWorkflowCount} met threshold {RepeatedWorkflowThreshold}; recommendation is derived cache pressure only.",
            ReasoningMaterializationOutcome.PromoteToFirstClassEntity =>
                "Promotion is advisory and still requires a separate authority-owning slice.",
            ReasoningMaterializationOutcome.RejectConcept =>
                "Concept was rejected because submitted evidence was insufficient.",
            _ =>
                $"No threshold was met: {failedScenarioCount}/{FailedScenarioThreshold} failed scenarios and {repeatedWorkflowCount}/{RepeatedWorkflowThreshold} repeated workflow signals."
        };
    }

    private static IReadOnlyList<string> BuildElevatedRiskSignals(
        ReasoningMaterializationConcept concept,
        int failedScenarioCount,
        int repeatedWorkflowCount,
        ReasoningMaterializationOutcome outcome)
    {
        var signals = new List<string>();
        if (failedScenarioCount > 0)
        {
            signals.Add($"{failedScenarioCount} failed reconstruction scenario(s) require explanation before persistence changes.");
        }

        if (repeatedWorkflowCount > 0)
        {
            signals.Add($"{repeatedWorkflowCount} repeated workflow signal(s) indicate operational friction.");
        }

        if (concept == ReasoningMaterializationConcept.Direction)
        {
            signals.Add("Direction materialization can imply strategic authority.");
        }

        if (outcome != ReasoningMaterializationOutcome.RemainDerived)
        {
            signals.Add($"{outcome} remains advisory and does not grant artifact authority.");
        }

        return signals.Order(StringComparer.Ordinal).ToArray();
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
