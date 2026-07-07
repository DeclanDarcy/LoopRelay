using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Codex;
using LoopRelay.Permissions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoopRelay.Permissions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionsCore(this IServiceCollection services)
    {
        services.TryAddSingleton<ICommandParser, CommandParser>();
        services.TryAddSingleton<ICommandCanonicalizer, CommandCanonicalizer>();
        services.TryAddSingleton<IFingerprintService, Sha256FingerprintService>();
        services.TryAddSingleton<IPermissionCache, InMemoryPermissionCache>();
        services.TryAddSingleton<IPermissionEvaluatorEngine, PermissionEvaluatorEngine>();
        services.TryAddSingleton<IInvariantGuard, InvariantGuard>();
        services.TryAddSingleton<IPermissionHandler, PermissionHandler>();
        return services;
    }

    public static IServiceCollection AddCodexPermissions(this IServiceCollection services)
    {
        services.AddPermissionsCore();
        services.TryAddSingleton<IPermissionAdapter, CodexPermissionAdapter>();
        services.TryAddSingleton<IPermissionGateway, PermissionGateway>();
        return services;
    }
}
