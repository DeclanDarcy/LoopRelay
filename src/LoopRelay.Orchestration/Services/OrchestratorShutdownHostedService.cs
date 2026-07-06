using Microsoft.Extensions.Hosting;

namespace LoopRelay.Orchestration.Services;

/// <summary>
/// App-shutdown hook (m10): on a graceful host stop, dispose the orchestrator registry — which tears down every
/// live <see cref="RepositoryOrchestrator"/> (planning/decision sessions) — and the agent-session registry, so
/// held-open codex app-server processes are released rather than orphaned. Disposal is the only work; there is no
/// background loop, so <see cref="StartAsync"/> is a no-op. <see cref="StopAsync"/> is best-effort and bounded by
/// the host's stop token so it can never hang shutdown: a slow/faulting teardown is swallowed, not propagated.
///
/// This is additive belt-and-suspenders over DI container disposal (the registries are <c>IAsyncDisposable</c>
/// singletons the container also disposes). The host invokes IHostedService.StopAsync BEFORE disposing the
/// container, so a clean stop releases the processes deterministically here; the container dispose remains the
/// fallback. (A hard crash/SIGKILL still bypasses both — that is the OS process-tree's responsibility.)
/// </summary>
public sealed class OrchestratorShutdownHostedService(
    RepositoryOrchestratorRegistry orchestratorRegistry) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // RepositoryOrchestratorRegistry.DisposeAsync removes+disposes every orchestrator, and each
            // orchestrator's DisposeAsync tears down its held-open sessions (which now also deregister from the
            // AgentSessionRegistry via IAgentRuntime.CloseSessionAsync), so disposing the orchestrator registry
            // covers the agent sessions transitively. Bounded by the host stop token so a slow teardown cannot
            // hang shutdown.
            await orchestratorRegistry.DisposeAsync().AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort teardown on shutdown: a slow or faulting dispose (or the stop token firing) must never
            // block or fault host shutdown. The container dispose remains the fallback.
        }
    }
}
