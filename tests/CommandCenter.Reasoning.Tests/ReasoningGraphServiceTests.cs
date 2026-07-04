using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Reasoning.Tests;

public sealed class ReasoningGraphServiceTests
{
    [Fact]
    public async Task GraphBuildsNodesAndRelationshipsFromEventsThreadsAndRelationships()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);

        ReasoningEvent source = await reasoningRepository.CreateEventAsync(repository, EventCommand("Source"));
        ReasoningEvent target = await reasoningRepository.CreateEventAsync(repository, EventCommand("Target"));
        ReasoningThread thread = await reasoningRepository.CreateThreadAsync(repository, ThreadCommand([source.Id, target.Id]));
        await reasoningRepository.CreateRelationshipAsync(repository, RelationshipCommand(source.Id, target.Id));

        ReasoningGraph graph = await graphService.GetGraphAsync(repository.Id);

        Assert.Contains(graph.Nodes, node => node.Id == $"ReasoningEvent:{source.Id}" && node.Resolved);
        Assert.Contains(graph.Nodes, node => node.Id == $"ReasoningThread:{thread.Id}" && node.Resolved);
        Assert.Contains(graph.Relationships, relationship =>
            relationship.Type == ReasoningRelationshipType.Supports &&
            relationship.SourceNodeId == $"ReasoningEvent:{source.Id}" &&
            relationship.TargetNodeId == $"ReasoningEvent:{target.Id}");
        Assert.Contains(graph.Relationships, relationship =>
            relationship.Type == ReasoningRelationshipType.BelongsTo &&
            relationship.SourceNodeId == $"ReasoningEvent:{target.Id}" &&
            relationship.TargetNodeId == $"ReasoningThread:{thread.Id}");
    }

    [Fact]
    public async Task GraphReportsMissingExternalArtifactReferenceDiagnostics()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);

        await reasoningRepository.CreateEventAsync(repository, EventCommand(
            "Uses missing artifact",
            [new ReasoningReference(ReasoningReferenceKind.Artifact, "missing", ".agents/missing.md")]));

        ReasoningGraph graph = await graphService.GetGraphAsync(repository.Id);

        Assert.Contains(graph.Nodes, node =>
            node.Id == "Artifact:missing" &&
            node.Resolved == false);
        Assert.Contains(graph.Diagnostics, diagnostic =>
            diagnostic.Contains("unresolved Artifact missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BackwardAndForwardTraceFollowCausalRelationships()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        ReasoningEvent source = await reasoningRepository.CreateEventAsync(repository, EventCommand("Source"));
        ReasoningEvent target = await reasoningRepository.CreateEventAsync(repository, EventCommand("Target"));
        await reasoningRepository.CreateRelationshipAsync(repository, RelationshipCommand(source.Id, target.Id));

        ReasoningTrace backward = await graphService.TraceBackwardAsync(
            repository.Id,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, target.Id));
        ReasoningTrace forward = await graphService.TraceForwardAsync(
            repository.Id,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, source.Id));

        Assert.Contains(backward.Nodes, node => node.ReferenceId == source.Id);
        Assert.Contains(backward.Relationships, relationship => relationship.SourceNodeId == $"ReasoningEvent:{source.Id}");
        Assert.Contains(forward.Nodes, node => node.ReferenceId == target.Id);
        Assert.Contains(forward.Relationships, relationship => relationship.TargetNodeId == $"ReasoningEvent:{target.Id}");
    }

    [Fact]
    public async Task BackwardTraceForDecisionShowsReasoningCause()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        ReasoningEvent cause = await reasoningRepository.CreateEventAsync(repository, EventCommand("Decision cause"));
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.CausedBy,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, cause.Id),
            new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-0001"),
            new ReasoningNarrative("The reasoning event caused the decision change."),
            Provenance()));

        ReasoningTrace trace = await graphService.TraceBackwardAsync(
            repository.Id,
            new ReasoningReference(ReasoningReferenceKind.Decision, "DEC-0001"));

        Assert.Contains(trace.Nodes, node => node.Kind == ReasoningReferenceKind.ReasoningEvent && node.ReferenceId == cause.Id);
        Assert.Contains(trace.Relationships, relationship =>
            relationship.Type == ReasoningRelationshipType.CausedBy &&
            relationship.TargetNodeId == "Decision:DEC-0001");
    }

    [Fact]
    public async Task ThreadTraversalIsAvailableThroughForwardTrace()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        ReasoningEvent first = await reasoningRepository.CreateEventAsync(repository, EventCommand("First"));
        ReasoningEvent second = await reasoningRepository.CreateEventAsync(repository, EventCommand("Second"));
        ReasoningThread thread = await reasoningRepository.CreateThreadAsync(repository, ThreadCommand([first.Id, second.Id]));

        ReasoningTrace trace = await graphService.TraceBackwardAsync(
            repository.Id,
            new ReasoningReference(ReasoningReferenceKind.ReasoningThread, thread.Id));

        Assert.Equal(
            [first.Id, second.Id],
            trace.Nodes
                .Where(node => node.Kind == ReasoningReferenceKind.ReasoningEvent)
                .OrderBy(node => node.ReferenceId, StringComparer.Ordinal)
                .Select(node => node.ReferenceId)
                .ToArray());
    }

    [Fact]
    public async Task GraphOutputIsReproducibleFromSameRepositoryState()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = CreateReasoningRepository(store);
        IReasoningGraphService graphService = CreateGraphService(repository, reasoningRepository, store);
        ReasoningEvent source = await reasoningRepository.CreateEventAsync(repository, EventCommand("Source"));
        ReasoningEvent target = await reasoningRepository.CreateEventAsync(repository, EventCommand("Target"));
        await reasoningRepository.CreateThreadAsync(repository, ThreadCommand([source.Id, target.Id]));
        await reasoningRepository.CreateRelationshipAsync(repository, RelationshipCommand(source.Id, target.Id));

        ReasoningGraph first = await graphService.GetGraphAsync(repository.Id);
        ReasoningGraph second = await graphService.GetGraphAsync(repository.Id);

        Assert.Equal(first.Nodes, second.Nodes);
        Assert.Equal(first.Relationships, second.Relationships);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
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

    private static CreateReasoningEventCommand EventCommand(
        string title,
        IReadOnlyList<ReasoningReference>? references = null)
    {
        return new CreateReasoningEventCommand(
            ReasoningEventFamily.Hypothesis,
            ReasoningEventType.HypothesisRaised,
            title,
            new ReasoningNarrative($"{title} narrative."),
            references ?? [],
            Provenance(),
            [],
            []);
    }

    private static CreateReasoningThreadCommand ThreadCommand(IReadOnlyList<string> eventIds)
    {
        return new CreateReasoningThreadCommand(
            "Thread",
            ReasoningThreadTheme.BeliefUnderInvestigation,
            "Tracks the events.",
            eventIds,
            []);
    }

    private static CreateReasoningRelationshipCommand RelationshipCommand(string sourceId, string targetId)
    {
        return new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.Supports,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, sourceId),
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, targetId),
            new ReasoningNarrative("Source supports target."),
            Provenance());
    }

    private static ReasoningProvenance Provenance()
    {
        return new ReasoningProvenance("ManualCapture", "agent");
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
