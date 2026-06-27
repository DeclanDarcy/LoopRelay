using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;

namespace CommandCenter.Agents.Services;

public sealed class AgentRuntime(
    IAgentProcessLauncher launcher,
    IAgentTurnBoundaryDetector boundaryDetector,
    IAgentTokenEstimator tokenEstimator,
    AgentSessionRegistry registry) : IAgentRuntime
{
    public async Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec,
        CancellationToken cancellationToken = default)
    {
        IAgentProcess process = await launcher.LaunchAsync(spec, AgentSessionMode.Persistent, cancellationToken);
        // Held-open sessions speak the Codex app-server JSON-RPC protocol over the process's stdio.
        var session = new CodexAppServerSession(spec, process, tokenEstimator);

        if (!registry.TryAdd(new AgentSessionKey(spec.RepositoryId, spec.SessionId), session))
        {
            await session.DisposeAsync();
            throw new InvalidOperationException(
                $"An agent session already exists for repository '{spec.RepositoryId}' and session '{spec.SessionId}'.");
        }

        return session;
    }

    public async Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        IAgentProcess process = await launcher.LaunchAsync(spec, AgentSessionMode.OneShot, cancellationToken);
        await using var session = new AgentSession(
            spec,
            AgentSessionMode.OneShot,
            process,
            boundaryDetector,
            tokenEstimator);

        return await session.RunTurnAsync(prompt, onChunk, cancellationToken);
    }
}
