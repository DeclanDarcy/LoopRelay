namespace LoopRelay.Completion;

public interface ICompletionObserver
{
    void Phase(string phase);

    void Info(string text);

    void Warn(string text);
}
