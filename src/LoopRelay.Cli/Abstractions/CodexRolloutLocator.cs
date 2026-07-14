namespace LoopRelay.Cli.Abstractions;

/// <summary>Resolves the on-disk codex rollout JSONL for a session's process, or null when it cannot.</summary>
internal interface ICodexRolloutLocator
{
    /// <param name="workingDirectory">The session's cwd (matched against the rollout's session_meta.cwd).</param>
    /// <param name="openedAtUtc">When the session opened; the rollout must have started at/after this.</param>
    string? Resolve(string workingDirectory, DateTimeOffset openedAtUtc);
}
