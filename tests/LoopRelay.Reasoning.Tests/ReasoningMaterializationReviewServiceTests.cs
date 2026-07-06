using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Reasoning.Abstractions;
using LoopRelay.Reasoning.Models;
using LoopRelay.Reasoning.Projections;
using LoopRelay.Reasoning.Services;

namespace LoopRelay.Reasoning.Tests;

public sealed class ReasoningMaterializationReviewServiceTests
{
    [Fact]
    public async Task ReviewRecommendsRemainDerivedWhenReasoningIsReconstructable()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningMaterializationReviewService service = CreateService(repository, reasoningRepository);
        ReasoningEvent raised = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Hypothesis,
            ReasoningEventType.HypothesisRaised,
            "Hypothesis raised"));
        await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Hypothesis,
            ReasoningEventType.HypothesisSupported,
            "Hypothesis supported"));
        await reasoningRepository.CreateThreadAsync(repository, new CreateReasoningThreadCommand(
            "Hypothesis thread",
            ReasoningThreadTheme.BeliefUnderInvestigation,
            "Tracks a hypothesis as classification evidence.",
            [raised.Id],
            []));

        ReasoningMaterializationReviewReport report = await service.RunReviewAsync(
            repository.Id,
            new ReasoningMaterializationReviewRequest(
            [
                new ReasoningMaterializationScenario(
                    ReasoningMaterializationConcept.Hypothesis,
                    "Can the hypothesis be reconstructed?",
                    false,
                    "Events and thread membership provide the trace.")
            ]));

        ReasoningConceptMaterializationReview hypothesis = ReviewFor(report, ReasoningMaterializationConcept.Hypothesis);
        Assert.Equal(ReasoningMaterializationOutcome.RemainDerived, hypothesis.Recommendation);
        Assert.Equal(0, hypothesis.FailedScenarioCount);
        Assert.Equal(2, hypothesis.FailedScenarioThreshold);
        Assert.Equal(3, hypothesis.RepeatedWorkflowThreshold);
        Assert.Contains("No threshold was met", hypothesis.BranchReason, StringComparison.Ordinal);
        Assert.Empty(hypothesis.ElevatedRiskSignals);
        Assert.Contains("classification evidence", hypothesis.Summary, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "reasoning", "hypotheses")));
    }

    [Fact]
    public async Task ReviewDoesNotPromoteDirectionSolelyBecauseDirectionEventsExist()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningMaterializationReviewService service = CreateService(repository, reasoningRepository);
        await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Direction,
            ReasoningEventType.DirectionObserved,
            "Direction observed"));
        await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Direction,
            ReasoningEventType.DirectionReinforced,
            "Direction reinforced"));
        await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Direction,
            ReasoningEventType.DirectionShifted,
            "Direction shifted"));

        ReasoningMaterializationReviewReport report = await service.RunReviewAsync(repository.Id);

        ReasoningConceptMaterializationReview direction = ReviewFor(report, ReasoningMaterializationConcept.Direction);
        Assert.Equal(ReasoningMaterializationOutcome.RemainDerived, direction.Recommendation);
        Assert.Contains(direction.Risks, risk => risk.Contains("strategy authority", StringComparison.Ordinal));
        Assert.Contains(direction.ElevatedRiskSignals, risk => risk.Contains("strategic authority", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "reasoning", "directions")));
    }

    [Fact]
    public async Task ReviewFlagsPossibleMaterializationOnlyWithRepeatedFailureEvidence()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningMaterializationReviewService service = CreateService(repository, reasoningRepository);

        ReasoningMaterializationReviewReport report = await service.RunReviewAsync(
            repository.Id,
            new ReasoningMaterializationReviewRequest(
            [
                new ReasoningMaterializationScenario(
                    ReasoningMaterializationConcept.Alternative,
                    "Why was the alternative rejected?",
                    true,
                    "Fixture could not produce a trace from events and relationships."),
                new ReasoningMaterializationScenario(
                    ReasoningMaterializationConcept.Alternative,
                    "Which selected path replaced it?",
                    true,
                    "Repeated fixture also failed reconstruction.")
            ]));

        ReasoningConceptMaterializationReview alternative = ReviewFor(report, ReasoningMaterializationConcept.Alternative);
        ReasoningConceptMaterializationReview contradiction = ReviewFor(report, ReasoningMaterializationConcept.Contradiction);
        Assert.Equal(ReasoningMaterializationOutcome.AddReadModelReport, alternative.Recommendation);
        Assert.Equal(2, alternative.FailedScenarioCount);
        Assert.Equal(0, alternative.RepeatedWorkflowCount);
        Assert.Contains("met threshold 2", alternative.BranchReason, StringComparison.Ordinal);
        Assert.Contains(alternative.ElevatedRiskSignals, signal => signal.Contains("failed reconstruction", StringComparison.Ordinal));
        Assert.Contains(alternative.ElevatedRiskSignals, signal => signal.Contains("advisory", StringComparison.Ordinal));
        Assert.Equal(ReasoningMaterializationOutcome.RemainDerived, contradiction.Recommendation);
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "reasoning", "alternatives")));
    }

    [Fact]
    public async Task ReviewEvaluatesThreadsWithoutGrantingAuthority()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningMaterializationReviewService service = CreateService(repository, reasoningRepository);
        ReasoningEvent reasoningEvent = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Evidence,
            ReasoningEventType.EvidenceAdded,
            "Evidence added"));
        ReasoningThread thread = await reasoningRepository.CreateThreadAsync(repository, new CreateReasoningThreadCommand(
            "Evidence trail",
            ReasoningThreadTheme.EvidenceTrail,
            "Groups evidence.",
            [reasoningEvent.Id],
            []));
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.BelongsTo,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningThread, thread.Id),
            new ReasoningNarrative("Event belongs to the evidence trail."),
            Provenance()));

        ReasoningMaterializationReviewReport report = await service.RunReviewAsync(repository.Id);

        ReasoningConceptMaterializationReview threadReview = ReviewFor(report, ReasoningMaterializationConcept.Thread);
        Assert.Equal(ReasoningMaterializationOutcome.RemainDerived, threadReview.Recommendation);
        Assert.Contains("reviewable grouping mechanism", threadReview.Summary, StringComparison.Ordinal);
        Assert.Contains(threadReview.Risks, risk => risk.Contains("decisions, sessions, or current strategy", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReviewFlagsLifecycleLikeEventFamilyGrowth()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningMaterializationReviewService service = CreateService(repository, reasoningRepository);
        foreach (ReasoningEventType type in new[]
        {
            ReasoningEventType.ContradictionIdentified,
            ReasoningEventType.ContradictionInvestigated,
            ReasoningEventType.ContradictionResolved,
            ReasoningEventType.ContradictionAccepted,
            ReasoningEventType.ContradictionRecurred
        })
        {
            await reasoningRepository.CreateEventAsync(repository, EventCommand(
                ReasoningEventFamily.Contradiction,
                type,
                type.ToString()));
        }

        ReasoningMaterializationReviewReport report = await service.RunReviewAsync(repository.Id);

        ReasoningTaxonomyMaterializationFinding finding = Assert.Single(
            report.TaxonomyFindings,
            item => item.Family == ReasoningEventFamily.Contradiction);
        Assert.True(finding.LifecycleRisk);
        Assert.Equal(5, finding.EventTypeCount);
        Assert.Equal(4, finding.EventTypeThreshold);
        Assert.True(finding.TerminalEventTypePresent);
        Assert.Contains(ReasoningEventType.ContradictionResolved, finding.TerminalEventTypes);
        Assert.Contains("threshold 4", finding.RiskReason, StringComparison.Ordinal);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Contains("lifecycle-like growth", StringComparison.Ordinal));
        Assert.Contains(report.DiagnosticGroups, group => group.Category == "materialization" && group.Diagnostics.Any(diagnostic => diagnostic.Contains("Hypothesis", StringComparison.Ordinal)));
        Assert.Contains(report.DiagnosticGroups, group => group.Category == "lifecycle risk" && group.Diagnostics.Any(diagnostic => diagnostic.Contains("threshold 4", StringComparison.Ordinal)));
        Assert.Contains(report.DiagnosticGroups, group => group.Category == "authority boundary" && group.Diagnostics.Any(diagnostic => diagnostic.Contains("authority", StringComparison.OrdinalIgnoreCase)));
    }

    private static ReasoningConceptMaterializationReview ReviewFor(
        ReasoningMaterializationReviewReport report,
        ReasoningMaterializationConcept concept)
    {
        return Assert.Single(report.Concepts, review => review.Concept == concept);
    }

    private static IReasoningRepository CreateReasoningRepository(IArtifactStore store)
    {
        return new FileSystemReasoningRepository(store, new ReasoningArtifactProjectionService());
    }

    private static IReasoningMaterializationReviewService CreateService(
        Repository repository,
        IReasoningRepository reasoningRepository)
    {
        return new ReasoningMaterializationReviewService(new StubRepositoryService(repository), reasoningRepository);
    }

    private static CreateReasoningEventCommand EventCommand(
        ReasoningEventFamily family,
        ReasoningEventType type,
        string title)
    {
        return new CreateReasoningEventCommand(
            family,
            type,
            title,
            new ReasoningNarrative($"{title}."),
            [],
            Provenance(),
            [],
            []);
    }

    private static ReasoningProvenance Provenance()
    {
        return new ReasoningProvenance("ManualCapture", "agent");
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Repo",
            Path = path
        };
    }

    private sealed class StubRepositoryService(params Repository[] repositories) : IRepositoryService
    {
        public Task<IReadOnlyList<Repository>> GetAllAsync()
        {
            return Task.FromResult<IReadOnlyList<Repository>>(repositories);
        }

        public Task<Repository> RegisterAsync(string repositoryPath)
        {
            throw new NotSupportedException();
        }

        public Task RemoveAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }
    }
}
