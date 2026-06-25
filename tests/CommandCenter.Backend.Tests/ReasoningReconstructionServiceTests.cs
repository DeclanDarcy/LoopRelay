using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Persistence;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Backend.Tests;

public sealed class ReasoningReconstructionServiceTests
{
    [Fact]
    public async Task DecisionSupersededQueryReconstructsCitedTrace()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        IReasoningReconstructionService reconstructionService = CreateReconstructionService(repository, reasoningRepository, graphService);
        ReasoningEvent supersession = await reasoningRepository.CreateEventAsync(repository, new CreateReasoningEventCommand(
            ReasoningEventFamily.DecisionEvolution,
            ReasoningEventType.DecisionSuperseded,
            "Decision replaced by event substrate",
            new ReasoningNarrative("The earlier decision was superseded because event-led reconstruction kept authority narrower."),
            [new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-0001")],
            Provenance(),
            [],
            []));
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.CausedBy,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, supersession.Id),
            new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-0001"),
            new ReasoningNarrative("The supersession event explains why DEC-0001 changed."),
            Provenance()));

        ReasoningReconstruction reconstruction = await reconstructionService.ReconstructAsync(
            repository.Id,
            new ReasoningQuery(
                ReasoningQueryCategory.Decision,
                "Why was this decision superseded?",
                new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-0001")));

        Assert.Contains(supersession.Id, reconstruction.Narrative.Details, StringComparison.Ordinal);
        Assert.Contains(reconstruction.Evidence, evidence => evidence.Kind == "Event" && evidence.Id == supersession.Id);
        Assert.Contains(reconstruction.Evidence, evidence => evidence.Kind == "Relationship" && evidence.Title == "CausedBy");
        Assert.Contains(reconstruction.Trace.Relationships, relationship => relationship.Type == ReasoningRelationshipType.CausedBy);
        Assert.Equal("High", reconstruction.Confidence);
        Assert.Equal("High", reconstruction.ConfidenceRationale.Level);
        Assert.True(reconstruction.ConfidenceRationale.EventEvidencePresent);
        Assert.True(reconstruction.ConfidenceRationale.RelationshipEvidencePresent);
        Assert.False(reconstruction.ConfidenceRationale.TraceDiagnosticsPresent);
        Assert.Empty(reconstruction.ConfidenceRationale.MissingEvidence);
        Assert.Equal(ReasoningTraceDirection.Backward, reconstruction.Scope.Direction);
        Assert.Equal("DEC-0001", reconstruction.Scope.Target.Id);
        Assert.Equal(supersession.Id, reconstruction.Scope.Source?.Id);
        Assert.Empty(reconstruction.Scope.UnreachableEvidence);
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "reasoning", "hypotheses")));
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "reasoning", "alternatives")));
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "reasoning", "contradictions")));
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "reasoning", "directions")));
    }

    [Fact]
    public async Task SameQueryOverUnchangedRepositoryReturnsSamePathAndEvidence()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        IReasoningQueryService queryService = new ReasoningQueryService(CreateReconstructionService(repository, reasoningRepository, graphService));
        ReasoningEvent first = await reasoningRepository.CreateEventAsync(repository, EventCommand("Alternative introduced", ReasoningEventType.AlternativeIntroduced));
        ReasoningEvent second = await reasoningRepository.CreateEventAsync(repository, EventCommand("Alternative rejected", ReasoningEventType.AlternativeRejected));
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.Invalidates,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, second.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, first.Id),
            new ReasoningNarrative("The rejection invalidates the introduced alternative."),
            Provenance()));
        var query = new ReasoningQuery(
            ReasoningQueryCategory.Alternative,
            "What alternatives were rejected?",
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, first.Id));

        ReasoningQueryResult firstResult = await queryService.RunQueryAsync(repository.Id, query);
        ReasoningQueryResult secondResult = await queryService.RunQueryAsync(repository.Id, query);

        Assert.Equal(
            firstResult.Reconstruction.Trace.Nodes.Select(node => node.Id).ToArray(),
            secondResult.Reconstruction.Trace.Nodes.Select(node => node.Id).ToArray());
        Assert.Equal(
            firstResult.Reconstruction.Trace.Relationships.Select(relationship => relationship.Id).ToArray(),
            secondResult.Reconstruction.Trace.Relationships.Select(relationship => relationship.Id).ToArray());
        Assert.Equal(
            firstResult.Reconstruction.Evidence.Select(evidence => $"{evidence.Kind}:{evidence.Id}").ToArray(),
            secondResult.Reconstruction.Evidence.Select(evidence => $"{evidence.Kind}:{evidence.Id}").ToArray());
    }

    [Fact]
    public async Task ReconstructionConfidenceRationaleExplainsMissingEvidence()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        IReasoningReconstructionService reconstructionService = CreateReconstructionService(repository, reasoningRepository, graphService);
        var artifactReference = new ReasoningReference(ReasoningReferenceKind.Artifact, "docs/design.md", "docs/design.md");
        await reasoningRepository.CreateEventAsync(repository, new CreateReasoningEventCommand(
            ReasoningEventFamily.Evidence,
            ReasoningEventType.EvidenceAdded,
            "Design evidence referenced",
            new ReasoningNarrative("The artifact is referenced but does not itself cite event or relationship evidence."),
            [artifactReference],
            Provenance(),
            [],
            []));

        ReasoningReconstruction reconstruction = await reconstructionService.ReconstructAsync(
            repository.Id,
            new ReasoningQuery(
                ReasoningQueryCategory.Decision,
                "What explains this artifact by itself?",
                artifactReference));

        Assert.Equal("Low", reconstruction.Confidence);
        Assert.False(reconstruction.ConfidenceRationale.EventEvidencePresent);
        Assert.False(reconstruction.ConfidenceRationale.RelationshipEvidencePresent);
        Assert.Contains("No event evidence was reachable for the requested trace.", reconstruction.ConfidenceRationale.MissingEvidence);
        Assert.Contains("No relationship evidence was reachable for the requested trace.", reconstruction.ConfidenceRationale.MissingEvidence);
        Assert.Contains("High confidence requires at least one reachable reasoning event.", reconstruction.ConfidenceRationale.WhyNotHigher);
        Assert.Contains("High confidence requires at least one reachable reasoning relationship.", reconstruction.ConfidenceRationale.WhyNotHigher);
        Assert.Contains(reconstruction.Scope.ReachableEvidence, evidence => evidence.Kind == "Reference" && evidence.Id == artifactReference.Id);
    }

    [Fact]
    public async Task ReconstructionScopePreservesForwardDirectionAndSourceReference()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        IReasoningReconstructionService reconstructionService = CreateReconstructionService(repository, reasoningRepository, graphService);
        ReasoningEvent source = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Decision path changed",
            ReasoningEventType.DecisionReframed,
            ReasoningEventFamily.DecisionEvolution));
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.LeadsTo,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, source.Id),
            new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-0002"),
            new ReasoningNarrative("The event leads to the reframed decision."),
            Provenance()));

        ReasoningReconstruction reconstruction = await reconstructionService.ReconstructAsync(
            repository.Id,
            new ReasoningQuery(
                ReasoningQueryCategory.Decision,
                "What changed after this event?",
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, source.Id),
                ReasoningTraceDirection.Forward));

        Assert.Equal(ReasoningTraceDirection.Forward, reconstruction.Scope.Direction);
        Assert.Equal(ReasoningReferenceKind.ReasoningEvent, reconstruction.Scope.Target.Kind);
        Assert.Equal(source.Id, reconstruction.Scope.Target.Id);
        Assert.Equal(ReasoningReferenceKind.Decision, reconstruction.Scope.Source?.Kind);
        Assert.Equal("DEC-0002", reconstruction.Scope.Source?.Id);
        Assert.Contains(reconstruction.Scope.ReachableEvidence, evidence => evidence.Kind == "Relationship" && evidence.Title == "LeadsTo");
    }

    [Fact]
    public async Task HypothesisFailureQueryReconstructsContradictingEvidence()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        IReasoningReconstructionService reconstructionService = CreateReconstructionService(repository, reasoningRepository, graphService);
        ReasoningEvent hypothesis = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Cache the derived graph",
            ReasoningEventType.HypothesisRaised,
            ReasoningEventFamily.Hypothesis));
        ReasoningEvent contradiction = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Graph cache became authority",
            ReasoningEventType.EvidenceAdded,
            ReasoningEventFamily.Evidence));
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.Contradicts,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, contradiction.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, hypothesis.Id),
            new ReasoningNarrative("The evidence contradicted the hypothesis that graph persistence was harmless."),
            Provenance()));

        ReasoningReconstruction reconstruction = await reconstructionService.ReconstructAsync(
            repository.Id,
            new ReasoningQuery(
                ReasoningQueryCategory.Hypothesis,
                "What killed this hypothesis?",
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, hypothesis.Id)));

        Assert.Contains(reconstruction.Evidence, evidence => evidence.Kind == "Event" && evidence.Id == contradiction.Id);
        Assert.Contains(reconstruction.Evidence, evidence => evidence.Kind == "Relationship" && evidence.Title == "Contradicts");
        Assert.Contains("Graph cache became authority", reconstruction.Narrative.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HistoricalQueriesReconstructPointInTimeDerivedStateFromEventTimelines()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        IReasoningReconstructionService reconstructionService = CreateReconstructionService(repository, reasoningRepository, graphService);
        ReasoningEvent hypothesis = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Event substrate is enough",
            ReasoningEventType.HypothesisRaised,
            ReasoningEventFamily.Hypothesis));
        ReasoningEvent alternative = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Use specialized alternative records",
            ReasoningEventType.AlternativeIntroduced,
            ReasoningEventFamily.Alternative));
        ReasoningEvent contradiction = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Decision authority conflict",
            ReasoningEventType.ContradictionIdentified,
            ReasoningEventFamily.Contradiction));
        ReasoningEvent direction = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Keep direction derived",
            ReasoningEventType.DirectionObserved,
            ReasoningEventFamily.Direction));
        DateTimeOffset historicalAt = DateTimeOffset.UtcNow.AddSeconds(1);
        await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Persisted direction abandoned",
            ReasoningEventType.DirectionAbandoned,
            ReasoningEventFamily.Direction));

        await AssertHistoricalEvidenceAsync(reconstructionService, repository, ReasoningQueryCategory.Hypothesis, hypothesis);
        await AssertHistoricalEvidenceAsync(reconstructionService, repository, ReasoningQueryCategory.Alternative, alternative);
        await AssertHistoricalEvidenceAsync(reconstructionService, repository, ReasoningQueryCategory.Contradiction, contradiction);
        await AssertHistoricalEvidenceAsync(reconstructionService, repository, ReasoningQueryCategory.Direction, direction);

        async Task AssertHistoricalEvidenceAsync(
            IReasoningReconstructionService service,
            Repository targetRepository,
            ReasoningQueryCategory category,
            ReasoningEvent expectedEvent)
        {
            ReasoningReconstruction reconstruction = await service.ReconstructAsync(
                targetRepository.Id,
                new ReasoningQuery(
                    category,
                    $"What {category.ToString().ToLowerInvariant()} events were visible?",
                    new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, expectedEvent.Id),
                    HistoricalAt: historicalAt));

            Assert.Contains(reconstruction.Evidence, evidence => evidence.Kind == "Event" && evidence.Id == expectedEvent.Id);
            Assert.Contains("Historical state is derived from event timelines", reconstruction.Narrative.Details, StringComparison.Ordinal);
            Assert.Contains(reconstruction.Diagnostics, diagnostic => diagnostic.Contains("Historical reconstruction used events visible", StringComparison.Ordinal));
            Assert.Equal(historicalAt, reconstruction.Scope.HistoricalCutoff);
            Assert.Equal(ReasoningTraceDirection.Backward, reconstruction.Scope.Direction);
        }
    }

    [Fact]
    public async Task HistoricalScopeReportsFutureEvidenceAsUnreachable()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        IReasoningReconstructionService reconstructionService = CreateReconstructionService(repository, reasoningRepository, graphService);
        ReasoningEvent future = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Future decision event",
            ReasoningEventType.DecisionReframed,
            ReasoningEventFamily.DecisionEvolution));
        DateTimeOffset historicalAt = DateTimeOffset.UtcNow.AddDays(-1);

        ReasoningReconstruction reconstruction = await reconstructionService.ReconstructAsync(
            repository.Id,
            new ReasoningQuery(
                ReasoningQueryCategory.Decision,
                "What decision evidence was available yesterday?",
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, future.Id),
                HistoricalAt: historicalAt));

        Assert.Equal(historicalAt, reconstruction.Scope.HistoricalCutoff);
        Assert.DoesNotContain(reconstruction.Scope.ReachableEvidence, evidence => evidence.Kind == "Event" && evidence.Id == future.Id);
        Assert.Contains(reconstruction.Scope.UnreachableEvidence, evidence => evidence.Kind == "Event" && evidence.Id == future.Id);
    }

    [Fact]
    public async Task ReconstructionReportsPersistOnlyWhenExplicitlyRun()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        IReasoningReconstructionService reconstructionService = CreateReconstructionService(repository, reasoningRepository, graphService);
        ReasoningEvent reasoningEvent = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Direction shifted",
            ReasoningEventType.DirectionShifted,
            ReasoningEventFamily.Direction));
        var query = new ReasoningQuery(
            ReasoningQueryCategory.Direction,
            "Why does the current strategy exist?",
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, reasoningEvent.Id));

        _ = await reconstructionService.ReconstructAsync(repository.Id, query);
        Assert.Empty(await reconstructionService.ListReportsAsync(repository.Id));

        ReasoningReconstructionReport report = await reconstructionService.RunReconstructionAsync(repository.Id, query);
        IReadOnlyList<ReasoningReconstructionReport> reports = await reconstructionService.ListReportsAsync(repository.Id);

        Assert.Single(reports);
        Assert.Equal(report.Id, reports.Single().Id);
        Assert.True(await store.ExistsAsync(ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.ReconstructionReportJson(report.Id))));
        Assert.True(await store.ExistsAsync(ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.ReconstructionReportMarkdown(report.Id))));
    }

    private static IReasoningRepository CreateReasoningRepository(IArtifactStore store)
    {
        return new FileSystemReasoningRepository(store, new ReasoningArtifactProjectionService());
    }

    private static IReasoningGraphService CreateGraphService(
        Repository repository,
        IReasoningRepository reasoningRepository,
        IArtifactStore store)
    {
        return new ReasoningGraphService(new StubRepositoryService(repository), reasoningRepository, store);
    }

    private static IReasoningReconstructionService CreateReconstructionService(
        Repository repository,
        IReasoningRepository reasoningRepository,
        IReasoningGraphService graphService)
    {
        return new ReasoningReconstructionService(new StubRepositoryService(repository), reasoningRepository, graphService);
    }

    private static CreateReasoningEventCommand EventCommand(
        string title,
        ReasoningEventType type,
        ReasoningEventFamily family = ReasoningEventFamily.Alternative)
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
        string path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
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
