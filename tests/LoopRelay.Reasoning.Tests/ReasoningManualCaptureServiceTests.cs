using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Reasoning.Abstractions;
using LoopRelay.Reasoning.Models;
using LoopRelay.Reasoning.Projections;
using LoopRelay.Reasoning.Services;

namespace LoopRelay.Reasoning.Tests;

[Collection("ProcessEnvironment")]
public sealed class ReasoningManualCaptureServiceTests
{
    [Fact]
    public void ManualCaptureTemplatesExposeUserSuppliedEventClassifications()
    {
        Repository repository = CreateRepository();
        Services services = CreateServices(repository);

        IReadOnlyList<ManualReasoningCaptureTemplate> templates = services.CaptureService.ListTemplates();

        Assert.Contains(templates, template =>
            template.Kind == ReasoningManualCaptureKind.AlternativeRejected &&
            template.Family == ReasoningEventFamily.Alternative &&
            template.Type == ReasoningEventType.AlternativeRejected &&
            template.ProvenanceSourceKind == "UserSupplied");
        Assert.Contains(templates, template =>
            template.Kind == ReasoningManualCaptureKind.ContradictionResolved &&
            template.SuggestedThreadTheme == ReasoningThreadTheme.Conflict);
    }

    [Fact]
    public async Task ManualCapturePreservesAlternativeRejectedAndRevisitedInEventThread()
    {
        Repository repository = CreateRepository();
        Services services = CreateServices(repository);
        ReasoningThread thread = await services.ThreadService.CreateThreadAsync(
            repository.Id,
            new CreateReasoningThreadCommand(
                "Alternative path",
                ReasoningThreadTheme.PathConsidered,
                "Tracks the alternative.",
                [],
                []));

        ReasoningEvent introduced = await ManualCaptureAsync(
            services.CaptureService,
            repository,
            ReasoningManualCaptureKind.AlternativeIntroduced,
            "Alternative introduced",
            [thread.Id]);
        ReasoningEvent rejected = await ManualCaptureAsync(
            services.CaptureService,
            repository,
            ReasoningManualCaptureKind.AlternativeRejected,
            "Alternative rejected",
            [thread.Id]);
        ReasoningEvent revisited = await ManualCaptureAsync(
            services.CaptureService,
            repository,
            ReasoningManualCaptureKind.AlternativeRevisited,
            "Alternative revisited",
            [thread.Id]);

        ReasoningThread reloadedThread = await services.ThreadService.GetThreadAsync(repository.Id, thread.Id);

        Assert.All([introduced, rejected, revisited], reasoningEvent =>
            Assert.Equal(ReasoningEventFamily.Alternative, reasoningEvent.Family));
        Assert.Equal(ReasoningEventType.AlternativeRejected, rejected.Type);
        Assert.Equal([introduced.Id, rejected.Id, revisited.Id], reloadedThread.EventIds);
    }

    [Fact]
    public async Task ManualCapturePreservesContradictionIdentifiedAndResolvedInEventThread()
    {
        Repository repository = CreateRepository();
        Services services = CreateServices(repository);
        ReasoningThread thread = await services.ThreadService.CreateThreadAsync(
            repository.Id,
            new CreateReasoningThreadCommand(
                "Contradiction",
                ReasoningThreadTheme.Conflict,
                "Tracks the contradiction.",
                [],
                []));

        ReasoningEvent identified = await ManualCaptureAsync(
            services.CaptureService,
            repository,
            ReasoningManualCaptureKind.ContradictionIdentified,
            "Contradiction identified",
            [thread.Id]);
        ReasoningEvent resolved = await ManualCaptureAsync(
            services.CaptureService,
            repository,
            ReasoningManualCaptureKind.ContradictionResolved,
            "Contradiction resolved",
            [thread.Id]);
        ReasoningThread reloadedThread = await services.ThreadService.GetThreadAsync(repository.Id, thread.Id);

        Assert.Equal(ReasoningEventFamily.Contradiction, identified.Family);
        Assert.Equal(ReasoningEventType.ContradictionResolved, resolved.Type);
        Assert.Equal("UserSupplied", resolved.Provenance.SourceKind);
        Assert.Equal(ReasoningCaptureMode.Manual, resolved.CaptureProvenance?.Mode);
        Assert.Equal("UserSupplied", resolved.CaptureProvenance?.SourceKind);
        Assert.Equal("Captured from explicit user-supplied reasoning.", resolved.CaptureProvenance?.CaptureReason);
        ReasoningDiagnosticGroup manualGroup = Assert.Single(resolved.CaptureProvenance?.DiagnosticGroups ?? []);
        Assert.Equal("capture", manualGroup.Category);
        Assert.Equal("Manual capture", manualGroup.Title);
        Assert.Contains("Capture mode: Manual.", manualGroup.Diagnostics);
        Assert.Contains("Captured by: agent.", manualGroup.Diagnostics);
        Assert.Equal([identified.Id, resolved.Id], reloadedThread.EventIds);
    }

