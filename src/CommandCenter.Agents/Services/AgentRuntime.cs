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

        if (spec.ResumeThreadId is not null)
        {
            // Resume verification is EAGER: the caller decides how to prime its first prompt based on whether
            // the resume succeeded, so the outcome must be known before any turn runs. On failure the process is
            // torn down through the single-sited CloseSessionAsync (deregister + dispose) and the typed exception
            // surfaces so the caller can fall back to a fresh, non-resuming open. A normal open stays lazy.
            try
            {
                await session.EnsureReadyAsync(cancellationToken);
            }
            catch (AgentSessionResumeException)
            {
                await CloseSessionAsync(session);
                throw;
            }
            catch (OperationCanceledException)
            {
                await CloseSessionAsync(session);
                throw;
            }
            catch (Exception ex)
            {
                await CloseSessionAsync(session);
                throw new AgentSessionResumeException($"Codex session resume failed: {ex.Message}", ex);
            }
        }

        return session;
    }

    public async ValueTask CloseSessionAsync(IAgentSession session)
    {
        // Single-sited teardown (m10): deregister from the registry AND dispose, so a held-open session is owned
        // in exactly one place. RemoveAsync both removes the entry and disposes the stored session; if the session
        // is not (or no longer) registered — already removed, or never added — fall back to disposing it directly
        // so it is still disposed exactly once.
        var key = new AgentSessionKey(session.RepositoryId, session.SessionId);
        if (!await registry.RemoveAsync(key).ConfigureAwait(false))
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
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
