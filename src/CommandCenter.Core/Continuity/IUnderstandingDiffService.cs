namespace CommandCenter.Core.Continuity;

public interface IUnderstandingDiffService
{
    IReadOnlyList<OperationalContextSemanticChange> Compare(
        OperationalContextDocument current,
        OperationalContextDocument proposed);
}
