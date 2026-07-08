namespace LoopRelay.Orchestration.Abstractions;

/// <summary>
/// A single sandbox workspace root. Disposal removes the workspace and everything in it (best-effort — a
/// still-locked file must never fail the transfer). Path resolution is a sealed default so every implementation
/// (real and test) maps a repository-relative artifact path to the SAME absolute location under the root.
/// </summary>
public interface ISandboxWorkspace : IAsyncDisposable
{
    /// <summary>Absolute path of the workspace root — pass as the agent spec's WorkingDirectory (codex <c>--cd</c>).</summary>
    string RootPath { get; }

    /// <summary>Resolves a repository-relative artifact path (e.g. <c>.agents/operational_context.md</c>) to its
    /// absolute location inside this workspace. Sealed so the real and in-memory workspaces cannot diverge.</summary>
    sealed string Resolve(string relativePath) =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(RootPath, relativePath));
}
