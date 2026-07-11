namespace LoopRelay.Agents.Services.Sessions;

/// <summary>
/// The single owner of one-shot prompt transport normalization (M7). The codex one-shot stdin
/// protocol needs a trailing newline to terminate the prompt; the gateway normalizes BEFORE
/// recording so the rendered-prompt fact holds exactly the bytes that went on the wire, and the
/// one-shot session applies the same (idempotent) normalization as defense for bare-runtime
/// callers. Persistent-session turns are framed as JSON-RPC and are never mutated.
/// </summary>
public static class AgentPromptTransport
{
    public static string EnsureTrailingNewline(string prompt) =>
        prompt.EndsWith('\n') ? prompt : prompt + "\n";
}