    [Fact]
    public async Task ReasoningEventsExposeAssistedCaptureProvenance()
    {
        Repository repository = CreateRepository();
        Services services = CreateServices(repository);

        ReasoningEvent created = await services.EventService.CreateEventAsync(
            repository.Id,
            EventCommand(
                "Assisted event",
                new ReasoningProvenance(
                    "AssistedReviewCapture",
                    "review-workflow",
                    ".agents/handoffs/handoff.md",
                    "Review",
                    "Reviewer accepted the assisted explanation.",
                    "assisted-fingerprint"),
                ["assisted-capture"]));

        Assert.Equal(ReasoningCaptureMode.Assisted, created.CaptureProvenance?.Mode);
        Assert.Equal("AssistedReviewCapture", created.CaptureProvenance?.SourceKind);
        Assert.Equal("Reviewer accepted the assisted explanation.", created.CaptureProvenance?.CaptureReason);
        Assert.Equal(".agents/handoffs/handoff.md", created.CaptureProvenance?.SourceArtifact);
        Assert.Equal("Fingerprint assisted-fingerprint", created.CaptureProvenance?.DuplicateSignal);
        Assert.Null(created.CaptureProvenance?.SourceTransition);
        ReasoningDiagnosticGroup assistedGroup = Assert.Single(created.CaptureProvenance?.DiagnosticGroups ?? []);
        Assert.Equal("capture", assistedGroup.Category);
        Assert.Equal("Assisted capture", assistedGroup.Title);
        Assert.Contains("Capture mode: Assisted.", assistedGroup.Diagnostics);
        Assert.Contains("Source artifact: .agents/handoffs/handoff.md.", assistedGroup.Diagnostics);
        Assert.Contains("Duplicate signal: Fingerprint assisted-fingerprint.", assistedGroup.Diagnostics);
    }

    [Fact]
    public async Task ManualCaptureRecordsDirectionShiftWithoutMaterializedDirectionEntity()
    {
        Repository repository = CreateRepository();
        Services services = CreateServices(repository);

        ReasoningEvent reasoningEvent = await ManualCaptureAsync(
            services.CaptureService,
            repository,
            ReasoningManualCaptureKind.DirectionShifted,
            "Direction shifted",
            []);

        Assert.Equal(ReasoningEventFamily.Direction, reasoningEvent.Family);
        Assert.Equal(ReasoningEventType.DirectionShifted, reasoningEvent.Type);
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "reasoning", "directions")));
    }

    [Fact]
    public async Task ManualCaptureRejectsInferredProvenance()
    {
        Repository repository = CreateRepository();
        Services services = CreateServices(repository);

        await Assert.ThrowsAsync<ReasoningValidationException>(() => services.CaptureService.CaptureAsync(
            repository.Id,
            new ManualReasoningCaptureCommand(
                ReasoningManualCaptureKind.HypothesisRaised,
                "Hypothesis raised",
                new ReasoningNarrative("A hypothesis was raised."),
                [],
                new ReasoningProvenance("InferredDecisionSupersession", "agent"),
                [],
                [])));
    }

    private sealed record Services(
        IReasoningEventService EventService,
        IReasoningThreadService ThreadService,
        IReasoningManualCaptureService CaptureService);

    private static Services CreateServices(Repository repository)
    {
        IReasoningRepository reasoningRepository = new FileSystemReasoningRepository(
            new FileSystemArtifactStore(),
            new ReasoningArtifactProjectionService());
        IRepositoryService repositoryService = new StubRepositoryService(repository);
        IReasoningEventService eventService = new ReasoningEventService(repositoryService, reasoningRepository);
        IReasoningThreadService threadService = new ReasoningThreadService(repositoryService, reasoningRepository);
        IReasoningManualCaptureService captureService = new ReasoningManualCaptureService(eventService, threadService);
        return new Services(eventService, threadService, captureService);
    }

    private static CreateReasoningEventCommand EventCommand(
        string title,
        ReasoningProvenance? provenance = null,
        IReadOnlyList<string>? tags = null)
    {
        return new CreateReasoningEventCommand(
            ReasoningEventFamily.Hypothesis,
            ReasoningEventType.HypothesisRaised,
            title,
            new ReasoningNarrative("A hypothesis was raised."),
            [],
            provenance ?? Provenance(),
            [],
            tags ?? []);
    }

    private static ReasoningProvenance Provenance()
    {
        return new ReasoningProvenance("ManualCapture", "agent", ".agents/plan.md", "Milestone 1", "excerpt", "fingerprint");
    }

    private static async Task<ReasoningEvent> ManualCaptureAsync(
        IReasoningManualCaptureService captureService,
        Repository repository,
        ReasoningManualCaptureKind kind,
        string title,
        IReadOnlyList<string> threadIds)
    {
        return await captureService.CaptureAsync(
            repository.Id,
            new ManualReasoningCaptureCommand(
                kind,
                title,
                new ReasoningNarrative($"{title}."),
                [new ReasoningReference(ReasoningReferenceKind.Artifact, "manual-note", ".agents/plan.md")],
                new ReasoningProvenance("UserSupplied", "agent"),
                threadIds,
                ["manual-capture"]));
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
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
