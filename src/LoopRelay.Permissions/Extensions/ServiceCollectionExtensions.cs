using LoopRelay.Permissions.Abstractions;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Services;
using LoopRelay.Permissions.Services.Codex;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LoopRelay.Permissions.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPermissionsCore(this IServiceCollection services) =>
        services.AddPermissionsCore(PermissionPolicyOptions.Default, replacePolicy: false);

    public static IServiceCollection AddPermissionsCore(
        this IServiceCollection services,
        PermissionPolicyOptions policy) =>
        services.AddPermissionsCore(policy, replacePolicy: true);

    private static IServiceCollection AddPermissionsCore(
        this IServiceCollection services,
        PermissionPolicyOptions policy,
        bool replacePolicy)
    {
        PermissionPolicyOptions resolvedPolicy = PermissionPolicyFactory.MergeWithMinimum(policy);
        if (replacePolicy)
        {
            services.AddSingleton(resolvedPolicy);
        }
        else
        {
            services.TryAddSingleton(resolvedPolicy);
        }

        services.TryAddSingleton<ICommandParser, CommandParser>();
        services.TryAddSingleton<ICommandCanonicalizer, CommandCanonicalizer>();
        services.TryAddSingleton<IFingerprintService, Sha256FingerprintService>();
        services.TryAddSingleton<IPermissionCache, InMemoryPermissionCache>();
        services.TryAddSingleton<IPermissionEvaluatorEngine, PermissionEvaluatorEngine>();
        services.TryAddSingleton<IInvariantGuard, InvariantGuard>();
        services.TryAddSingleton<IPermissionHandler, PermissionHandler>();
        services.TryAddSingleton<OperationPermissionHandler>();
        return services;
    }

    public static IServiceCollection AddCodexPermissions(this IServiceCollection services)
    {
        services.AddPermissionsCore();
        services.TryAddSingleton<IPermissionAdapter, CodexPermissionAdapter>();
        services.TryAddSingleton<IPermissionGateway, PermissionGateway>();
        return services;
    }

    public static IServiceCollection AddCodexPermissions(
        this IServiceCollection services,
        PermissionPolicyOptions policy)
    {
        services.AddPermissionsCore(policy);
        services.TryAddSingleton<IPermissionAdapter, CodexPermissionAdapter>();
        services.TryAddSingleton<IPermissionGateway, PermissionGateway>();
        return services;
    }
}
