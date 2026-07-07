namespace LoopRelay.Completion;

public interface ICompletionObserver
{
    void Phase(string phase);

    void Info(string text);

    void Warn(string text);
}

public sealed class NullCompletionObserver : ICompletionObserver
{
    public static NullCompletionObserver Instance { get; } = new();

    private NullCompletionObserver()
    {
    }

    public void Phase(string phase)
    {
    }

    public void Info(string text)
    {
    }

    public void Warn(string text)
    {
    }
}
