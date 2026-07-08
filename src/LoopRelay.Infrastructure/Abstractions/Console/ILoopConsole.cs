namespace LoopRelay.Infrastructure.Abstractions.Console;

/// <summary>Sink for structured host output and streamed agent deltas.</summary>
public interface ILoopConsole
{
    bool IsProgressInteractive => false;

    void Phase(string phase);
    void Message(string content);
    void Delta(string text);
    void Tool(string summary);
    void Info(string text);
    void Warn(string text);
    void Error(string text);
    void Progress(string text) { }
    void ProgressComplete(string? text = null)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Progress(text);
        }
    }
}
