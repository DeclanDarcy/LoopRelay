using LoopRelay.DecisionSessions.Extensions;
using LoopRelay.Execution.Extensions;
using LoopRelay.Execution.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LoopRelay.Execution.Tests;

/// <summary>
/// Phase 5 guard (refactor-lazy-sqlite.md, "Removing the four hosted services"): proves the zero-startup payoff
/// at the registration boundary. Kestrel must bind with ZERO per-repo derivation work, so:
/// <list type="number">
///   <item>NONE of the deleted derivation hosted services (decision-session recovery)
///   may be registered as an <see cref="IHostedService"/> any longer — their bodies now
///   live in the on-demand runners reached lazily on first access; and</item>
///   <item>the ONE kept recovery — execution orphan reconciliation — must run via the post-bind
///   <see cref="IHostedLifecycleService.StartedAsync"/> hook (off the pre-Kestrel <c>StartAsync</c> path),
///   guarded by the global recovery ledger.</item>
/// </list>
/// Inspecting the <see cref="ServiceDescriptor"/>s (rather than booting a host) keeps this a pure, fast assertion
/// that needs no <c>IArtifactStore</c>/<c>IRepositoryService</c> host wiring and no process-global mutation, so it
/// is intentionally NOT in the ProcessEnvironment serialized collection.
/// </summary>
public sealed class HostedServiceStartupGuardTests
{
    [Fact]
    public void No_per_repo_derivation_hosted_service_is_registered()
    {
        var services = new ServiceCollection();
        services.AddDecisionSessions();
        services.AddExecution();

        Type[] hostedImplementations = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(HostedImplementationType)
            .Where(type => type is not null)
            .Select(type => type!)
            .ToArray();

        // The derivation hosted services are deleted; none may survive as an IHostedService. (Their types no
        // longer exist, so we match by name to keep this a registration-shape assertion that fails loudly if a
        // future change re-introduces an eagerly-started per-repo derivation service under any of these names.)
        string[] forbiddenNames =
        [
            "DecisionSessionRecoveryHostedService"
        ];

        foreach (string forbidden in forbiddenNames)
        {
            Assert.DoesNotContain(hostedImplementations, type => type.Name == forbidden);
        }
    }

    [Fact]
    public void Execution_recovery_runs_via_the_post_bind_started_lifecycle_hook()
    {
        var services = new ServiceCollection();
        services.AddExecution();

        ServiceDescriptor descriptor = Assert.Single(
            services.Where(candidate =>
                candidate.ServiceType == typeof(IHostedService) &&
                HostedImplementationType(candidate) == typeof(ExecutionSessionRecoveryHostedService)));

        // It must be registered as an IHostedService whose implementation is an IHostedLifecycleService, so the
        // host invokes StartedAsync AFTER Kestrel binds rather than blocking the pre-bind StartAsync path.
        Type implementationType = HostedImplementationType(descriptor)!;
        Assert.True(
            typeof(IHostedLifecycleService).IsAssignableFrom(implementationType),
            "Execution recovery must implement IHostedLifecycleService so it runs from the post-bind StartedAsync hook.");
    }

    private static Type? HostedImplementationType(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationType is not null)
        {
            return descriptor.ImplementationType;
        }

        // AddHostedService<T> sets ImplementationType; a host-supplied instance override is covered here too.
        return descriptor.ImplementationInstance?.GetType();
    }
}
