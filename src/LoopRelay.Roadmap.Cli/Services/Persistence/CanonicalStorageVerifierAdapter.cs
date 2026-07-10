using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using CanonicalStorageVerificationResult = LoopRelay.Orchestration.Resolution.StorageVerificationResult;
using RoadmapStorageVerificationResult = LoopRelay.Roadmap.Cli.Services.Persistence.WorkspaceVerificationResult;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal sealed class CanonicalStorageVerifierAdapter(
    IWorkspaceVerificationService _workspaceVerification,
    WorkspaceVerificationOptions? _options = null,
    IArtifactStore? _store = null) : IStorageVerifier
{
    private readonly IStorageVerifier _fallbackAuthorityVerifier = new FileSystemStorageVerifier();
    private readonly IArtifactStore _store = _store ?? new FileSystemArtifactStore();

    public async Task<CanonicalStorageVerificationResult> VerifyAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repositoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            Path = Path.GetFullPath(repositoryPath),
        };
        var artifacts = new RoadmapArtifacts(_store, repository);
        RoadmapStorageVerificationResult roadmap =
            await _workspaceVerification.VerifyAsync(artifacts, _options, cancellationToken);
        CanonicalStorageVerificationResult authority =
            await _fallbackAuthorityVerifier.VerifyAsync(repository.Path, cancellationToken);

        if (roadmap.Success)
        {
            return authority;
        }

        IReadOnlyList<string> staleExports = Findings(
            roadmap,
            WorkspaceVerificationFindingKind.StaleExport,
            WorkspaceVerificationFindingKind.MissingExport);
        IReadOnlyList<string> conflicts = Findings(
            roadmap,
            WorkspaceVerificationFindingKind.Conflict,
            WorkspaceVerificationFindingKind.DuplicateIdentity);
        IReadOnlyList<string> corruption = Findings(
            roadmap,
            WorkspaceVerificationFindingKind.CorruptDomain,
            WorkspaceVerificationFindingKind.NondeterministicRoundTrip,
            WorkspaceVerificationFindingKind.UnrecoverableArchive);
        IReadOnlyList<string> unsupported = Findings(roadmap, WorkspaceVerificationFindingKind.UnsupportedVersion);
        IReadOnlyList<string> unresolved = Findings(
            roadmap,
            WorkspaceVerificationFindingKind.UnresolvedPath,
            WorkspaceVerificationFindingKind.InvalidReference,
            WorkspaceVerificationFindingKind.OrphanedArtifact);
        IReadOnlyList<string> partialTransactions = Findings(
            roadmap,
            WorkspaceVerificationFindingKind.MutationRequired);
        IReadOnlyList<ResolutionBlocker> blockers = roadmap.Findings
            .Select(ToBlocker)
            .ToArray();

        StorageAuthorityKind authorityKind = corruption.Count > 0
            ? StorageAuthorityKind.Corrupt
            : unsupported.Count > 0
                ? StorageAuthorityKind.Unsupported
                : conflicts.Count > 0
                    ? StorageAuthorityKind.Ambiguous
                    : authority.Authority;

        return new CanonicalStorageVerificationResult(
            authorityKind,
            UsableAuthority: false,
            staleExports,
            conflicts,
            corruption,
            unsupported,
            unresolved,
            partialTransactions,
            blockers,
            roadmap.Findings.Select(finding => finding.Identity).Distinct(StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<string> Findings(
        RoadmapStorageVerificationResult result,
        params WorkspaceVerificationFindingKind[] kinds)
    {
        var set = kinds.ToHashSet();
        return result.Findings
            .Where(finding => set.Contains(finding.Kind))
            .Select(finding => finding.Identity)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static ResolutionBlocker ToBlocker(WorkspaceVerificationFinding finding) =>
        new(
            ToCategory(finding.Kind),
            $"{finding.Domain}:{finding.Rule}: {finding.CurrentState}",
            "roadmap workspace verification",
            finding.RecommendedAction,
            Recoverable: finding.Kind != WorkspaceVerificationFindingKind.UnsupportedVersion,
            [finding.Identity]);

    private static BlockerCategory ToCategory(WorkspaceVerificationFindingKind kind) =>
        kind switch
        {
            WorkspaceVerificationFindingKind.MutationRequired => BlockerCategory.Recovery,
            WorkspaceVerificationFindingKind.InvalidReference or WorkspaceVerificationFindingKind.UnresolvedPath => BlockerCategory.Validation,
            WorkspaceVerificationFindingKind.Conflict or WorkspaceVerificationFindingKind.DuplicateIdentity => BlockerCategory.Repository,
            _ => BlockerCategory.Storage,
        };
}
