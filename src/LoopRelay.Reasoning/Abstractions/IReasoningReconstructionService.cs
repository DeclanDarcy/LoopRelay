using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Abstractions;

public interface IReasoningReconstructionService
{
    Task<ReasoningReconstruction> ReconstructAsync(Guid repositoryId, ReasoningQuery query);

    Task<ReasoningReconstructionReport> RunReconstructionAsync(Guid repositoryId, ReasoningQuery query);

    Task<IReadOnlyList<ReasoningReconstructionReport>> ListReportsAsync(Guid repositoryId);
}
