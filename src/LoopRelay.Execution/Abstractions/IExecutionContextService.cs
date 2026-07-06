
using LoopRelay.Execution.Models;

namespace LoopRelay.Execution.Abstractions;

public interface IImplementationExecutionContextService
{
    Task<ImplementationExecutionContext> BuildContextAsync(Guid repositoryId);
}
