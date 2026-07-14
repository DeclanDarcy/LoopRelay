using LoopRelay.Infrastructure.Abstractions.Console;

namespace LoopRelay.Infrastructure.Services.Console;

/// <summary>
/// Writes loop progress to the console while preserving streamed turn layout.
/// </summary>
public class ConsoleLoopConsole(TextWriter? _output = null, TextWriter? _error = null) : ILoopConsole
{
    private readonly TextWriter _outWriter = _output ?? System.Console.Out;
    private readonly TextWriter _errWriter = _error ?? System.Console.Error;
    private readonly bool _progressInteractive = _output is null && _error is null && !System.Console.IsErrorRedirected;
    private bool midLine;
    private bool progressLineActive;
    private int progressLineLength;

    public bool IsProgressInteractive => _progressInteractive;

    public void Phase(string phase)
    {
        EnsureLineStart();
        _outWriter.WriteLine($"\n=== {phase} ===");
    }

    public void Message(string content)
    {
        EnsureLineStart();
        _outWriter.WriteLine(content);
    }

    public void Delta(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _outWriter.Write(text);
        midLine = text[^1] != '\n';
    }

    public void Tool(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        EnsureLineStart();
        _outWriter.WriteLine($"  {summary}");
    }

    public void Info(string text)
    {
        EnsureLineStart();
        _outWriter.WriteLine($"[ok] {text}");
    }

    public void Warn(string text)
    {
        EnsureLineStart();
        _outWriter.WriteLine($"[warn] {text}");
    }

    public void Error(string text)
    {
        ProgressComplete();
        EnsureLineStart();
        _errWriter.WriteLine($"[error] {text}");
    }

    public void Progress(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!_progressInteractive)
        {
            _errWriter.WriteLine(text);
            return;
        }

        _errWriter.Write('\r');
        _errWriter.Write(text);
        if (progressLineLength > text.Length)
        {
            _errWriter.Write(new string(' ', progressLineLength - text.Length));
            _errWriter.Write('\r');
            _errWriter.Write(text);
        }

        progressLineLength = text.Length;
        progressLineActive = true;
    }

    public void ProgressComplete(string? text = null)
    {
        if (!_progressInteractive)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                _errWriter.WriteLine(text);
            }

            return;
        }

        if (progressLineActive)
        {
            _errWriter.Write('\r');
            _errWriter.Write(new string(' ', progressLineLength));
            _errWriter.Write('\r');
            progressLineActive = false;
            progressLineLength = 0;
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            _errWriter.WriteLine(text);
        }
    }

    private void EnsureLineStart()
    {
        if (!midLine)
        {
            return;
        }

        _outWriter.WriteLine();
        midLine = false;
    }
}
