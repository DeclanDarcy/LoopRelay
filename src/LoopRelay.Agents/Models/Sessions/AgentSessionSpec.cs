using System.Collections.ObjectModel;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Agents.Models.Sessions;

public sealed record AgentSessionSpec
{
    public AgentSessionSpec(
        SessionIdentity sessionId,
        string repositoryId,
        SessionRole role,
        SandboxProfile sandbox,
        AgentModel model,
        AgentEffort effort,
        AgentConfigurationAuthority configurationAuthority,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? startupOptions = null,
        string? resumeThreadId = null,
        OperationPermissionProfile? operationPermissionProfile = null)
    {
        if (string.IsNullOrWhiteSpace(repositoryId))
        {
            throw new ArgumentException("Repository id must not be empty.", nameof(repositoryId));
        }

        if (!Enum.IsDefined(model))
        {
            throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported agent model.");
        }

        if (!Enum.IsDefined(effort))
        {
            throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unsupported agent effort.");
        }

        if (!Enum.IsDefined(configurationAuthority))
        {
            throw new ArgumentOutOfRangeException(
                nameof(configurationAuthority),
                configurationAuthority,
                "Unsupported configuration authority.");
        }

        var options = new Dictionary<string, string>(
            startupOptions ?? new Dictionary<string, string>(),
            StringComparer.Ordinal);
        string? reserved = options.Keys.FirstOrDefault(IsReservedConfigurationOption);
        if (reserved is not null)
        {
            throw new ArgumentException(
                $"Startup option '{reserved}' cannot override canonical model or effort.",
                nameof(startupOptions));
        }

        SessionId = sessionId;
        RepositoryId = repositoryId;
        Role = role;
        Sandbox = sandbox;
        Model = model;
        Effort = effort;
        ConfigurationAuthority = configurationAuthority;
        WorkingDirectory = workingDirectory;
        StartupOptions = new ReadOnlyDictionary<string, string>(
            options);
        ResumeThreadId = resumeThreadId;
        OperationPermissionProfile = operationPermissionProfile;
    }

    public SessionIdentity SessionId { get; }

    public string RepositoryId { get; }

    public SessionRole Role { get; }

    public SandboxProfile Sandbox { get; }

    public AgentModel Model { get; }

    public AgentEffort Effort { get; }

    public AgentConfigurationAuthority ConfigurationAuthority { get; }

    public string? WorkingDirectory { get; }

    public IReadOnlyDictionary<string, string> StartupOptions { get; }

    /// <summary>Codex app-server thread id to resume instead of starting a fresh thread (persistent sessions
    /// only; ignored by one-shots). When set, the handshake runs eagerly at open — see AgentRuntime.</summary>
    public string? ResumeThreadId { get; }

    public OperationPermissionProfile? OperationPermissionProfile { get; }

    private static bool IsReservedConfigurationOption(string key) =>
        string.Equals(key, "model", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "effort", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, "model_reasoning_effort", StringComparison.OrdinalIgnoreCase);
}
