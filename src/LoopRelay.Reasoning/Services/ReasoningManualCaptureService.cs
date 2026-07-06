using LoopRelay.Reasoning.Abstractions;
using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Services;

public sealed class ReasoningManualCaptureService(
    IReasoningEventService eventService,
    IReasoningThreadService threadService)
    : IReasoningManualCaptureService
{
    public const string UserSuppliedProvenanceSourceKind = "UserSupplied";
    public const string ManualCaptureProvenanceSourceKind = "ManualCapture";

    private static readonly IReadOnlyDictionary<ReasoningManualCaptureKind, ManualReasoningCaptureTemplate> Templates =
        CreateTemplates().ToDictionary(template => template.Kind);

    public IReadOnlyList<ManualReasoningCaptureTemplate> ListTemplates()
    {
        return Templates.Values
            .OrderBy(template => template.Kind.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<ReasoningEvent> CaptureAsync(Guid repositoryId, ManualReasoningCaptureCommand command)
    {
        if (!Templates.TryGetValue(command.Kind, out ManualReasoningCaptureTemplate? template))
        {
            throw new ReasoningValidationException($"Unsupported manual reasoning capture kind: {command.Kind}.");
        }

        ValidateManualProvenance(command.Provenance);

        foreach (string threadId in command.ThreadIds ?? Array.Empty<string>())
        {
            await threadService.GetThreadAsync(repositoryId, threadId);
        }

        ReasoningEvent reasoningEvent = await eventService.CreateEventAsync(repositoryId, new CreateReasoningEventCommand(
            template.Family,
            template.Type,
            command.Title,
            command.Narrative,
            command.References,
            command.Provenance,
            command.ThreadIds,
            command.Tags));

        foreach (string threadId in command.ThreadIds ?? Array.Empty<string>())
        {
            await threadService.AppendThreadEventAsync(repositoryId, threadId, reasoningEvent.Id);
        }

        return reasoningEvent;
    }

    private static void ValidateManualProvenance(ReasoningProvenance provenance)
    {
        if (!string.Equals(provenance.SourceKind, UserSuppliedProvenanceSourceKind, StringComparison.Ordinal) &&
            !string.Equals(provenance.SourceKind, ManualCaptureProvenanceSourceKind, StringComparison.Ordinal))
        {
            throw new ReasoningValidationException(
                $"Manual reasoning captures require {UserSuppliedProvenanceSourceKind} or {ManualCaptureProvenanceSourceKind} provenance.");
        }
    }

    private static IReadOnlyList<ManualReasoningCaptureTemplate> CreateTemplates()
    {
        return
        [
            Decision(ReasoningManualCaptureKind.DecisionSuperseded, ReasoningEventType.DecisionSuperseded),
            Decision(ReasoningManualCaptureKind.DecisionReframed, ReasoningEventType.DecisionReframed),
            Decision(ReasoningManualCaptureKind.DecisionReconsidered, ReasoningEventType.DecisionReconsidered),
            Template(ReasoningManualCaptureKind.HypothesisRaised, ReasoningEventFamily.Hypothesis, ReasoningEventType.HypothesisRaised, ReasoningThreadTheme.BeliefUnderInvestigation),
            Template(ReasoningManualCaptureKind.HypothesisSupported, ReasoningEventFamily.Hypothesis, ReasoningEventType.HypothesisSupported, ReasoningThreadTheme.BeliefUnderInvestigation),
            Template(ReasoningManualCaptureKind.HypothesisChallenged, ReasoningEventFamily.Hypothesis, ReasoningEventType.HypothesisChallenged, ReasoningThreadTheme.BeliefUnderInvestigation),
            Template(ReasoningManualCaptureKind.HypothesisInvalidated, ReasoningEventFamily.Hypothesis, ReasoningEventType.HypothesisInvalidated, ReasoningThreadTheme.BeliefUnderInvestigation),
            Template(ReasoningManualCaptureKind.HypothesisRetired, ReasoningEventFamily.Hypothesis, ReasoningEventType.HypothesisRetired, ReasoningThreadTheme.BeliefUnderInvestigation),
            Template(ReasoningManualCaptureKind.AlternativeIntroduced, ReasoningEventFamily.Alternative, ReasoningEventType.AlternativeIntroduced, ReasoningThreadTheme.PathConsidered),
            Template(ReasoningManualCaptureKind.AlternativeCompared, ReasoningEventFamily.Alternative, ReasoningEventType.AlternativeCompared, ReasoningThreadTheme.PathConsidered),
            Template(ReasoningManualCaptureKind.AlternativeRejected, ReasoningEventFamily.Alternative, ReasoningEventType.AlternativeRejected, ReasoningThreadTheme.PathConsidered),
            Template(ReasoningManualCaptureKind.AlternativeRevisited, ReasoningEventFamily.Alternative, ReasoningEventType.AlternativeRevisited, ReasoningThreadTheme.PathConsidered),
            Template(ReasoningManualCaptureKind.AlternativeSelected, ReasoningEventFamily.Alternative, ReasoningEventType.AlternativeSelected, ReasoningThreadTheme.PathConsidered),
            Template(ReasoningManualCaptureKind.ContradictionIdentified, ReasoningEventFamily.Contradiction, ReasoningEventType.ContradictionIdentified, ReasoningThreadTheme.Conflict),
            Template(ReasoningManualCaptureKind.ContradictionInvestigated, ReasoningEventFamily.Contradiction, ReasoningEventType.ContradictionInvestigated, ReasoningThreadTheme.Conflict),
            Template(ReasoningManualCaptureKind.ContradictionResolved, ReasoningEventFamily.Contradiction, ReasoningEventType.ContradictionResolved, ReasoningThreadTheme.Conflict),
            Template(ReasoningManualCaptureKind.ContradictionAccepted, ReasoningEventFamily.Contradiction, ReasoningEventType.ContradictionAccepted, ReasoningThreadTheme.Conflict),
            Template(ReasoningManualCaptureKind.ContradictionRecurred, ReasoningEventFamily.Contradiction, ReasoningEventType.ContradictionRecurred, ReasoningThreadTheme.Conflict),
            Template(ReasoningManualCaptureKind.DirectionObserved, ReasoningEventFamily.Direction, ReasoningEventType.DirectionObserved, ReasoningThreadTheme.StrategicMovement),
            Template(ReasoningManualCaptureKind.DirectionReinforced, ReasoningEventFamily.Direction, ReasoningEventType.DirectionReinforced, ReasoningThreadTheme.StrategicMovement),
            Template(ReasoningManualCaptureKind.DirectionShifted, ReasoningEventFamily.Direction, ReasoningEventType.DirectionShifted, ReasoningThreadTheme.StrategicMovement),
            Template(ReasoningManualCaptureKind.DirectionAbandoned, ReasoningEventFamily.Direction, ReasoningEventType.DirectionAbandoned, ReasoningThreadTheme.StrategicMovement),
            Template(ReasoningManualCaptureKind.AssumptionIntroduced, ReasoningEventFamily.AssumptionEvolution, ReasoningEventType.AssumptionIntroduced, ReasoningThreadTheme.AssumptionEvolution),
            Template(ReasoningManualCaptureKind.AssumptionChallenged, ReasoningEventFamily.AssumptionEvolution, ReasoningEventType.AssumptionChallenged, ReasoningThreadTheme.AssumptionEvolution),
            Template(ReasoningManualCaptureKind.AssumptionInvalidated, ReasoningEventFamily.AssumptionEvolution, ReasoningEventType.AssumptionInvalidated, ReasoningThreadTheme.AssumptionEvolution),
            Template(ReasoningManualCaptureKind.AssumptionReplaced, ReasoningEventFamily.AssumptionEvolution, ReasoningEventType.AssumptionReplaced, ReasoningThreadTheme.AssumptionEvolution),
            Template(ReasoningManualCaptureKind.ConstraintIntroduced, ReasoningEventFamily.ConstraintEvolution, ReasoningEventType.ConstraintIntroduced, ReasoningThreadTheme.ConstraintEvolution),
            Template(ReasoningManualCaptureKind.ConstraintModified, ReasoningEventFamily.ConstraintEvolution, ReasoningEventType.ConstraintModified, ReasoningThreadTheme.ConstraintEvolution),
            Template(ReasoningManualCaptureKind.ConstraintRetired, ReasoningEventFamily.ConstraintEvolution, ReasoningEventType.ConstraintRetired, ReasoningThreadTheme.ConstraintEvolution),
            Template(ReasoningManualCaptureKind.EvidenceAdded, ReasoningEventFamily.Evidence, ReasoningEventType.EvidenceAdded, ReasoningThreadTheme.EvidenceTrail)
        ];
    }

    private static ManualReasoningCaptureTemplate Decision(
        ReasoningManualCaptureKind kind,
        ReasoningEventType type)
    {
        return Template(
            kind,
            ReasoningEventFamily.DecisionEvolution,
            type,
            ReasoningThreadTheme.DecisionEvolution,
            [ReasoningReferenceKind.Decision, ReasoningReferenceKind.Proposal, ReasoningReferenceKind.Candidate]);
    }

    private static ManualReasoningCaptureTemplate Template(
        ReasoningManualCaptureKind kind,
        ReasoningEventFamily family,
        ReasoningEventType type,
        ReasoningThreadTheme suggestedThreadTheme,
        IReadOnlyList<ReasoningReferenceKind>? suggestedReferenceKinds = null)
    {
        return new ManualReasoningCaptureTemplate(
            kind,
            family,
            type,
            suggestedThreadTheme,
            UserSuppliedProvenanceSourceKind,
            suggestedReferenceKinds ?? [ReasoningReferenceKind.Artifact]);
    }
}
