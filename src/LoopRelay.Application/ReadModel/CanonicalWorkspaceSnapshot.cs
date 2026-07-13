using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoopRelay.Application.ReadModel;

public enum ClaimKnowledge
{
    Known,
    Unknown,
    Conflict,
    Stale,
}

public sealed record CanonicalClaim(
    string Key,
    string? Value,
    ClaimKnowledge Knowledge,
    string Owner,
    IReadOnlyList<string> SourceIdentities,
    string SourceWatermark,
    string ObservedVersion,
    string? Reason = null,
    IReadOnlyList<string>? ConflictingSources = null);

public sealed record OwnerProjectionSection(
    string Owner,
    string Watermark,
    IReadOnlyList<CanonicalClaim> Claims);

public sealed record OwnerProjectionResult(
    OwnerProjectionSection Section,
    string WatermarkBefore,
    string WatermarkAfter,
    bool ExternalObservation = false,
    IReadOnlyList<string>? ExternalObservationIdentities = null);

public interface ICanonicalOwnerProjection
{
    string Owner { get; }
    Task<OwnerProjectionResult> ProjectAsync(CancellationToken cancellationToken = default);
}

public static class CanonicalProjectionOwners
{
    public const string Storage = "StorageAuthority";
    public const string Workflow = "WorkflowAuthority";
    public const string Products = "ProductGateAuthority";
    public const string PolicyRuntime = "PolicyRuntimeAuthority";
    public const string Dispatch = "PromptDispatchAuthority";
    public const string Effects = "EffectCoordinator";
    public const string Recovery = "RecoveryCoordinator";
    public const string Interaction = "InteractionBroker";
    public const string Completion = "CompletionAuthority";
    public const string Certification = "CertificationAuthority";
    public const string Release = "ReleaseEvidenceAuthority";

    public static IReadOnlyList<string> Required { get; } =
    [
        Storage, Workflow, Products, PolicyRuntime, Dispatch, Effects, Recovery, Interaction,
        Completion, Certification, Release,
    ];
}

public sealed record CanonicalWorkspaceSnapshot(
    string WorkspaceIdentity,
    string SchemaIdentity,
    string CatalogIdentity,
    string SnapshotIdentity,
    IReadOnlyList<OwnerProjectionSection> OwnerProjections,
    IReadOnlyList<CanonicalClaim> Conflicts,
    IReadOnlyList<string> ExternalObservationIdentities)
{
    public OwnerProjectionSection Section(string owner) =>
        OwnerProjections.Single(section => section.Owner == owner);
}

