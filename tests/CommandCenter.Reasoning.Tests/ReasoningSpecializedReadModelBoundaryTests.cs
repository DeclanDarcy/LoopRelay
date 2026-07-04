using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Reasoning.Abstractions;
using CommandCenter.Reasoning.Models;
using CommandCenter.Reasoning.Projections;
using CommandCenter.Reasoning.Services;

namespace CommandCenter.Reasoning.Tests;

public sealed class ReasoningSpecializedReadModelBoundaryTests
{
    [Fact]
    public async Task MaterializationRecommendationsDoNotCreateReadModelsOrAffectReasoningOutputs()
    {
        var store = new MemoryArtifactStore();
        Repository repository = CreateRepository();
        IReasoningRepository reasoningRepository = new FileSystemReasoningRepository(
            store,
            new ReasoningArtifactProjectionService());
        IReasoningGraphService graphService = new ReasoningGraphService(
            new StubRepositoryService(repository),
            reasoningRepository,
            store);
        IReasoningQueryService queryService = new ReasoningQueryService(
            new ReasoningReconstructionService(
                new StubRepositoryService(repository),
                reasoningRepository,
                graphService));
        IReasoningMaterializationReviewService reviewService = new ReasoningMaterializationReviewService(
            new StubRepositoryService(repository),
            reasoningRepository);

        ReasoningEvent introduced = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Alternative,
            ReasoningEventType.AlternativeIntroduced,
            "Alternative introduced"));
        ReasoningEvent rejected = await reasoningRepository.CreateEventAsync(repository, EventCommand(
            ReasoningEventFamily.Alternative,
            ReasoningEventType.AlternativeRejected,
            "Alternative rejected"));
        await reasoningRepository.CreateRelationshipAsync(repository, new CreateReasoningRelationshipCommand(
            ReasoningRelationshipType.Invalidates,
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, rejected.Id),
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, introduced.Id),
            new ReasoningNarrative("The rejected event invalidates the introduced alternative."),
            Provenance()));
        ReasoningQuery query = new(
            ReasoningQueryCategory.Alternative,
            "Why was this alternative rejected?",
            new ReasoningReference(ReasoningReferenceKind.ReasoningEvent, introduced.Id));

        ReasoningGraph graphBefore = await graphService.GetGraphAsync(repository.Id);
        ReasoningQueryResult queryBefore = await queryService.RunQueryAsync(repository.Id, query);

        ReasoningMaterializationReviewReport report = await reviewService.RunReviewAsync(
            repository.Id,
            new ReasoningMaterializationReviewRequest(
            [
                new ReasoningMaterializationScenario(
                    ReasoningMaterializationConcept.Alternative,
                    "Why was the alternative rejected?",
                    true,
                    "Fixture deliberately reports reconstruction pressure."),
                new ReasoningMaterializationScenario(
                    ReasoningMaterializationConcept.Alternative,
                    "Which path replaced the rejected alternative?",
                    true,
                    "Repeated fixture pressure is still advisory only."),
                new ReasoningMaterializationScenario(
                    ReasoningMaterializationConcept.Hypothesis,
                    "Which hypothesis summary is repeated?",
                    false,
                    "Repeated workflow pressure suggests cache pressure only.",
                    RepeatedWorkflowCount: 3)
            ]));

        ReasoningGraph graphAfter = await graphService.GetGraphAsync(repository.Id);
        ReasoningQueryResult queryAfter = await queryService.RunQueryAsync(repository.Id, query);

        Assert.Equal(
            ReasoningMaterializationOutcome.AddReadModelReport,
            ReviewFor(report, ReasoningMaterializationConcept.Alternative).Recommendation);
        Assert.Equal(
            ReasoningMaterializationOutcome.AddDerivedCache,
            ReviewFor(report, ReasoningMaterializationConcept.Hypothesis).Recommendation);
        Assert.Equal(GraphSignature(graphBefore), GraphSignature(graphAfter));
        Assert.Equal(QuerySignature(queryBefore), QuerySignature(queryAfter));
        await AssertNoUnapprovedSpecializedArtifactsAsync(store, repository);
    }

    private static ReasoningConceptMaterializationReview ReviewFor(
        ReasoningMaterializationReviewReport report,
        ReasoningMaterializationConcept concept)
    {
        return Assert.Single(report.Concepts, review => review.Concept == concept);
    }

    private static string[] GraphSignature(ReasoningGraph graph)
    {
        return graph.Nodes
            .Select(node => $"node:{node.Id}:{node.Resolved}")
            .Concat(graph.Relationships.Select(relationship =>
                $"relationship:{relationship.Id}:{relationship.Type}:{relationship.SourceNodeId}:{relationship.TargetNodeId}"))
            .Concat(graph.Diagnostics.Select(diagnostic => $"diagnostic:{diagnostic}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] QuerySignature(ReasoningQueryResult result)
    {
        return result.Reconstruction.Trace.Nodes
            .Select(node => $"node:{node.Id}")
            .Concat(result.Reconstruction.Trace.Relationships.Select(relationship => $"relationship:{relationship.Id}"))
            .Concat(result.Reconstruction.Evidence.Select(evidence => $"evidence:{evidence.Kind}:{evidence.Id}:{evidence.Title}"))
            .Concat(result.Diagnostics.Select(diagnostic => $"diagnostic:{diagnostic}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task AssertNoUnapprovedSpecializedArtifactsAsync(
        IArtifactStore store,
        Repository repository)
    {
        foreach (string directory in new[]
        {
            "hypotheses",
            "alternatives",
            "contradictions",
            "directions",
            "graph",
            "queries",
            "reports"
        })
        {
            string path = Path.Combine(repository.Path, ".agents", "reasoning", directory);
            Assert.Empty(await store.ListAsync(path, "*"));
            Assert.Empty(await store.ListDirectoriesAsync(path));
        }
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
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Repo",
            Path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"))
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
