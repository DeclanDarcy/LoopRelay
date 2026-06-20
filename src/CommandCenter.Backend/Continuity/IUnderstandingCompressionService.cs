namespace CommandCenter.Backend.Continuity;

public interface IUnderstandingCompressionService
{
    OperationalContextCompressionResult Compress(
        OperationalContextDocument current,
        OperationalContextDocument proposed);
}
