using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Chaining;

public sealed class DurableBoundaryEvidenceTests
{
    [Fact]
    public async Task BoundaryEvidenceSurvivesRestartAsAppendOnlyFacts()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-boundary-").FullName;
        try
        {
            var repository = new Repository { Id = Guid.NewGuid(), Name = "boundary", Path = root };
            var store = new CanonicalWorkflowPersistenceStore(repository);
            var writer = new WorkflowBoundaryEvidenceWriter(new CanonicalChainBoundaryEvidenceStore(store));
            WorkflowBoundaryEvaluation boundary = Boundary();
            RunIdentity run = RunIdentity.New();

            await writer.WriteAsync(boundary, run, "TraditionalRoadmapToExecute", CancellationToken.None);
            await writer.WriteAsync(boundary, run, "TraditionalRoadmapToExecute", CancellationToken.None);
            IReadOnlyList<CanonicalChainBoundaryEventRecord> restarted =
                await new CanonicalWorkflowPersistenceStore(repository)
                    .ReadChainBoundaryEventsAsync(CancellationToken.None);

            Assert.Equal(2, restarted.Count);
            CanonicalChainBoundaryEventRecord record = restarted[0];
            Assert.Equal("TraditionalRoadmapToExecute", record.ChainIdentity);
            Assert.Equal(run.Value, record.RunId);
            Assert.Equal(WorkflowIdentity.Plan, record.TargetWorkflow);
            Assert.Equal("Advanced", record.Decision);
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
