using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Persistence;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Backend.Tests;

public sealed class ReasoningRepositoryTests
{
    [Fact]
    public async Task CreateEventAllocatesIdByScanningExistingArtifacts()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);

        await store.WriteAsync(
            ReasoningArtifactPaths.Resolve(repository, ".agents/reasoning/events/EVT-0042/event.json"),
            CreateDocument(repository, CreateEvent("EVT-0042", repository.Id)));

        ReasoningEvent created = await reasoningRepository.CreateEventAsync(repository, EventCommand());

        Assert.Equal("EVT-0043", created.Id);
    }

    [Fact]
    public async Task EventPersistenceRoundTripsThroughRepositoryFiles()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);

        ReasoningEvent created = await reasoningRepository.CreateEventAsync(repository, EventCommand(tags: ["capture"]));
        ReasoningEvent? loaded = await reasoningRepository.GetEventAsync(repository, created.Id);
        string? markdown = await store.ReadAsync(ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.EventMarkdown(created.Id)));

        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal(["capture"], loaded.Tags);
        Assert.Contains("Event Family: Hypothesis", markdown);
        Assert.Contains("Markdown projection is generated from event.json.", markdown);
    }

    [Fact]
    public async Task ExistingEventCannotBeMutatedByCreate()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);

        ReasoningEvent first = await reasoningRepository.CreateEventAsync(repository, EventCommand(title: "Original"));
        ReasoningEvent second = await reasoningRepository.CreateEventAsync(repository, EventCommand(title: "Correction"));
        ReasoningEvent? reloadedFirst = await reasoningRepository.GetEventAsync(repository, first.Id);

        Assert.Equal("EVT-0001", first.Id);
        Assert.Equal("EVT-0002", second.Id);
        Assert.Equal("Original", reloadedFirst?.Title);
    }

    [Fact]
    public async Task EventsRequireProvenance()
    {
        IReasoningRepository reasoningRepository = CreateReasoningRepository(new MemoryArtifactStore());
        Repository repository = CreateRepository();

        ReasoningValidationException exception = await Assert.ThrowsAsync<ReasoningValidationException>(() =>
            reasoningRepository.CreateEventAsync(repository, EventCommand(provenance: new ReasoningProvenance("", "agent"))));

        Assert.Contains("provenance source kind", exception.Message);
    }

    [Fact]
    public async Task RepositoryOwnershipIsEnforced()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        Repository otherRepository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);

        await store.WriteAsync(
            ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.EventJson("EVT-0001")),
            CreateDocument(otherRepository, CreateEvent("EVT-0001", otherRepository.Id)));

        ReasoningValidationException exception = await Assert.ThrowsAsync<ReasoningValidationException>(() =>
            reasoningRepository.GetEventAsync(repository, "EVT-0001"));

        Assert.Contains("different repository", exception.Message);
    }

    [Theory]
    [InlineData("../EVT-0001")]
    [InlineData("EVT-1")]
    [InlineData("THR-0001")]
    public void UnsafeIdsAreRejected(string id)
    {
        Assert.Throws<ArgumentException>(() => ReasoningArtifactPaths.EventJson(id));
    }

    [Fact]
    public async Task UnsupportedSchemaVersionsAreRejected()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);

        await store.WriteAsync(
            ReasoningArtifactPaths.Resolve(repository, ReasoningArtifactPaths.EventJson("EVT-0001")),
            CreateDocument(repository, CreateEvent("EVT-0001", repository.Id), schemaVersion: "999"));

        ReasoningValidationException exception = await Assert.ThrowsAsync<ReasoningValidationException>(() =>
            reasoningRepository.GetEventAsync(repository, "EVT-0001"));

        Assert.Contains("Unsupported reasoning schema version", exception.Message);
    }

    [Fact]
    public async Task ThreadPersistenceAndEventGroupingRoundTrip()
    {
        IReasoningRepository reasoningRepository = CreateReasoningRepository(new MemoryArtifactStore());
        Repository repository = CreateRepository();

        ReasoningEvent reasoningEvent = await reasoningRepository.CreateEventAsync(repository, EventCommand());
        ReasoningThread thread = await reasoningRepository.CreateThreadAsync(repository, new CreateReasoningThreadCommand(
            "Strategy emergence",
            ReasoningThreadTheme.StrategicMovement,
            "Tracks the strategy.",
            [reasoningEvent.Id],
            ["strategy"]));
        ReasoningEvent secondEvent = await reasoningRepository.CreateEventAsync(repository, EventCommand(title: "Second"));

        ReasoningThread updated = await reasoningRepository.AppendThreadEventAsync(repository, thread.Id, secondEvent.Id);
        ReasoningThread? loaded = await reasoningRepository.GetThreadAsync(repository, thread.Id);

        Assert.Equal("THR-0001", thread.Id);
        Assert.Equal([reasoningEvent.Id, secondEvent.Id], updated.EventIds);
        Assert.Equal(updated.EventIds, loaded?.EventIds);
    }

    [Fact]
    public async Task RelationshipPersistenceAndValidationRoundTrip()
    {
        IReasoningRepository reasoningRepository = CreateReasoningRepository(new MemoryArtifactStore());
        Repository repository = CreateRepository();
        ReasoningEvent source = await reasoningRepository.CreateEventAsync(repository, EventCommand(title: "Source"));
        ReasoningEvent target = await reasoningRepository.CreateEventAsync(repository, EventCommand(title: "Target"));

        ReasoningRelationship relationship = await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.Supports,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, source.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, target.Id),
            new ReasoningNarrative("Source supports target."),
            Provenance()));

        IReadOnlyList<ReasoningRelationship> relationships = await reasoningRepository.ListRelationshipsAsync(repository);

        Assert.Equal("REL-0001", relationship.Id);
        Assert.Single(relationships);
        Assert.Equal(source.Id, relationships[0].Source.Id);
    }

    [Fact]
    public async Task MissingReasoningRelationshipReferencesAreRejected()
    {
        IReasoningRepository reasoningRepository = CreateReasoningRepository(new MemoryArtifactStore());
        Repository repository = CreateRepository();
        ReasoningEvent target = await reasoningRepository.CreateEventAsync(repository, EventCommand(title: "Target"));

        ReasoningValidationException exception = await Assert.ThrowsAsync<ReasoningValidationException>(() =>
            reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
                ReasoningRelationshipType.Supports,
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, "EVT-9999"),
                new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, target.Id),
                new ReasoningNarrative("Missing source."),
                Provenance())));

        Assert.Contains("EVT-9999", exception.Message);
    }

    [Fact]
    public void MarkdownProjectionsAreDeterministic()
    {
        var projectionService = new ReasoningArtifactProjectionService();
        ReasoningEvent reasoningEvent = CreateEvent("EVT-0001", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

        string first = projectionService.RenderEvent(reasoningEvent);
        string second = projectionService.RenderEvent(reasoningEvent);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task SpecializedEntityDirectoriesAreNotCreatedForEventFamilies()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);

        await reasoningRepository.CreateEventAsync(repository, EventCommand(family: ReasoningEventFamily.Hypothesis));
        await reasoningRepository.CreateEventAsync(repository, EventCommand(family: ReasoningEventFamily.Alternative));
        await reasoningRepository.CreateEventAsync(repository, EventCommand(family: ReasoningEventFamily.Contradiction));
        await reasoningRepository.CreateEventAsync(repository, EventCommand(family: ReasoningEventFamily.Direction));

        Assert.False(await store.ExistsAsync(ReasoningArtifactPaths.Resolve(repository, ".agents/reasoning/hypotheses")));
        Assert.False(await store.ExistsAsync(ReasoningArtifactPaths.Resolve(repository, ".agents/reasoning/alternatives")));
        Assert.False(await store.ExistsAsync(ReasoningArtifactPaths.Resolve(repository, ".agents/reasoning/contradictions")));
        Assert.False(await store.ExistsAsync(ReasoningArtifactPaths.Resolve(repository, ".agents/reasoning/directions")));
    }

    private static IReasoningRepository CreateReasoningRepository(IArtifactStore store)
    {
        return new FileSystemReasoningRepository(store, new ReasoningArtifactProjectionService());
    }

    private static Repository CreateRepository()
    {
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Repo",
            Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        };
    }

    private static CreateReasoningEventCommand EventCommand(
        ReasoningEventFamily family = ReasoningEventFamily.Hypothesis,
        string title = "Reasoning event",
        ReasoningProvenance? provenance = null,
        IReadOnlyList<string>? tags = null)
    {
        return new CreateReasoningEventCommand(
            family,
            ReasoningEventType.HypothesisRaised,
            title,
            new ReasoningNarrative("A hypothesis was raised."),
            Array.Empty<ReasoningReference>(),
            provenance ?? Provenance(),
            Array.Empty<string>(),
            tags);
    }

    private static ReasoningEvent CreateEvent(string id, Guid repositoryId)
    {
        return new ReasoningEvent(
            id,
            repositoryId,
            DateTimeOffset.Parse("2026-06-22T00:00:00Z"),
            ReasoningEventFamily.Hypothesis,
            ReasoningEventType.HypothesisRaised,
            "Reasoning event",
            new ReasoningNarrative("A hypothesis was raised."),
            Array.Empty<ReasoningReference>(),
            Provenance(),
            Array.Empty<string>(),
            ["tag"]);
    }

    private static ReasoningProvenance Provenance()
    {
        return new ReasoningProvenance("ManualCapture", "agent", ".agents/plan.md", "Milestone 1", "excerpt", "fingerprint");
    }

    private static string CreateDocument<T>(Repository repository, T payload, string schemaVersion = ReasoningArtifactPaths.SchemaVersion)
    {
        return System.Text.Json.JsonSerializer.Serialize(
            new ReasoningArtifactDocument<T>(schemaVersion, repository.Id, DateTimeOffset.UtcNow, null, payload),
            ReasoningJson.Options);
    }
}
