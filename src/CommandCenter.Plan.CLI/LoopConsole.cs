using System.IO;

namespace CommandCenter.Plan.Cli;

/// <summary>Sink for everything the loop prints. Abstracted so tests can capture output.</summary>
internal interface ILoopConsole
{
    void Phase(string phase);
    void Message(string content);
    void Delta(string text);
    void Tool(string summary);
    void Info(string text);
    void Warn(string text);
    void Error(string text);
}

/// <summary>
/// Writes loop progress to the console. A codex turn's output streams in as deltas (<see cref="Delta"/>) via bare
/// writes with no guaranteed trailing newline, so the console tracks whether it is mid-line and closes the
/// streamed line before any structured message (phase/message/info/warn/error). Without that, a status line
/// glues onto the tail of the streamed output and the whole log runs together, hard to follow.
/// </summary>
internal sealed class ConsoleLoopConsole(TextWriter? output = null, TextWriter? error = null) : ILoopConsole
{
    private readonly TextWriter outWriter = output ?? Console.Out;
    private readonly TextWriter errWriter = error ?? Console.Error;
    private bool midLine;   // a delta was written that did not end in a newline

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

    // A tool call the agent ran mid-turn (e.g. "$ git status"), on its own indented line so it reads as a
    // compact aside within the streamed reply. Closes any half-written delta line first.
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

    // Close a half-written streamed (delta) line so the next structured message starts on its own line. The
    // newline goes to stdout (where the mid-line delta was written) even when the caller is Error (stderr).
    private void EnsureLineStart()
    {
        if (midLine)
        {
            outWriter.WriteLine();
            midLine = false;
        }
    }
}
