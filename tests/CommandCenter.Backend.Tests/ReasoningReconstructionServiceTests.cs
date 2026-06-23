using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
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

    private static CreateReasoningEventCommand EventCommand(string title, ReasoningEventType type)
    {
        return new CreateReasoningEventCommand(
            ReasoningEventFamily.Alternative,
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
