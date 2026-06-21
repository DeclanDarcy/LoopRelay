using CommandCenter.Continuity.Models;

namespace CommandCenter.Continuity.Abstractions;

public interface IUnderstandingCompressionService
{
    OperationalContextCompressionResult Compress(
        OperationalContextDocument current,
        OperationalContextDocument proposed);
}
