namespace LoopRelay.Orchestration.Abstractions;

/// <summary>
/// Creates an isolated, disposable sandbox workspace for a one-shot agent turn that must NOT see the whole
/// repository. The operational-context evolution turn a decision-session Transfer runs (m7
/// <c>UpdateOperationalContext</c>) is scoped here so codex's <c>--cd</c> confines its
/// <c>workspace-write</c> sandbox to just the handful of files copied in, instead of re-exploring the entire
/// repo every transfer (the ~425k-token cost the transfer-cost economics investigation measured). The seam is
/// pluggable so tests can substitute an in-memory workspace and a deployment can relocate the temp root.
/// </summary>
public interface ISandboxWorkspaceFactory
{
    /// <summary>
    /// Creates a fresh, empty workspace directory. <paramref name="label"/> is a human hint used only to make
    /// the temp path recognizable; it is never security-relevant. The caller populates the workspace, runs the
    /// scoped turn against <see cref="ISandboxWorkspace.RootPath"/>, reads results back, then disposes it.
    /// </summary>
    Task<ISandboxWorkspace> CreateAsync(string label, CancellationToken cancellationToken = default);
}
