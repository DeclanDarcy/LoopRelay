using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Orchestration.Policy;

/// <summary>
/// The layer that supplied a resolved policy value. Layered provenance is capped at exactly
/// these three layers; recognized environment variables and `--policy` flags are both
/// invocation-scoped inputs, with an explicit flag beating an ambient environment variable.
/// </summary>
public enum PolicyLayer
{
    BuiltIn,
    Workspace,
    Invocation,
}

public sealed record PolicyFieldProvenance(string Field, PolicyLayer Layer, string Origin);

/// <summary>
/// One invocation-layer override. <paramref name="IsExplicit"/> distinguishes explicit flags
/// (`--policy key=value`) from ambient environment variables; within one key an explicit
/// override wins over an ambient one, and duplicate overrides of the same explicitness are a
/// rejected conflict.
/// </summary>
public sealed record PolicyOverride(string Key, string Value, string Origin, bool IsExplicit);

/// <summary>
/// The single resolved, versioned operational policy an invocation executes under. Resolved
/// once per invocation; every consumer observes this one instance, and every attempt records
/// <see cref="PolicyId"/>. <see cref="ResolvedJson"/> is the canonical serialization the
/// identity hash covers — it is what the policy-resolution fact stores.
/// </summary>
public sealed record ResolvedOperationalPolicy(
    string PolicyId,
    string SchemaVersion,
    NonImplementationArtifactPolicyOptions ArtifactPolicy,
    int MaxUnboundedContinuationSteps,
    int MaxNoChangesCommits,
    int OperationalContextGrowthWarningStreak,
    bool DecisionSessionResume,
    PermissionPolicyOptions Permissions,
    IReadOnlyList<PolicyFieldProvenance> Provenance,
    string ResolvedJson,
    string SourceDescription);

public sealed class PolicyResolutionException : Exception
{
    public PolicyResolutionException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Resolves the operational policy from exactly three layers: built-in defaults, the workspace
/// settings document, and invocation overrides. A configured value is either demonstrably
/// effective or explicitly rejected — unknown keys, malformed values, duplicate overrides, and
/// consumer-less toggles all reject resolution instead of being silently ignored.
/// </summary>
public static class OperationalPolicyResolver
{
    public const string SchemaVersion = "policy-v1";

    public const string AllowHitlRequestedNonImplementationFilesKey = "artifactPolicy.allowHitlRequestedNonImplementationFiles";
    public const string AllowAuxiliaryNonImplementationFilesKey = "artifactPolicy.allowAuxiliaryNonImplementationFiles";
    public const string MaxUnboundedContinuationStepsKey = "execution.maxUnboundedContinuationSteps";
    public const string MaxNoChangesCommitsKey = "execution.maxNoChangesCommits";
    public const string OperationalContextGrowthWarningStreakKey = "execution.operationalContextGrowthWarningStreak";
    public const string DecisionSessionResumeKey = "decisions.sessionResume";

    public const bool DefaultAllowHitlRequestedNonImplementationFiles = false;
    public const bool DefaultAllowAuxiliaryNonImplementationFiles = false;
    public const int DefaultMaxUnboundedContinuationSteps = 32;
    public const int DefaultMaxNoChangesCommits = 2;
    public const int DefaultOperationalContextGrowthWarningStreak = 2;
    public const bool DefaultDecisionSessionResume = true;

    private const string BuiltInOrigin = "built-in";

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new(JsonSerializerDefaults.Web);

    public static ResolvedOperationalPolicy Resolve(
        CliPolicyDocument workspacePolicy,
        string workspaceSource,
        IReadOnlyList<PolicyOverride> invocationOverrides,
        PermissionPolicyOptions permissions)
    {
        ArgumentNullException.ThrowIfNull(workspacePolicy);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceSource);
        ArgumentNullException.ThrowIfNull(invocationOverrides);
        ArgumentNullException.ThrowIfNull(permissions);

        IReadOnlyDictionary<string, PolicyOverride> overrides = IndexOverrides(invocationOverrides);
        List<PolicyFieldProvenance> provenance = [];

