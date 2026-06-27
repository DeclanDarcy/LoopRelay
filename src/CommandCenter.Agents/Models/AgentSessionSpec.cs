using System.Collections.ObjectModel;

namespace CommandCenter.Agents.Models;

public sealed record AgentSessionSpec
{
    public AgentSessionSpec(
        SessionIdentity sessionId,
        string repositoryId,
        SessionRole role,
        SandboxProfile sandbox,
        EffortProfile effort,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? startupOptions = null)
    {
        SessionId = sessionId;
        RepositoryId = repositoryId;
        Role = role;
        Sandbox = sandbox;
        Effort = effort;
        WorkingDirectory = workingDirectory;
        StartupOptions = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(startupOptions ?? new Dictionary<string, string>(), StringComparer.Ordinal));
    }

    public SessionIdentity SessionId { get; }

    public string RepositoryId { get; }

    public SessionRole Role { get; }

    public SandboxProfile Sandbox { get; }

    public EffortProfile Effort { get; }

    public string? WorkingDirectory { get; }

    public IReadOnlyDictionary<string, string> StartupOptions { get; }
}
