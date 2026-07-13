using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Abstractions;

namespace LoopRelay.Cli.Services.Telemetry;

/// <summary>
/// Builds the per-turn telemetry recorder for the CLI loop. Whether it is enabled is decided by
/// the resolved operational policy (<c>runtime.sessionTelemetry</c>, M7) — the old ad-hoc
/// <c>LoopRelay_SESSION_LOG</c> read retired when the variable became a resolver-validated
/// ambient policy input. Disabled swaps in the no-op recorder, which also skips the extra
/// post-turn probe. SQLite under <c>&lt;repo&gt;/.LoopRelay/persistence/looprelay.sqlite3</c> is
/// canonical; JSONL under <c>&lt;repo&gt;/.LoopRelay/telemetry/</c> remains a compatibility export.
/// </summary>
internal static class SessionTelemetryComposition
{
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
        ILoopConsole console,
        ProviderEnvironmentConfiguration? providerEnvironment = null)
    {
        if (!enabled)
        {
            return new NullSessionTelemetryRecorder();
        }

        string directory = Path.Combine(repository.Path, ".LoopRelay", "telemetry");
        var sink = new CompositeSessionTelemetrySink(
        [
            new SqliteSessionTelemetrySink(repository),
            new RotatingJsonlTelemetrySink(directory, clock),
        ]);
        providerEnvironment ??= ProviderEnvironmentConfiguration.Resolve();
        var locator = new FileSystemCodexRolloutLocator(
            FileSystemCodexRolloutLocator.ResolveDefaultSessionsRoot(providerEnvironment));
        return new SessionTelemetryRecorder(probe, locator, sink, costModel, clock, console, providerEnvironment);
    }
}
