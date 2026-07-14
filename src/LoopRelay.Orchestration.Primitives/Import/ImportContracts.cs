using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Interactions;

namespace LoopRelay.Orchestration.Import;

public enum ImportSourceKind
{
    CanonicalMigrationRequired,
    LegacyContinuityV3,
    PreUnificationRoadmap,
    PlanningArtifacts,
    ExecuteArtifacts,
    CanonicalExportPackage,
    CompositeOwnedWorkspace,
    Unknown,
    Ambiguous,
}

public enum ImportLifecycle
{
    Detected,
    Previewed,
    ApprovalRequired,
    Approved,
    Started,
    Verified,
    EffectsPending,
    Completed,
    RecoveryRequired,
    Refused,
}

public readonly record struct ImportDetectionIdentity(string Value)
{
    public static ImportDetectionIdentity New() => new(CausalUlid.NewId("importdetect"));
    public override string ToString() => Value;
}
public readonly record struct ImportPreviewIdentity(string Value)
{
    public static ImportPreviewIdentity New() => new(CausalUlid.NewId("importpreview"));
    public override string ToString() => Value;
}
public readonly record struct ImportOperationIdentity(string Value)
{
    public static ImportOperationIdentity New() => new(CausalUlid.NewId("import"));
    public override string ToString() => Value;
}
public readonly record struct ImportReceiptIdentity(string Value)
{
    public static ImportReceiptIdentity New() => new(CausalUlid.NewId("importreceipt"));
    public override string ToString() => Value;
}

public sealed record ImportAdapterDescriptor(
    string AdapterIdentity,
    string Version,
    ImportSourceKind SourceKind,
    IReadOnlyList<string> MappedDomains,
    IReadOnlyList<string> IdentityRules,
    IReadOnlyList<string> UnsupportedFields,
    IReadOnlyList<string> FixtureIdentities,
    string RetirementCriterion);

public sealed record ImportDetection(
    ImportDetectionIdentity Identity,
    string RepositoryPath,
    ImportSourceKind SourceKind,
    string SourceFamily,
    string? SourceVersion,
    string SourceFingerprint,
    IReadOnlyList<ImportAdapterDescriptor> Adapters,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> UnsupportedFacts,
    IReadOnlyList<string> Evidence,
    DateTimeOffset DetectedAt)
{
    public bool CanPreview => SourceKind is not (ImportSourceKind.Unknown or ImportSourceKind.Ambiguous) && Conflicts.Count == 0;
}

public sealed record ImportIdentityMapping(
    string Domain,
    string SourceIdentity,
    string? TargetIdentity,
    bool Preserved,
    string Rule,
    string? Conflict);

public sealed record ImportSemanticDelta(
    string Domain,
    int SourceCount,
    int TargetCountBefore,
    int ExpectedTargetCount,
    IReadOnlyList<string> UnknownFields);

public sealed record ImportPreview(
    ImportPreviewIdentity Identity,
    ImportDetection Detection,
    IReadOnlyList<ImportIdentityMapping> Mappings,
    IReadOnlyList<ImportSemanticDelta> SemanticDelta,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> UnsupportedFacts,
    IReadOnlyList<string> UnknownFields,
    InteractionRequestIdentity? ApprovalInteraction,
    DateTimeOffset PreviewedAt)
{
    public bool RequiresHumanDecision => Conflicts.Count > 0 || ApprovalInteraction is not null;
}

public sealed record ImportApproval(
    ImportPreviewIdentity Preview,
    string SourceFingerprint,
    string ApproverIdentity,
    IReadOnlyList<string> AuthorizationEvidence,
    InteractionRequestIdentity? ResolvedInteraction,
    DateTimeOffset ApprovedAt);

public sealed record ImportVerification(
    bool Equivalent,
    IReadOnlyList<string> DomainDiffs,
    string TargetLogicalFingerprint,
    DateTimeOffset VerifiedAt);

public sealed record ImportReceipt(
    ImportReceiptIdentity Identity,
    ImportOperationIdentity Operation,
    ImportPreviewIdentity Preview,
    string SourceFingerprint,
    string TargetLogicalFingerprint,
    IReadOnlyList<string> Evidence,
    DateTimeOffset RecordedAt);

public sealed record ImportAdapterExhaustion(
    string AdapterIdentity,
    string AdapterVersion,
    string PortfolioFingerprint,
    IReadOnlyDictionary<string, string> FixtureReceipts,
    IReadOnlyDictionary<string, string> CanonicalOnlyRuns,
    IReadOnlyDictionary<string, string> DisabledResults,
    DateTimeOffset ExhaustedAt,
    DateTimeOffset? SupersededAt);

public sealed record ImportResult(
    ImportLifecycle Lifecycle,
    ImportDetection? Detection,
    ImportPreview? Preview,
    ImportOperationIdentity? Operation,
    ImportReceipt? Receipt,
    string Explanation,
    IReadOnlyList<string> Evidence);

public interface IImportPortfolioDetector
{
    Task<ImportDetection> DetectAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<ImportPreview> PreviewAsync(ImportDetection detection, CancellationToken cancellationToken = default);
}

public interface IImportGateway
{
    Task<ImportResult> DetectAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<ImportResult> PreviewAsync(ImportDetectionIdentity detection, CancellationToken cancellationToken = default);
    Task<ImportResult> ApproveAsync(ImportApproval approval, CancellationToken cancellationToken = default);
    Task<ImportResult> ExecuteAsync(ImportPreviewIdentity preview, CancellationToken cancellationToken = default);
    Task<ImportResult> VerifyAsync(string importIdentity, CancellationToken cancellationToken = default);
}
