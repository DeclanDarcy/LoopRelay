using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

/// <summary>
/// Maps a role-aware <see cref="AgentSessionSpec"/> to Codex CLI arguments. Validated against
/// codex-cli 0.139: a turn is run with <c>exec --json</c> (the prompt is piped on stdin via the
/// trailing <c>-</c>; events stream back as JSONL parsed by <see cref="CodexEventTurnBoundaryDetector"/>).
/// <para>
/// The removed <c>codex proto</c> subcommand no longer exists, so the held-open multi-turn path is
/// not expressible with <c>exec</c> alone (each <c>exec</c> turn reads its prompt to stdin EOF and
/// exits). Multi-turn continuation must use <c>exec resume &lt;session-id&gt;</c> (resumable,
/// process-per-turn) or the experimental <c>app-server</c> (held-open) transport — both are the
/// next m1 step. The <paramref name="mode"/> therefore no longer changes the argv; it is retained
/// for the call site and governs stdin/lifecycle in the runtime, not the arguments.
/// </para>
/// <para>
/// <c>model_reasoning_effort</c> is a free-string config value in the Codex protocol; the canonical
/// tiers are <c>minimal|low|medium|high</c>. The <see cref="AgentEffortLevel"/> map below emits
/// <c>low|medium|high</c>, and <see cref="EffortProfile.Identifier"/> is the escape hatch for any
/// other tier (e.g. <c>minimal</c>).
/// </para>
/// </summary>
public static class CodexAgentArgumentBuilder
{
    public static IReadOnlyList<string> Build(AgentSessionSpec spec, AgentSessionMode mode)
    {
        _ = mode; // argv is identical for one-shot and (future) resumable turns; see remarks.
        string workingDirectory = spec.WorkingDirectory ?? ".";

        var arguments = new List<string>
        {
            "exec",
            "--json",
            "--cd",
            workingDirectory,
            "--sandbox",
            spec.Sandbox.CanWriteWorkspace ? "workspace-write" : "read-only"
        };

        if (!spec.Sandbox.RequiresApproval)
        {
            arguments.Add("-c");
            arguments.Add("approval_policy=\"never\"");
        }

        arguments.Add("-c");
        arguments.Add($"model_reasoning_effort=\"{MapEffort(spec.Effort)}\"");

        foreach (KeyValuePair<string, string> option in spec.StartupOptions)
        {
            arguments.Add("-c");
            arguments.Add($"{option.Key}={option.Value}");
        }

        // Trailing positional '-' reads the prompt from stdin; must come after all options.
        arguments.Add("-");

        return arguments;
    }

    private static string MapEffort(EffortProfile effort) =>
        effort.Identifier is { Length: > 0 } identifier
            ? identifier
            : effort.Level switch
            {
                AgentEffortLevel.High => "high",
                AgentEffortLevel.Medium => "medium",
                _ => "low"
            };
}
