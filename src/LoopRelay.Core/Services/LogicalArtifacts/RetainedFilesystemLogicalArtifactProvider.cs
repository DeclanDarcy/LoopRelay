using LoopRelay.Core.Abstractions.Artifacts;

namespace LoopRelay.Core.Services.Artifacts;

public sealed class RetainedFilesystemLogicalArtifactProvider : ILogicalArtifactProvider
{
    private readonly IArtifactStore _store;
    private readonly IReadOnlyDictionary<string, LogicalArtifactDomain> _exactPaths;
    private readonly IReadOnlyList<LogicalArtifactPathPattern> _patterns;

    public RetainedFilesystemLogicalArtifactProvider(
        IArtifactStore store,
        IEnumerable<string> exactPaths,
        IEnumerable<LogicalArtifactPathPattern>? patterns = null)
        : this(
            store,
            exactPaths.ToDictionary(
                LogicalArtifactPath.NormalizeKnownRelative,
                _ => LogicalArtifactDomain.RetainedFile,
                StringComparer.OrdinalIgnoreCase),
            patterns)
    {
    }

    public RetainedFilesystemLogicalArtifactProvider(
        IArtifactStore store,
        IReadOnlyDictionary<string, LogicalArtifactDomain> exactPaths,
        IEnumerable<LogicalArtifactPathPattern>? patterns = null)
    {
        _store = store;
        _exactPaths = exactPaths.ToDictionary(
            pair => LogicalArtifactPath.NormalizeKnownRelative(pair.Key),
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);
        _patterns = (patterns ?? []).ToArray();
    }

    public bool CanResolve(string relativePath)
    {
        string normalizedPath = LogicalArtifactPath.NormalizeKnownRelative(relativePath);
        return _exactPaths.ContainsKey(normalizedPath) ||
            _patterns.Any(pattern => LogicalArtifactPath.MatchesPattern(
                normalizedPath,
                pattern.Directory,
                pattern.SearchPattern));
    }

    public async Task<LogicalArtifactResolutionResult> ResolveAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalizedPath = LogicalArtifactPath.NormalizeKnownRelative(relativePath);
        LogicalArtifactDescriptor descriptor = DescriptorFor(normalizedPath);
        string? content = await _store.ReadAsync(normalizedPath);
        if (content is null)
        {
            return LogicalArtifactResolutionResult.Unresolved(
                descriptor,
                LogicalArtifactResolutionStatus.MissingRetainedFile,
                $"Retained filesystem artifact is missing: {normalizedPath}");
        }

        return LogicalArtifactResolutionResult.Resolved(descriptor, content);
    }

    private LogicalArtifactDescriptor DescriptorFor(string normalizedPath)
    {
        if (_exactPaths.TryGetValue(normalizedPath, out LogicalArtifactDomain exactDomain))
        {
            return new LogicalArtifactDescriptor(
                normalizedPath,
                exactDomain,
                LogicalArtifactStorageKind.RetainedFilesystem,
                normalizedPath);
        }

        LogicalArtifactPathPattern pattern = _patterns.First(item => LogicalArtifactPath.MatchesPattern(
            normalizedPath,
            item.Directory,
            item.SearchPattern));

        string identity = string.IsNullOrWhiteSpace(pattern.IdentityPrefix)
            ? normalizedPath
            : $"{pattern.IdentityPrefix}:{normalizedPath}";
        return new LogicalArtifactDescriptor(
            normalizedPath,
            pattern.Domain,
            LogicalArtifactStorageKind.RetainedFilesystem,
            identity);
    }
}
