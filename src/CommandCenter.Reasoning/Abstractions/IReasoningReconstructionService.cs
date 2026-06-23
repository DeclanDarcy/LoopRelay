using CommandCenter.Reasoning.Models;

namespace CommandCenter.Reasoning.Abstractions;

public interface IReasoningReconstructionService
{
    Task<ReasoningReconstruction> ReconstructAsync(Guid repositoryId, ReasoningQuery query);
}