public sealed class CanonicalWorkspaceSnapshotComposer(
    IReadOnlyList<ICanonicalOwnerProjection> _projections,
    int _externalRetryLimit = 2)
{
    public async Task<CanonicalWorkspaceSnapshot> ComposeAsync(
        string workspaceIdentity,
        string schemaIdentity,
        string catalogIdentity,
        CancellationToken cancellationToken = default)
    {
        ValidateRegistrations();
        var results = new List<OwnerProjectionResult>(_projections.Count);
        foreach (ICanonicalOwnerProjection projection in _projections.OrderBy(item => item.Owner, StringComparer.Ordinal))
        {
            OwnerProjectionResult result = await projection.ProjectAsync(cancellationToken);
            int retries = 0;
            while (result.ExternalObservation && result.WatermarkBefore != result.WatermarkAfter &&
                retries++ < _externalRetryLimit)
                result = await projection.ProjectAsync(cancellationToken);
            if (result.ExternalObservation && result.WatermarkBefore != result.WatermarkAfter)
            {
                result = result with
                {
                    Section = result.Section with
                    {
                        Claims = result.Section.Claims.Select(claim => claim with
                        {
                            Knowledge = ClaimKnowledge.Stale,
                            Reason = "External observation changed during snapshot composition.",
                        }).ToArray(),
                    },
                };
            }
            ValidateResult(projection, result);
            results.Add(result);
        }

        OwnerProjectionSection[] sections = results.Select(result => result.Section with
            { Claims = result.Section.Claims.OrderBy(claim => claim.Key, StringComparer.Ordinal).ToArray() })
            .OrderBy(section => section.Owner, StringComparer.Ordinal).ToArray();
        CanonicalClaim[] conflicts = sections.SelectMany(section => section.Claims)
            .GroupBy(claim => claim.Key, StringComparer.Ordinal)
            .Where(group => group.Select(claim => claim.Value).Distinct(StringComparer.Ordinal).Count() > 1 &&
                group.All(claim => claim.Knowledge == ClaimKnowledge.Known))
            .Select(group => new CanonicalClaim(group.Key, null, ClaimKnowledge.Conflict,
                "CanonicalWorkspaceSnapshotComposer",
                group.SelectMany(claim => claim.SourceIdentities).Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal).ToArray(),
                string.Join('|', group.Select(claim => claim.SourceWatermark).Order(StringComparer.Ordinal)),
                string.Join('|', group.Select(claim => claim.ObservedVersion).Order(StringComparer.Ordinal)),
                "Owner projections disagree; the composer did not choose a winner.",
                group.Select(claim => $"{claim.Owner}:{claim.Value}").Order(StringComparer.Ordinal).ToArray()))
            .OrderBy(claim => claim.Key, StringComparer.Ordinal).ToArray();
        string[] external = results.SelectMany(result => result.ExternalObservationIdentities ?? [])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string identityDocument = JsonSerializer.Serialize(new
        {
            workspaceIdentity, schemaIdentity, catalogIdentity,
            watermarks = sections.Select(section => new { section.Owner, section.Watermark }),
            external,
        });
        string snapshotIdentity = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(identityDocument)));
        return new(workspaceIdentity, schemaIdentity, catalogIdentity, snapshotIdentity,
            sections, conflicts, external);
    }

    private void ValidateRegistrations()
    {
        string[] duplicates = _projections.GroupBy(item => item.Owner, StringComparer.Ordinal)
            .Where(group => group.Count() != 1).Select(group => group.Key).Order(StringComparer.Ordinal).ToArray();
        string[] missing = CanonicalProjectionOwners.Required.Except(
            _projections.Select(item => item.Owner), StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        string[] unknown = _projections.Select(item => item.Owner).Except(
            CanonicalProjectionOwners.Required, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0 || duplicates.Length > 0 || unknown.Length > 0)
            throw new InvalidOperationException(
                $"Canonical projection registration is invalid. missing=[{string.Join(',', missing)}] " +
                $"duplicates=[{string.Join(',', duplicates)}] unknown=[{string.Join(',', unknown)}]");
    }

    private static void ValidateResult(ICanonicalOwnerProjection projection, OwnerProjectionResult result)
    {
        if (result.Section.Owner != projection.Owner || string.IsNullOrWhiteSpace(result.Section.Watermark))
            throw new InvalidOperationException($"Projection {projection.Owner} returned invalid owner/watermark metadata.");
        foreach (CanonicalClaim claim in result.Section.Claims)
        {
            if (claim.Owner != projection.Owner || claim.SourceIdentities.Count == 0 ||
                string.IsNullOrWhiteSpace(claim.SourceWatermark) || string.IsNullOrWhiteSpace(claim.ObservedVersion) ||
                (claim.Knowledge != ClaimKnowledge.Known && string.IsNullOrWhiteSpace(claim.Reason)))
                throw new InvalidOperationException($"Projection {projection.Owner} returned an untraceable claim `{claim.Key}`.");
        }
    }
}

public static class CanonicalWorkspaceSnapshotRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string RenderJson(CanonicalWorkspaceSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal);

    public static string RenderText(CanonicalWorkspaceSnapshot snapshot)
    {
        var lines = new List<string>
        {
            $"Workspace: {snapshot.WorkspaceIdentity}",
            $"Schema: {snapshot.SchemaIdentity}",
            $"Catalog: {snapshot.CatalogIdentity}",
            $"Snapshot: {snapshot.SnapshotIdentity}",
        };
        foreach (OwnerProjectionSection section in snapshot.OwnerProjections)
        {
            lines.Add($"[{section.Owner}] watermark={section.Watermark}");
            lines.AddRange(section.Claims.Select(claim =>
                $"{claim.Key}={claim.Value ?? "<unknown>"} ({claim.Knowledge}) sources={string.Join(',', claim.SourceIdentities)}" +
                (claim.Reason is null ? string.Empty : $" reason={claim.Reason}")));
        }
        return string.Join('\n', lines) + "\n";
    }
}
