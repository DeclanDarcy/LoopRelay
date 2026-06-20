namespace CommandCenter.Backend.Continuity;

public interface IUnderstandingDiffService
{
    IReadOnlyList<OperationalContextSemanticChange> Compare(
        OperationalContextDocument current,
        OperationalContextDocument proposed);
}
