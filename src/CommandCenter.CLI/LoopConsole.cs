namespace CommandCenter.Cli;

/// <summary>Sink for everything the loop prints. Abstracted so tests can capture output.</summary>
internal interface ILoopConsole
{
    void Phase(string phase);
    void Message(string content);
    void Delta(string text);
    void Info(string text);
    void Warn(string text);
    void Error(string text);
}

/// <summary>Writes loop progress to the real console. Deltas stream inline; messages/info/warn/error get prefixes.</summary>
internal sealed class ConsoleLoopConsole : ILoopConsole
{
    public void Phase(string phase) => Console.WriteLine($"\n=== {phase} ===");
    public void Message(string content) => Console.WriteLine(content);
    public void Delta(string text) => Console.Write(text);
    public void Info(string text) => Console.WriteLine($"[ok] {text}");
    public void Warn(string text) => Console.WriteLine($"[warn] {text}");
    public void Error(string text) => Console.Error.WriteLine($"[error] {text}");
}