        bool allowHitl = ResolveBool(
            AllowHitlRequestedNonImplementationFilesKey,
            DefaultAllowHitlRequestedNonImplementationFiles,
            workspacePolicy.AllowHitlRequestedNonImplementationFiles,
            workspaceSource,
            overrides,
            provenance);
        bool allowAuxiliary = ResolveBool(
            AllowAuxiliaryNonImplementationFilesKey,
            DefaultAllowAuxiliaryNonImplementationFiles,
            workspacePolicy.AllowAuxiliaryNonImplementationFiles,
            workspaceSource,
            overrides,
            provenance);
        int maxUnboundedContinuationSteps = ResolvePositiveInt(
            MaxUnboundedContinuationStepsKey,
            DefaultMaxUnboundedContinuationSteps,
            workspacePolicy.MaxUnboundedContinuationSteps,
            workspaceSource,
            overrides,
            provenance);
        int maxNoChangesCommits = ResolvePositiveInt(
            MaxNoChangesCommitsKey,
            DefaultMaxNoChangesCommits,
            workspacePolicy.MaxNoChangesCommits,
            workspaceSource,
            overrides,
            provenance);
        int operationalContextGrowthWarningStreak = ResolvePositiveInt(
            OperationalContextGrowthWarningStreakKey,
            DefaultOperationalContextGrowthWarningStreak,
            workspacePolicy.OperationalContextGrowthWarningStreak,
            workspaceSource,
            overrides,
            provenance);
        bool decisionSessionResume = ResolveBool(
            DecisionSessionResumeKey,
            DefaultDecisionSessionResume,
            workspacePolicy.DecisionSessionResume,
            workspaceSource,
            overrides,
            provenance);

        foreach (string key in overrides.Keys)
        {
            if (!KnownKeys.Contains(key))
            {
                throw new PolicyResolutionException(
                    $"Unknown policy override key `{key}`. Known keys: {string.Join(", ", KnownKeys)}.");
            }
        }

        if (allowAuxiliary)
        {
            throw new PolicyResolutionException(
                $"`{AllowAuxiliaryNonImplementationFilesKey}` is configured `true` but has no consumer on the " +
                "canonical path; the value would have no production effect. Remove it (or set it to `false`) " +
                "until the plan-review surface converges.");
        }

        NonImplementationArtifactPolicyOptions artifactPolicy = new(allowHitl, allowAuxiliary);
        string resolvedJson = JsonSerializer.Serialize(
            new CanonicalPolicySnapshot(
                SchemaVersion,
                new CanonicalArtifactPolicy(allowHitl, allowAuxiliary),
                new CanonicalExecutionPolicy(
                    maxUnboundedContinuationSteps,
                    maxNoChangesCommits,
                    operationalContextGrowthWarningStreak),
                new CanonicalDecisionsPolicy(decisionSessionResume),
                CanonicalPermissions.From(permissions)),
            CanonicalJsonOptions);
        string policyId = ComputePolicyId(resolvedJson);

        return new ResolvedOperationalPolicy(
            policyId,
            SchemaVersion,
            artifactPolicy,
            maxUnboundedContinuationSteps,
            maxNoChangesCommits,
            operationalContextGrowthWarningStreak,
            decisionSessionResume,
            permissions,
            provenance,
            resolvedJson,
            workspaceSource);
    }

    private static readonly IReadOnlyList<string> KnownKeys =
    [
        AllowHitlRequestedNonImplementationFilesKey,
        AllowAuxiliaryNonImplementationFilesKey,
        MaxUnboundedContinuationStepsKey,
        MaxNoChangesCommitsKey,
        OperationalContextGrowthWarningStreakKey,
        DecisionSessionResumeKey,
    ];

