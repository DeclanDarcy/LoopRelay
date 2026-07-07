namespace LoopRelay.Infrastructure.Console;

/// <summary>
/// Writes loop progress to the console while preserving streamed turn layout.
/// </summary>
public class ConsoleLoopConsole(TextWriter? output = null, TextWriter? error = null) : ILoopConsole
{
    private readonly TextWriter outWriter = output ?? System.Console.Out;
    private readonly TextWriter errWriter = error ?? System.Console.Error;
    private readonly bool progressInteractive = output is null && error is null && !System.Console.IsErrorRedirected;
    private bool midLine;
    private bool progressLineActive;
    private int progressLineLength;

    public bool IsProgressInteractive => progressInteractive;

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
        ProgressComplete();
        EnsureLineStart();
        errWriter.WriteLine($"[error] {text}");
    }

    public void Progress(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!progressInteractive)
        {
            errWriter.WriteLine(text);
            return;
        }

        errWriter.Write('\r');
        errWriter.Write(text);
        if (progressLineLength > text.Length)
        {
            errWriter.Write(new string(' ', progressLineLength - text.Length));
            errWriter.Write('\r');
            errWriter.Write(text);
        }

        progressLineLength = text.Length;
        progressLineActive = true;
    }

    public void ProgressComplete(string? text = null)
    {
        if (!progressInteractive)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                errWriter.WriteLine(text);
            }

            return;
        }

        if (progressLineActive)
        {
            errWriter.Write('\r');
            errWriter.Write(new string(' ', progressLineLength));
            errWriter.Write('\r');
            progressLineActive = false;
            progressLineLength = 0;
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            errWriter.WriteLine(text);
        }
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
