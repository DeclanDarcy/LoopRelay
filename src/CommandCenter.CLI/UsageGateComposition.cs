using System;

namespace CommandCenter.Cli;

/// <summary>
/// Selects the Codex usage watermark gate for the CLI loop. Disabled by default — the loop wires in
/// <see cref="NullUsageGate"/>, so turns run without probing or waiting on Codex quota. Set
/// <c>COMMANDCENTER_USAGE_GATE=1</c> (or <c>true</c>) to re-enable the real <see cref="UsageGate"/> and its
/// wait-for-reset behavior. UsageGate's own code and tests are untouched by this switch — it only decides
/// which implementation the loop wires up.
/// </summary>
internal static class UsageGateComposition
{
    public static bool IsEnabled()
    {
        string? flag = Environment.GetEnvironmentVariable("COMMANDCENTER_USAGE_GATE");
        return string.Equals(flag, "1", StringComparison.OrdinalIgnoreCase)
               || string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static IUsageGate Create(ICodexUsageProbe probe, IUsageDelay delay, ILoopConsole console) =>
        IsEnabled() ? new UsageGate(probe, delay, console) : new NullUsageGate();
}
