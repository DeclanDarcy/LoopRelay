using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Core.Services.Artifacts;

public sealed class LogicalArtifactResolver(IEnumerable<ILogicalArtifactProvider> providers) : ILogicalArtifactResolver
{
    private readonly IReadOnlyList<ILogicalArtifactProvider> _providers = providers.ToArray();

    public async Task<LogicalArtifactResolutionResult> ResolveAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        if (!LogicalArtifactPath.TryNormalize(relativePath, out string normalizedPath))
        {
            return LogicalArtifactResolutionResult.Unresolved(
                new LogicalArtifactDescriptor(relativePath, LogicalArtifactDomain.Unknown, LogicalArtifactStorageKind.Unknown),
                LogicalArtifactResolutionStatus.InvalidPath,
                "Logical artifact paths must be repository-relative and must not contain traversal segments.");
        }

        foreach (ILogicalArtifactProvider provider in _providers)
        {
            if (provider.CanResolve(normalizedPath))
            {
                return await provider.ResolveAsync(normalizedPath, cancellationToken);
            }
        }

        return LogicalArtifactResolutionResult.Unresolved(
            new LogicalArtifactDescriptor(normalizedPath, LogicalArtifactDomain.Unknown, LogicalArtifactStorageKind.Unknown),
            LogicalArtifactResolutionStatus.WrongDomain,
            "No logical artifact provider is registered for this path.");
    }
}
