using LoopRelay.Continuity.Models;

namespace LoopRelay.Continuity.Abstractions;

public interface IUnderstandingDiffService
{
    IReadOnlyList<OperationalContextSemanticChange> Compare(
        OperationalContextDocument current,
        OperationalContextDocument proposed);
}
