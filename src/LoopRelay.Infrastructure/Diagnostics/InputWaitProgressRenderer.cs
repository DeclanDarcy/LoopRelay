using LoopRelay.Infrastructure.Console;

namespace LoopRelay.Infrastructure.Diagnostics;

public sealed record InputWaitProgressSnapshot(
    int PromptTokensEstimated,
    TimeSpan Elapsed,
    bool HasFirstOutput);

public interface IInputWaitProgressRenderer
{
    TimeSpan RefreshInterval { get; }

    void Started(InputWaitProgressSnapshot snapshot);

    void Waiting(InputWaitProgressSnapshot snapshot);

    void FirstOutput(InputWaitProgressSnapshot snapshot);

    void CompletedWithoutOutput(InputWaitProgressSnapshot snapshot);
}

public sealed class ConsoleInputWaitProgressRenderer(ILoopConsole console) : IInputWaitProgressRenderer
{
    public TimeSpan RefreshInterval =>
        console.IsProgressInteractive ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(30);

    public void Started(InputWaitProgressSnapshot snapshot)
    {
        if (console.IsProgressInteractive)
        {
            console.Progress(StatusLine(snapshot));
            return;
        }

        console.Progress($"[codex] submitted turn: promptTokensEstimated={snapshot.PromptTokensEstimated}");
    }

    public void Waiting(InputWaitProgressSnapshot snapshot)
    {
        if (console.IsProgressInteractive)
        {
            console.Progress(StatusLine(snapshot));
            return;
        }

        console.Progress($"[codex] waiting for first output: elapsed={FormatElapsedCompact(snapshot.Elapsed)}");
    }

    public void FirstOutput(InputWaitProgressSnapshot snapshot) =>
        console.ProgressComplete($"[codex] first output: elapsed={FormatElapsedCompact(snapshot.Elapsed)}");

    public void CompletedWithoutOutput(InputWaitProgressSnapshot snapshot) =>
        console.ProgressComplete($"[codex] completed before visible output: elapsed={FormatElapsedCompact(snapshot.Elapsed)}");

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
