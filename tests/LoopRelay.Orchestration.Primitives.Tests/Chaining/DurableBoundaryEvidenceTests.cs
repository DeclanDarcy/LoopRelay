using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Chaining;

public sealed class DurableBoundaryEvidenceTests
{
    [Fact]
    public async Task BoundaryEvidenceSurvivesRestartAndDuplicateWriteIsIdempotent()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-boundary-").FullName;
        try
        {
            var repository = new Repository { Id = Guid.NewGuid(), Name = "boundary", Path = root };
            var store = new CanonicalWorkflowPersistenceStore(repository);
            var writer = new WorkflowBoundaryEvidenceWriter(store);
            WorkflowBoundaryEvaluation boundary = Boundary();

            await writer.WriteAsync(boundary, "TraditionalRoadmapToExecute", CancellationToken.None);
            await writer.WriteAsync(boundary, "TraditionalRoadmapToExecute", CancellationToken.None);
            CanonicalWorkflowPersistenceSnapshot restarted = await new CanonicalWorkflowPersistenceStore(repository)
                .LoadSnapshotAsync(CancellationToken.None);

            CanonicalWorkflowChainRunRecord record = Assert.Single(restarted.WorkflowChainRuns);
            Assert.Equal("TraditionalRoadmapToExecute", record.ChainIdentity);
            Assert.Equal(WorkflowIdentity.Plan, record.CurrentWorkflow);
            Assert.Equal(RuntimeOutcomeKind.Completed, record.Status);
            Assert.Contains("boundary:TraditionalRoadmap->Plan", record.Evidence);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static WorkflowBoundaryEvaluation Boundary()
    {
        GateResult satisfied = new(GateStatus.Satisfied, [], "satisfied", ["product:prepared-epic"]);
        return new WorkflowBoundaryEvaluation(
            WorkflowIdentity.TraditionalRoadmap,
            WorkflowIdentity.Plan,
            satisfied,
            satisfied,
            new ProductTransferResult(
                WorkflowIdentity.TraditionalRoadmap,
                WorkflowIdentity.Plan,
                [],
                satisfied,
                "transferred"),
            true,
            "Advanced from TraditionalRoadmap to Plan.");
    }
}
