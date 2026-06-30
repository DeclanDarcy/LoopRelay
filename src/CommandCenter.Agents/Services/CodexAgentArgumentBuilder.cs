using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

/// <summary>
/// Maps a role-aware <see cref="AgentSessionSpec"/> to Codex CLI arguments (codex-cli 0.139),
/// branching on session lifetime:
/// <list type="bullet">
/// <item><b>Persistent</b> → <c>codex … app-server --listen stdio://</c>: the held-open JSON-RPC
/// transport driven by <see cref="CodexAppServerSession"/>. Sandbox/approval/effort are set per
/// thread/turn in the protocol; the global flags set process-level defaults.</item>
/// <item><b>OneShot</b> → <c>codex exec --json … -</c>: a single turn with the prompt piped on stdin,
/// events streaming back as JSONL parsed by <see cref="CodexEventTurnBoundaryDetector"/>.</item>
/// </list>
/// The removed <c>codex proto</c> subcommand no longer exists; held-open multi-turn is impossible
/// with <c>exec</c> (each <c>exec</c> turn reads its prompt to stdin EOF and exits), which is why the
/// persistent path uses <c>app-server</c>.
/// <para>
/// <c>model_reasoning_effort</c> is a free-string config value; canonical tiers are
/// <c>minimal|low|medium|high</c> (and <c>xhigh</c>). The <see cref="AgentEffortLevel"/> map emits
/// <c>low|medium|high</c>; <see cref="EffortProfile.Identifier"/> is the escape hatch for any other tier.
/// </para>
/// </summary>
public static class CodexAgentArgumentBuilder
{
    public static IReadOnlyList<string> Build(AgentSessionSpec spec, AgentSessionMode mode)
    {
        string workingDirectory = spec.WorkingDirectory ?? ".";
        string sandbox = spec.Sandbox.CanWriteWorkspace ? "workspace-write" : "read-only";

        if (mode == AgentSessionMode.Persistent)
        {
            return new List<string>
            {
                "--cd",
                workingDirectory,
                "--sandbox",
                sandbox,
                "--ask-for-approval",
                spec.Sandbox.RequiresApproval ? "on-request" : "never",
                "app-server",
                "--listen",
                "stdio://"
            };
        }

        var arguments = new List<string>
        {
            "exec",
            "--json",
            "--cd",
            workingDirectory,
            "-c",
            "approval_policy=\"never\"",
            "-c",
            $"model_reasoning_effort=\"{MapEffort(spec.Effort)}\""
        };

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
