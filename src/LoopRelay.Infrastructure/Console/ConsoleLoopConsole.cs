namespace LoopRelay.Infrastructure.Console;

/// <summary>
/// Writes loop progress to the console while preserving streamed turn layout.
/// </summary>
public class ConsoleLoopConsole(TextWriter? output = null, TextWriter? error = null) : ILoopConsole
{
    private readonly TextWriter outWriter = output ?? System.Console.Out;
    private readonly TextWriter errWriter = error ?? System.Console.Error;
    private bool midLine;

    public void Phase(string phase)
    {
        EnsureLineStart();
        outWriter.WriteLine($"\n=== {phase} ===");
    }

    public void Message(string content)
    {
        EnsureLineStart();
        outWriter.WriteLine(content);
    }

    public void Delta(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        outWriter.Write(text);
        midLine = text[^1] != '\n';
    }

    public void Tool(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        EnsureLineStart();
        outWriter.WriteLine($"  {summary}");
    }

    public void Info(string text)
    {
        EnsureLineStart();
        outWriter.WriteLine($"[ok] {text}");
    }

    public void Warn(string text)
    {
        EnsureLineStart();
        outWriter.WriteLine($"[warn] {text}");
    }

    public void Error(string text)
    {
        EnsureLineStart();
        errWriter.WriteLine($"[error] {text}");
    }

    private void EnsureLineStart()
    {
        if (!midLine)
        {
            return;
        }

        outWriter.WriteLine();
        midLine = false;
    }
}
