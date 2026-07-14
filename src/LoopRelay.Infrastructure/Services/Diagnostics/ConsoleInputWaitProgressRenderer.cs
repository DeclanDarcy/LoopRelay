using LoopRelay.Infrastructure.Abstractions.Console;
using LoopRelay.Infrastructure.Abstractions.Diagnostics;
using LoopRelay.Infrastructure.Models.Diagnostics;

namespace LoopRelay.Infrastructure.Services.Diagnostics;

public sealed class ConsoleInputWaitProgressRenderer(ILoopConsole _console) : IInputWaitProgressRenderer
{
    public TimeSpan RefreshInterval =>
        _console.IsProgressInteractive ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(30);

    public void Started(InputWaitProgressSnapshot snapshot)
    {
        if (_console.IsProgressInteractive)
        {
            _console.Progress(StatusLine(snapshot));
            return;
        }

        _console.Progress($"[codex] submitted turn: promptTokensEstimated={snapshot.PromptTokensEstimated}");
    }

    public void Waiting(InputWaitProgressSnapshot snapshot)
    {
        if (_console.IsProgressInteractive)
        {
            _console.Progress(StatusLine(snapshot));
            return;
        }

        _console.Progress($"[codex] waiting for first output: elapsed={FormatElapsedCompact(snapshot.Elapsed)}");
    }

    public void FirstOutput(InputWaitProgressSnapshot snapshot) =>
        _console.ProgressComplete($"[codex] first output: elapsed={FormatElapsedCompact(snapshot.Elapsed)}");

    public void CompletedWithoutOutput(InputWaitProgressSnapshot snapshot) =>
        _console.ProgressComplete($"[codex] completed before visible output: elapsed={FormatElapsedCompact(snapshot.Elapsed)}");

    private static string StatusLine(InputWaitProgressSnapshot snapshot) =>
        $"[codex] processing input | {FormatTokens(snapshot.PromptTokensEstimated)} prompt tokens estimated | {FormatElapsed(snapshot.Elapsed)} elapsed";

    private static string FormatTokens(int tokens) =>
        tokens >= 1_000
            ? $"{(int)Math.Round(tokens / 1_000.0)}k"
            : tokens.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatElapsed(TimeSpan elapsed) =>
        $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";

    private static string FormatElapsedCompact(TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes >= 1)
        {
            return $"{(int)elapsed.TotalMinutes}m{elapsed.Seconds:00}s";
        }

        return $"{Math.Max(0, (int)Math.Round(elapsed.TotalSeconds))}s";
    }
}
