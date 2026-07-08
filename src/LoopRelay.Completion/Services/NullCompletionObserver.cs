namespace LoopRelay.Completion;

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
