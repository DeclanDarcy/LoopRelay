using CommandCenter.Continuity.Models;

namespace CommandCenter.Continuity.Abstractions;

public interface IUnderstandingDiffService
{
    IReadOnlyList<OperationalContextSemanticChange> Compare(
        OperationalContextDocument current,
        OperationalContextDocument proposed);
}
