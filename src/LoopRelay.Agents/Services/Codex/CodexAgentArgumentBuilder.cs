using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Agents.Services.Codex;

/// <summary>
/// Maps a role-aware <see cref="AgentSessionSpec"/> to Codex CLI arguments (codex-cli 0.139),
/// branching on session lifetime:
/// <list type="bullet">
/// <item><b>Persistent</b> → <c>codex … app-server --listen stdio://</c>: the held-open JSON-RPC
/// transport driven by <see cref="CodexAppServerSession"/>. Sandbox/approval/effort are set per
/// thread/turn in the protocol; the global flags set process-level defaults.</item>
/// <item><b>OneShot</b> → <c>codex exec --json --skip-git-repo-check … -</c>: a single turn with the
/// prompt piped on stdin, events streaming back as JSONL parsed by
/// <see cref="CodexEventTurnBoundaryDetector"/>. <c>--skip-git-repo-check</c> is required because
/// one-shot turns may run with <c>--cd</c> pointed at a temp sandbox workspace outside any trusted
/// git repository — without the flag codex exits 1 immediately ("Not inside a trusted directory and
/// --skip-git-repo-check was not specified") and the turn never runs.</item>
/// </list>
/// The removed <c>codex proto</c> subcommand no longer exists; held-open multi-turn is impossible
/// with <c>exec</c> (each <c>exec</c> turn reads its prompt to stdin EOF and exits), which is why the
/// persistent path uses <c>app-server</c>.
/// <para>
/// Model and effort are canonical session fields and are always projected explicitly.
/// </para>
/// </summary>
public static class CodexAgentArgumentBuilder
{
    public static IReadOnlyList<string> Build(AgentSessionSpec spec, AgentSessionMode mode)
    {
        string workingDirectory = spec.WorkingDirectory ?? ".";
        // The Identifier IS the codex sandbox mode (read-only | workspace-write | danger-full-access), emitted
        // verbatim as the process-level default; the persistent path also sets it per thread/start.
        string sandbox = spec.Sandbox.Identifier;

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
                "--model",
                AgentConfigurationCatalog.Format(spec.Model),
                "-c",
                $"model_reasoning_effort=\"{AgentConfigurationCatalog.Format(spec.Effort)}\"",
                "app-server",
                "--listen",
                "stdio://"
            };
        }

        var arguments = new List<string>
        {
            "exec",
            "--json",
            // One-shots may target a temp sandbox --cd outside any trusted git repo; codex refuses to run
            // there (exit 1, "Not inside a trusted directory") unless the trust check is skipped.
            "--skip-git-repo-check",
            "--cd",
            workingDirectory,
            "--model",
            AgentConfigurationCatalog.Format(spec.Model),
            "-c",
            "approval_policy=\"never\"",
            "-c",
            $"model_reasoning_effort=\"{AgentConfigurationCatalog.Format(spec.Effort)}\""
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
}
