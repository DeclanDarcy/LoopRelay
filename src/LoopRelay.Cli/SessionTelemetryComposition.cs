using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Abstractions;

namespace LoopRelay.Cli;

/// <summary>
/// Builds the per-turn telemetry recorder for the CLI loop. Enabled by default; set
/// <c>LoopRelay_SESSION_LOG=0</c> (or <c>false</c>) to disable (swaps in the no-op recorder, which also
/// skips the extra post-turn probe). The log lives under <c>&lt;repo&gt;/.LoopRelay/telemetry/</c>.
/// </summary>
internal static class SessionTelemetryComposition
{
    public static bool IsEnabled()
    {
        string? flag = Environment.GetEnvironmentVariable("LoopRelay_SESSION_LOG");
        return !(string.Equals(flag, "0", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(flag, "false", StringComparison.OrdinalIgnoreCase));
    }

    public static string RepoName(Repository repository) =>
        !string.IsNullOrWhiteSpace(repository.Name)
            ? repository.Name
            : Path.GetFileName(repository.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public static ISessionTelemetryRecorder CreateRecorder(
        Repository repository,
        bool enabled,
        ICodexUsageProbe probe,
        IDecisionCostModel costModel,
        IClock clock,
        ILoopConsole console)
    {
        if (!enabled)
        {
            return new NullSessionTelemetryRecorder();
        }

        string directory = Path.Combine(repository.Path, ".LoopRelay", "telemetry");
        var sink = new RotatingJsonlTelemetrySink(directory, clock);
        var locator = new FileSystemCodexRolloutLocator(FileSystemCodexRolloutLocator.ResolveDefaultSessionsRoot());
        return new SessionTelemetryRecorder(probe, locator, sink, costModel, clock, console);
    }
}
