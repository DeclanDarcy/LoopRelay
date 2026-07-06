using System.Text.Json;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Reasoning.Abstractions;
using LoopRelay.Reasoning.Models;
using LoopRelay.Reasoning.Persistence;
using LoopRelay.Reasoning.Projections;
using LoopRelay.Reasoning.Services;

namespace LoopRelay.Reasoning.Tests;

[Collection("ProcessEnvironment")]
public sealed class ReasoningCertificationServiceTests
{
    [Fact]
    public async Task CertificationPassesForEmptyRepositoryAsValidBaseline()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        ReasoningServices services = CreateServices(repository, store);

        ReasoningCertificationReport report = await services.Certification.GetCurrentCertificationAsync(repository.Id);

        Assert.Equal("certification.current", report.Id);
        Assert.Equal(ReasoningCertificationResultKind.Passed, report.Result.Kind);
        Assert.Contains(report.Evidence, evidence =>
            evidence.Id == "CERT-000" &&
            evidence.Passed &&
            evidence.Scenario == "No reasoning captured");
        Assert.Empty(await services.Repository.ListCertificationReportsAsync(repository));
    }

    [Fact]
    public async Task CertificationPassesForAnswerableOutcomeScenariosAndPersistsReport()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        ReasoningServices services = CreateServices(repository, store);
        await CreateOutcomeFixtureAsync(repository, services.Repository);

        ReasoningCertificationReport current = await services.Certification.GetCurrentCertificationAsync(repository.Id);
        ReasoningCertificationReport persisted = await services.Certification.RunCertificationAsync(repository.Id);
        IReadOnlyList<ReasoningCertificationReport> reports = await services.Certification.ListReportsAsync(repository.Id);

        Assert.Equal(ReasoningCertificationResultKind.Passed, current.Result.Kind);
        Assert.Equal(ReasoningCertificationResultKind.Passed, persisted.Result.Kind);
        Assert.StartsWith("certification.", persisted.Id, StringComparison.Ordinal);
        Assert.DoesNotContain(persisted.Evidence, evidence => !evidence.Passed);
        Assert.Contains(persisted.Evidence, evidence => evidence.Id == "CERT-100" && evidence.Passed);
        Assert.Contains(persisted.Evidence, evidence => evidence.Id == "CERT-110" && evidence.Passed);
        Assert.Contains(persisted.Evidence, evidence => evidence.Id == "CERT-120" && evidence.Passed);
        Assert.Contains(persisted.Evidence, evidence => evidence.Id == "CERT-130" && evidence.Passed);
        Assert.Contains(persisted.Evidence, evidence => evidence.Id == "CERT-140" && evidence.Passed);
        Assert.Contains(persisted.Evidence, evidence => evidence.Id == "CERT-150" && evidence.Passed);
        Assert.Single(reports);
        Assert.True(await store.ExistsAsync(Path.Combine(repository.Path, ".agents", "reasoning", "reports", $"{persisted.Id}.json")));
        Assert.True(await store.ExistsAsync(Path.Combine(repository.Path, ".agents", "reasoning", "reports", $"{persisted.Id}.md")));
    }

    [Fact]
    public async Task CertificationFailsWhenEventLacksProvenance()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        ReasoningServices services = CreateServices(repository, store);
        ReasoningEvent reasoningEvent = await services.Repository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Hypothesis,
            ReasoningEventType.HypothesisRaised,
            "Incomplete provenance candidate",
            "The event will be rewritten with incomplete provenance."));
        ReasoningEvent corrupted = reasoningEvent with
        {
            Provenance = new ReasoningProvenance("", "")
        };
        var document = new ReasoningArtifactDocument<ReasoningEvent>(
            ReasoningArtifactPaths.SchemaVersion,
            repository.Id,
            corrupted.CreatedAt,
            null,
            corrupted);
        await store.WriteAsync(
            ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.EventJson(corrupted.Id)),
            JsonSerializer.Serialize(document, ReasoningJson.Options));

        ReasoningCertificationReport report = await services.Certification.GetCurrentCertificationAsync(repository.Id);

        Assert.Equal(ReasoningCertificationResultKind.Failed, report.Result.Kind);
        ReasoningCertificationEvidence provenance = Assert.Single(report.Evidence, evidence => evidence.Id == "CERT-010");
        Assert.False(provenance.Passed);
        Assert.Contains(corrupted.Id, string.Join(" ", provenance.Details), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CertificationFailsWhenPersistedRelationshipPointsToMissingReasoningNode()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        ReasoningServices services = CreateServices(repository, store);
        ReasoningEvent source = await services.Repository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Evidence,
            ReasoningEventType.EvidenceAdded,
            "Source evidence",
            "Source evidence exists."));
        ReasoningEvent target = await services.Repository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Evidence,
            ReasoningEventType.EvidenceAdded,
            "Target evidence",
            "Target evidence exists."));
        ReasoningRelationship relationship = await services.Repository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.Supports,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, source.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, target.Id),
            new ReasoningNarrative("Source supports target."),
            Provenance()));
        ReasoningRelationship corrupted = relationship with
        {
            Target = new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, "EVT-9999")
        };
        var document = new ReasoningArtifactDocument<ReasoningRelationship>(
            ReasoningArtifactPaths.SchemaVersion,
            repository.Id,
            corrupted.CreatedAt,
            null,
            corrupted);
        await store.WriteAsync(
            ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.RelationshipJson(corrupted.Id)),
            JsonSerializer.Serialize(document, ReasoningJson.Options));

        ReasoningCertificationReport report = await services.Certification.GetCurrentCertificationAsync(repository.Id);

        Assert.Equal(ReasoningCertificationResultKind.Failed, report.Result.Kind);
        ReasoningCertificationEvidence integrity = Assert.Single(report.Evidence, evidence => evidence.Id == "CERT-020");
        Assert.False(integrity.Passed);
        Assert.Contains("EVT-9999", string.Join(" ", integrity.Details), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CertificationReportsUnresolvedExternalReferencesAsDiagnostics()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        ReasoningServices services = CreateServices(repository, store);
        await services.Repository.CreateEventAsync(repository, new CreateReasoningEventCommand(
            ReasoningEventFamily.Evidence,
            ReasoningEventType.EvidenceAdded,
            "External evidence",
            new ReasoningNarrative("External artifact reference is missing."),
            [new ReasoningReference(ReasoningReferenceKind.Artifact, "missing-note", ".agents/missing.md")],
            Provenance(),
            [],
            []));

        ReasoningCertificationReport report = await services.Certification.GetCurrentCertificationAsync(repository.Id);

        Assert.Equal(ReasoningCertificationResultKind.Passed, report.Result.Kind);
        Assert.Contains(report.Diagnostics, diagnostic =>
            diagnostic.Contains("unresolved Artifact missing-note", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationRebuildsFromStructuredArtifactsWhenMarkdownIsMissing()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        ReasoningServices services = CreateServices(repository, store);
        FixtureIds fixture = await CreateOutcomeFixtureAsync(repository, services.Repository);
        await store.DeleteAsync(ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.EventMarkdown(fixture.DirectionShiftId)));
        await store.DeleteAsync(ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.ThreadMarkdown(fixture.ThreadId)));
        await store.DeleteAsync(ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.RelationshipMarkdown(fixture.DecisionRelationshipId)));

        ReasoningCertificationReport report = await services.Certification.GetCurrentCertificationAsync(repository.Id);

        Assert.Equal(ReasoningCertificationResultKind.Passed, report.Result.Kind);
        Assert.Contains(report.Evidence, evidence =>
            evidence.Id == "CERT-000" &&
            evidence.Summary.Contains("structured JSON", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CertificationSurvivesFreshServiceGraphReload()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        ReasoningServices initial = CreateServices(repository, store);
        await CreateOutcomeFixtureAsync(repository, initial.Repository);
        ReasoningCertificationReport before = await initial.Certification.GetCurrentCertificationAsync(repository.Id);

        ReasoningServices restarted = CreateServices(repository, store);
        ReasoningCertificationReport after = await restarted.Certification.GetCurrentCertificationAsync(repository.Id);

        Assert.Equal(ReasoningCertificationResultKind.Passed, after.Result.Kind);
        Assert.Equal(
            before.Evidence.Select(EvidenceSignature).Order(StringComparer.Ordinal),
            after.Evidence.Select(EvidenceSignature).Order(StringComparer.Ordinal));
    }

    private static async Task<FixtureIds> CreateOutcomeFixtureAsync(
        Repository repository,
        IReasoningRepository reasoningRepository)
    {
        ReasoningEvent rejectedAlternative = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Alternative,
            ReasoningEventType.AlternativeRejected,
            "Rejected provider-session continuity",
            "Provider-session reuse was rejected because repository artifacts must remain the continuity source."));
        ReasoningEvent selectedAlternative = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Alternative,
            ReasoningEventType.AlternativeSelected,
            "Selected repository event substrate",
            "The event-led repository substrate was selected because it preserves rationale without creating authority."));
        ReasoningEvent hypothesis = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Hypothesis,
            ReasoningEventType.HypothesisSupported,
            "Repository truth supports reconstruction",
            "Persisted events, threads, relationships, references, and provenance are enough to rebuild answers."));
        ReasoningEvent invalidatedAssumption = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.AssumptionEvolution,
            ReasoningEventType.AssumptionInvalidated,
            "Manual capture is not enough",
            "Manual-only capture was invalidated because objective domain transitions can be inferred."));
        ReasoningEvent contradiction = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Contradiction,
            ReasoningEventType.ContradictionRecurred,
            "Derived read models imply authority",
            "Repeated materialization pressure conflicted with the requirement that reasoning remain explanatory."));
        ReasoningEvent directionShift = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Direction,
            ReasoningEventType.DirectionShifted,
            "Shift toward repository recovery certification",
            "The strategy shifted toward proving equivalent answers after restart instead of adding caches."));
        ReasoningEvent decisionSuperseded = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.DecisionEvolution,
            ReasoningEventType.DecisionSuperseded,
            "Current strategy supersedes session continuity",
            "The current strategy superseded session continuity because recovery must start from repository truth."));
        ReasoningThread thread = await reasoningRepository.CreateThreadAsync(repository, new CreateReasoningThreadCommand(
            "Repository recovery strategy",
            ReasoningThreadTheme.StrategicMovement,
            "Tracks why the strategy moved toward repository-backed reasoning recovery.",
            [
                rejectedAlternative.Id,
                selectedAlternative.Id,
                hypothesis.Id,
                invalidatedAssumption.Id,
                contradiction.Id,
                directionShift.Id,
                decisionSuperseded.Id
            ],
            ["strategy"]));

        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.ComparesWith, rejectedAlternative, selectedAlternative);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.Supports, hypothesis, selectedAlternative);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.Challenges, contradiction, invalidatedAssumption);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.Invalidates, invalidatedAssumption, hypothesis);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.LeadsTo, selectedAlternative, directionShift);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.LeadsTo, contradiction, directionShift);
        await RelateAsync(reasoningRepository, repository, ReasoningRelationshipType.CausedBy, directionShift, decisionSuperseded);
        ReasoningRelationship decisionRelationship = await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.CausedBy,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, decisionSuperseded.Id),
            new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-STRATEGY-CURRENT"),
            new ReasoningNarrative("The supersession event explains the current strategy decision."),
            Provenance()));
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.BelongsTo,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, directionShift.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningThread, thread.Id),
            new ReasoningNarrative("The direction shift belongs to the long-horizon strategy thread."),
            Provenance()));

        return new FixtureIds(directionShift.Id, thread.Id, decisionRelationship.Id);
    }

    private static async Task RelateAsync(
        IReasoningRepository reasoningRepository,
        Repository repository,
        ReasoningRelationshipType type,
        ReasoningEvent source,
        ReasoningEvent target)
    {
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            type,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, source.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, target.Id),
            new ReasoningNarrative($"{source.Title} {type.ToString().ToLowerInvariant()} {target.Title}."),
            Provenance()));
    }

    private static CreateReasoningEventCommand EventCommand(
        ReasoningEventFamily family,
        ReasoningEventType type,
        string title,
        string summary)
    {
        return new CreateReasoningEventCommand(
            family,
            type,
            title,
            new ReasoningNarrative(summary),
            [],
            Provenance(),
            [],
            []);
    }

    private static ReasoningProvenance Provenance()
    {
        return new ReasoningProvenance("ManualCapture", "agent", ".agents/plan.md", "Milestone 8", "certification", "m8-fixture");
    }

    private static string EvidenceSignature(ReasoningCertificationEvidence evidence)
    {
        return $"{evidence.Id}:{evidence.Scenario}:{evidence.Passed}:{evidence.Summary}:{string.Join("|", evidence.Details)}";
    }

    private static ReasoningServices CreateServices(Repository repository, IArtifactStore store)
    {
        IReasoningRepository reasoningRepository = new FileSystemReasoningRepository(
            store,
            new ReasoningArtifactProjectionService());
        var repositoryService = new StubRepositoryService(repository);
        IReasoningGraphService graphService = new ReasoningGraphService(
            repositoryService,
            reasoningRepository,
            store);
        IReasoningReconstructionService reconstructionService = new ReasoningReconstructionService(
            repositoryService,
            reasoningRepository,
            graphService);
        IReasoningQueryService queryService = new ReasoningQueryService(reconstructionService);
        return new ReasoningServices(
            reasoningRepository,
            new ReasoningCertificationService(repositoryService, reasoningRepository, graphService, queryService));
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

    private sealed record FixtureIds(
        string DirectionShiftId,
        string ThreadId,
        string DecisionRelationshipId);

    private sealed record ReasoningServices(
        IReasoningRepository Repository,
        IReasoningCertificationService Certification);

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