    private static IReadOnlyDictionary<string, PolicyOverride> IndexOverrides(
        IReadOnlyList<PolicyOverride> invocationOverrides)
    {
        Dictionary<string, PolicyOverride> indexed = new(StringComparer.Ordinal);
        foreach (IGrouping<string, PolicyOverride> group in invocationOverrides.GroupBy(o => o.Key, StringComparer.Ordinal))
        {
            List<PolicyOverride> explicitOverrides = group.Where(o => o.IsExplicit).ToList();
            List<PolicyOverride> ambientOverrides = group.Where(o => !o.IsExplicit).ToList();
            if (explicitOverrides.Count > 1 || ambientOverrides.Count > 1)
            {
                throw new PolicyResolutionException(
                    $"Conflicting policy overrides for `{group.Key}`: " +
                    $"{string.Join(", ", group.Select(o => $"{o.Origin}={o.Value}"))}. " +
                    "Give each policy key at most once per source.");
            }

            indexed[group.Key] = explicitOverrides.Count == 1 ? explicitOverrides[0] : ambientOverrides[0];
        }

        return indexed;
    }

    private static bool ResolveBool(
        string key,
        bool builtIn,
        bool? workspace,
        string workspaceSource,
        IReadOnlyDictionary<string, PolicyOverride> overrides,
        List<PolicyFieldProvenance> provenance)
    {
        (string raw, PolicyLayer layer, string origin, bool resolvedFromText) = SelectLayer(
            key, workspace?.ToString(), workspaceSource, overrides);
        if (!resolvedFromText)
        {
            provenance.Add(new PolicyFieldProvenance(key, PolicyLayer.BuiltIn, BuiltInOrigin));
            return builtIn;
        }

        bool value = raw.ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => throw new PolicyResolutionException(
                $"Policy value `{raw}` for `{key}` (from {origin}) is not a boolean; expected true/false."),
        };
        provenance.Add(new PolicyFieldProvenance(key, layer, origin));
        return value;
    }

    private static int ResolvePositiveInt(
        string key,
        int builtIn,
        int? workspace,
        string workspaceSource,
        IReadOnlyDictionary<string, PolicyOverride> overrides,
        List<PolicyFieldProvenance> provenance)
    {
        (string raw, PolicyLayer layer, string origin, bool resolvedFromText) = SelectLayer(
            key, workspace?.ToString(CultureInfo.InvariantCulture), workspaceSource, overrides);
        if (!resolvedFromText)
        {
            provenance.Add(new PolicyFieldProvenance(key, PolicyLayer.BuiltIn, BuiltInOrigin));
            return builtIn;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 1)
        {
            throw new PolicyResolutionException(
                $"Policy value `{raw}` for `{key}` (from {origin}) is not a positive integer.");
        }

        provenance.Add(new PolicyFieldProvenance(key, layer, origin));
        return value;
    }

    private static (string Raw, PolicyLayer Layer, string Origin, bool Resolved) SelectLayer(
        string key,
        string? workspaceValue,
        string workspaceSource,
        IReadOnlyDictionary<string, PolicyOverride> overrides)
    {
        if (overrides.TryGetValue(key, out PolicyOverride? invocation))
        {
            return (invocation.Value, PolicyLayer.Invocation, invocation.Origin, true);
        }

        if (workspaceValue is not null)
        {
            return (workspaceValue, PolicyLayer.Workspace, workspaceSource, true);
        }

        return (string.Empty, PolicyLayer.BuiltIn, BuiltInOrigin, false);
    }

    private static string ComputePolicyId(string resolvedJson)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(resolvedJson));
        string hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"pol_v1_{hex[..32]}";
    }

    // The identity hash covers the canonical serialization of every effective value, including
    // the permissions snapshot — a policy-affecting configuration outside the versioned
    // identity would be a silently-chosen policy. Provenance is evidence, not identity: the
    // same effective values yield the same policy identity regardless of which layer supplied
    // each value.
    private sealed record CanonicalPolicySnapshot(
        string SchemaVersion,
        CanonicalArtifactPolicy ArtifactPolicy,
        CanonicalExecutionPolicy Execution,
        CanonicalDecisionsPolicy Decisions,
        CanonicalPermissions Permissions);

    private sealed record CanonicalArtifactPolicy(
        bool AllowHitlRequestedNonImplementationFiles,
        bool AllowAuxiliaryNonImplementationFiles);

    private sealed record CanonicalExecutionPolicy(
        int MaxUnboundedContinuationSteps,
        int MaxNoChangesCommits,
        int OperationalContextGrowthWarningStreak);

    private sealed record CanonicalDecisionsPolicy(bool SessionResume);

    // The permission options hold hash-ordered sets, and .NET randomizes string hashing per
    // process — serializing them directly would give the same configuration a different policy
    // identity on every invocation. The canonical form sorts every collection so identical
    // content always hashes identically.
    private sealed record CanonicalPermissions(
        string FingerprintVersion,
        string[] CommandsWithSubcommands,
        string[] SafeTools,
        string[] SafeBashCommands,
        CanonicalHardDeny HardDeny,
        CanonicalReviewRequired ReviewRequired,
        CanonicalAllow Allow)
    {
        public static CanonicalPermissions From(PermissionPolicyOptions permissions) =>
            new(
                permissions.FingerprintVersion,
                Sorted(permissions.CommandsWithSubcommands),
                Sorted(permissions.SafeTools),
                Sorted(permissions.SafeBashCommands),
                new CanonicalHardDeny(
                    Sorted(permissions.HardDeny.PrivilegeEscalationCommands),
                    permissions.HardDeny.RecursiveForceDelete.Command,
                    permissions.HardDeny.RecursiveForceDelete.FlagSets
                        .Select(Sorted)
                        .OrderBy(flagSet => string.Join("|", flagSet), StringComparer.Ordinal)
                        .ToArray(),
                    Sorted(permissions.HardDeny.SystemControlCommands),
                    Sorted(permissions.HardDeny.NetworkFetchCommands),
                    Sorted(permissions.HardDeny.GitForcePushFlags),
                    Sorted(permissions.HardDeny.IndirectShellExecution.Commands),
                    permissions.HardDeny.IndirectShellExecution.Flag),
                new CanonicalReviewRequired(
                    permissions.ReviewRequired.GitCommit,
                    Sorted(permissions.ReviewRequired.GitCommitAmendFlags),
                    permissions.ReviewRequired.GitPush,
                    Sorted(permissions.ReviewRequired.InstallCommands),
                    permissions.ReviewRequired.InstallSubcommand,
                    Sorted(permissions.ReviewRequired.InfrastructureCommands)),
                new CanonicalAllow(
                    Sorted(permissions.Allow.AlwaysAllowedCommands),
                    Sorted(permissions.Allow.GitReadOnlySubcommands),
                    Sorted(permissions.Allow.GitLogAllowedUnlessFlags),
                    Sorted(permissions.Allow.DotnetAllowedSubcommands),
                    Sorted(permissions.Allow.PackageManagerAllowedSubcommands),
                    Sorted(permissions.Allow.TestCommands)));

        private static string[] Sorted(IEnumerable<string> values) =>
            values.OrderBy(value => value, StringComparer.Ordinal).ToArray();

        private static SortedDictionary<string, string[]> Sorted(
            IReadOnlyDictionary<string, IReadOnlySet<string>> map)
        {
            SortedDictionary<string, string[]> sorted = new(StringComparer.Ordinal);
            foreach ((string key, IReadOnlySet<string> values) in map)
            {
                sorted[key] = Sorted(values);
            }

            return sorted;
        }
    }

    private sealed record CanonicalHardDeny(
        string[] PrivilegeEscalationCommands,
        string RecursiveForceDeleteCommand,
        string[][] RecursiveForceDeleteFlagSets,
        string[] SystemControlCommands,
        string[] NetworkFetchCommands,
        string[] GitForcePushFlags,
        string[] IndirectShellExecutionCommands,
        string IndirectShellExecutionFlag);

    private sealed record CanonicalReviewRequired(
        bool GitCommit,
        string[] GitCommitAmendFlags,
        bool GitPush,
        string[] InstallCommands,
        string InstallSubcommand,
        string[] InfrastructureCommands);

    private sealed record CanonicalAllow(
        string[] AlwaysAllowedCommands,
        string[] GitReadOnlySubcommands,
        string[] GitLogAllowedUnlessFlags,
        string[] DotnetAllowedSubcommands,
        SortedDictionary<string, string[]> PackageManagerAllowedSubcommands,
        SortedDictionary<string, string[]> TestCommands);
}
