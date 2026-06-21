using CommandCenter.Continuity.Models;

namespace CommandCenter.Continuity.Abstractions;

public interface IOperationalContextParser
{
    OperationalContextDocument Parse(string markdown);

    string Render(OperationalContextDocument document);
}
