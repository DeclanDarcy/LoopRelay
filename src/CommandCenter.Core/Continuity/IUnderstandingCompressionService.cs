namespace CommandCenter.Core.Continuity;

public interface IUnderstandingCompressionService
{
    OperationalContextCompressionResult Compress(
        OperationalContextDocument current,
        OperationalContextDocument proposed);
}
