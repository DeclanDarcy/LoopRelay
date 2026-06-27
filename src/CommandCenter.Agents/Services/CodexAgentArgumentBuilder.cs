using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

/// <summary>
/// Maps a role-aware <see cref="AgentSessionSpec"/> to Codex CLI arguments for one-shot
/// and held-open (persistent) launches.
/// NOTE: the exact Codex subcommands and flag values are pending validation against the
/// installed Codex version (plan "Process Model": "The exact model_reasoning_effort values
/// must be validated against the installed Codex version"). The mapping below is structured
/// and unit-tested but MUST be confirmed before the held-open path drives a live process.
/// </summary>
public static class CodexAgentArgumentBuilder
{
    public static IReadOnlyList<string> Build(AgentSessionSpec spec, AgentSessionMode mode)
    {
        string workingDirectory = spec.WorkingDirectory ?? ".";
        var arguments = new List<string>();

        if (mode == AgentSessionMode.Persistent)
        {
            // Held-open, multi-turn protocol owned by CommandCenter.Agents.
            arguments.Add("proto");
            arguments.Add("--cd");
            arguments.Add(workingDirectory);
        }
        else
        {
            // One-shot: prompt piped on stdin (mirrors CodexExecutionProvider).
            arguments.Add("exec");
            arguments.Add("--cd");
            arguments.Add(workingDirectory);
            arguments.Add("-");
        }

        arguments.Add("--sandbox");
        arguments.Add(spec.Sandbox.CanWriteWorkspace ? "workspace-write" : "read-only");

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
