using LoopRelay.Continuity.Models;

namespace LoopRelay.Continuity.Abstractions;

public interface IOperationalContextParser
{
    OperationalContextDocument Parse(string markdown);

    string Render(OperationalContextDocument document);
}
