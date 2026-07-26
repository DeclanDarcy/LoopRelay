using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Orchestration.Storage;

/// <summary>
/// The only thing between <see cref="IWorkspaceStorageInspector"/> and <c>RepositoryObserver</c>,
/// and therefore the place that decides which verification tier routine observation pays for. It
/// reads only <c>Health</c>, <c>Exists</c>, <c>Schema</c>, <c>RequiredActions</c>, <c>Evidence</c>,
/// <c>UnresolvedReferences</c> and <c>InterruptedOperations</c> - never the inventory's digests -
/// so it requests <see cref="StorageVerificationDepth.Light"/>. The explicit <c>storage</c>
/// commands go to the inspector directly and keep the deep default.
/// </summary>
public sealed class WorkspaceStorageVerifierAdapter(IWorkspaceStorageInspector? inspector = null) : IStorageVerifier
{
    private readonly IWorkspaceStorageInspector _inspector = inspector ?? new WorkspaceStorageInspector();

    public async Task<StorageVerificationResult> VerifyAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        StorageInspection inspection = await _inspector.VerifyAsync(
            new StorageVerifyRequest(repositoryPath, StorageVerificationDepth.Light), cancellationToken);
        StorageAuthorityKind authority = inspection.Health switch
        {
            StorageHealth.Corrupt => StorageAuthorityKind.Corrupt,
            StorageHealth.Unsupported => StorageAuthorityKind.Unsupported,
            _ when !inspection.Exists => StorageAuthorityKind.Missing,
            _ => StorageAuthorityKind.CanonicalSqlite,
        };
        ResolutionWarning[] warnings = inspection.Health == StorageHealth.Healthy
            ? []
            : [new ResolutionWarning(
                WarningCategory.Storage,
                inspection.Health == StorageHealth.ActionRequired
                    ? "Workspace storage requires an explicit operation."
                    : $"Workspace storage is {inspection.Health.ToString().ToLowerInvariant()}.",
                "workspace storage authority",
                inspection.RequiredActions.FirstOrDefault() ?? "Inspect storage evidence.",
                inspection.Evidence)];
        return new StorageVerificationResult(
            authority,
            inspection.Health == StorageHealth.Healthy,
            [],
            [],
            inspection.Health == StorageHealth.Corrupt ? inspection.Evidence : [],
            inspection.Health == StorageHealth.Unsupported ? inspection.Evidence : [],
            inspection.UnresolvedReferences,
            inspection.InterruptedOperations,
            warnings,
            inspection.Evidence,
            inspection.Health,
            inspection.Schema,
            inspection.RequiredActions);
    }
}
