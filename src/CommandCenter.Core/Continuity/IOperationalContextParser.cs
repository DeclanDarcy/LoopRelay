namespace CommandCenter.Core.Continuity;

public interface IOperationalContextParser
{
    OperationalContextDocument Parse(string markdown);

    string Render(OperationalContextDocument document);
}
