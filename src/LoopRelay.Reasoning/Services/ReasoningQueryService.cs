using LoopRelay.Reasoning.Abstractions;
using LoopRelay.Reasoning.Models;

namespace LoopRelay.Reasoning.Services;

public sealed class ReasoningQueryService(IReasoningReconstructionService reconstructionService)
    : IReasoningQueryService
{
    public async Task<ReasoningQueryResult> RunQueryAsync(Guid repositoryId, ReasoningQuery query)
    {
        ValidateQuery(query);
        ReasoningReconstruction reconstruction = await reconstructionService.ReconstructAsync(repositoryId, query);
        return new ReasoningQueryResult(
            repositoryId,
            reconstruction.GeneratedAt,
            query,
            reconstruction,
            reconstruction.Diagnostics,
            reconstruction.DiagnosticGroups);
    }

    private static void ValidateQuery(ReasoningQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Question))
        {
            throw new ReasoningValidationException("Reasoning query question is required.");
        }

        if (string.IsNullOrWhiteSpace(query.Target.Id))
        {
            throw new ReasoningValidationException("Reasoning query target id is required.");
        }
    }
}
