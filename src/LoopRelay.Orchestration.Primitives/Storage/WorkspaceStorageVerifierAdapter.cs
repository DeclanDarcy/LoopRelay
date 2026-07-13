using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Orchestration.Storage;

public sealed class WorkspaceStorageVerifierAdapter(IWorkspaceStorageInspector? inspector = null) : IStorageVerifier
{
    private readonly IWorkspaceStorageInspector _inspector = inspector ?? new WorkspaceStorageInspector();

    public async Task<StorageVerificationResult> VerifyAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        StorageInspection inspection = await _inspector.VerifyAsync(
            new StorageVerifyRequest(repositoryPath), cancellationToken);
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
