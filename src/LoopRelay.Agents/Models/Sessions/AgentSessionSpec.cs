using System.Collections.ObjectModel;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Agents.Models.Sessions;

public sealed record AgentSessionSpec
{
    public AgentSessionSpec(
        SessionIdentity sessionId,
        string repositoryId,
        SessionRole role,
        SandboxProfile sandbox,
        EffortProfile effort,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? startupOptions = null,
        string? resumeThreadId = null,
        OperationPermissionProfile? operationPermissionProfile = null)
    {
        SessionId = sessionId;
        RepositoryId = repositoryId;
        Role = role;
        Sandbox = sandbox;
        Effort = effort;
        WorkingDirectory = workingDirectory;
        StartupOptions = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(startupOptions ?? new Dictionary<string, string>(), StringComparer.Ordinal));
        ResumeThreadId = resumeThreadId;
        OperationPermissionProfile = operationPermissionProfile;
    }

    public SessionIdentity SessionId { get; }

    public string RepositoryId { get; }

    public SessionRole Role { get; }

    public SandboxProfile Sandbox { get; }

    public EffortProfile Effort { get; }

    public string? WorkingDirectory { get; }

    public IReadOnlyDictionary<string, string> StartupOptions { get; }

    /// <summary>Codex app-server thread id to resume instead of starting a fresh thread (persistent sessions
    /// only; ignored by one-shots). When set, the handshake runs eagerly at open — see AgentRuntime.</summary>
    public string? ResumeThreadId { get; }

    public OperationPermissionProfile? OperationPermissionProfile { get; }
}
