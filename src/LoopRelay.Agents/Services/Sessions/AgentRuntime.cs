using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Agents.Services.Codex;
using LoopRelay.Permissions.Abstractions.Evaluation;

namespace LoopRelay.Agents.Services.Sessions;

public sealed class AgentRuntime(
    IAgentProcessLauncher _launcher,
    IAgentTurnBoundaryDetector _boundaryDetector,
    IAgentTokenEstimator _tokenEstimator,
    AgentSessionRegistry _registry,
    IPermissionGateway? _permissionGateway = null) : IAgentRuntime
{
    // The Codex provider identity lives here, on the provider implementation itself — session
    // evidence reads it from this declaration instead of repeating the literal (D5/M7).
    public AgentRuntimeCapabilities Capabilities { get; } = new(
        Provider: "codex",
        OneShotExecution: true,
        PersistentSessions: true,
        SessionResume: true);

    public async Task<IAgentSession> OpenSessionAsync(
        AgentSessionSpec spec,
        CancellationToken cancellationToken = default)
    {
        IAgentProcess process = await _launcher.LaunchAsync(spec, AgentSessionMode.Persistent, cancellationToken);
        // Held-open sessions speak the Codex app-server JSON-RPC protocol over the process's stdio.
        var session = new CodexAppServerSession(spec, process, _tokenEstimator, _permissionGateway);

        if (!_registry.TryAdd(new AgentSessionKey(spec.RepositoryId, spec.SessionId), session))
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
        if (!await _registry.RemoveAsync(key).ConfigureAwait(false))
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
        IAgentProcess process = await _launcher.LaunchAsync(spec, AgentSessionMode.OneShot, cancellationToken);
        await using var session = new AgentSession(
            spec,
            AgentSessionMode.OneShot,
            process,
            _boundaryDetector,
            _tokenEstimator);

        return await session.RunTurnAsync(prompt, onChunk, cancellationToken);
    }
}
