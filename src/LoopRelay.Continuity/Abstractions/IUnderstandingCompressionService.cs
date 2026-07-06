using LoopRelay.Continuity.Models;

namespace LoopRelay.Continuity.Abstractions;

public interface IUnderstandingCompressionService
{
    OperationalContextCompressionResult Compress(
        OperationalContextDocument current,
        OperationalContextDocument proposed);
}
