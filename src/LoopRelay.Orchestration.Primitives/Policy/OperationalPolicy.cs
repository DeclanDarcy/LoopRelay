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

public sealed record PolicyFieldProvenance(string Field, PolicyLayer Layer, string Origin)
{
    public string? ConfiguredValue { get; init; }

    public required string EffectiveValue { get; init; }

    public required string Reason { get; init; }

    public IReadOnlyList<string> OverrideChain { get; init; } = [];
}

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
public sealed record ResolvedExecutionPolicy(
    int MaxUnboundedContinuationSteps,
    int MaxNoChangesCommits,
    int OperationalContextGrowthWarningStreak);

public enum TelemetryPolicyMode
{
    Disabled,
    Enabled,
    Required,
}

public sealed record TelemetryPolicy(TelemetryPolicyMode Mode);

public enum InputWaitPolicyMode
{
    Disabled,
    Enabled,
}

public sealed record InputWaitPolicy(InputWaitPolicyMode Mode);

public sealed record RuntimePolicy(
    TelemetryPolicy Telemetry,
    InputWaitPolicy InputWait);

public enum UsageLimitRetryMode
{
    Never,
    Bounded,
    Unlimited,
}

public sealed record UsageLimitRetryPolicy(
    UsageLimitRetryMode Mode,
    int MaxAttempts);

public enum UnknownOutcomePolicy
{
    RequiresReconciliation,
}

public sealed record RecoveryPolicy(
    ResumePolicy Resume,
    UsageLimitRetryPolicy UsageLimitRetry,
    UnknownOutcomePolicy UnknownOutcome);

public sealed record ResolvedOperationalPolicy(
    string PolicyId,
    string PolicySchemaVersion,
    ResolvedExecutionPolicy Execution,
    RuntimePolicy Runtime,
    RecoveryPolicy Recovery,
    PermissionPolicyOptions Permissions,
    IReadOnlyList<PolicyFieldProvenance> Provenance,
    string ResolvedJson,
    string SourceDescription)
{
    public string SchemaVersion => PolicySchemaVersion;
    public int MaxUnboundedContinuationSteps => Execution.MaxUnboundedContinuationSteps;
    public int MaxNoChangesCommits => Execution.MaxNoChangesCommits;
    public int OperationalContextGrowthWarningStreak => Execution.OperationalContextGrowthWarningStreak;
    public bool DecisionSessionResume => Recovery.Resume.Enabled;
    public ResumePolicy Resume => Recovery.Resume;
    public bool SessionTelemetry => Runtime.Telemetry.Mode != TelemetryPolicyMode.Disabled;
    public bool UsageLimitWaitRetry => Recovery.UsageLimitRetry.Mode != UsageLimitRetryMode.Never;
    public bool InputWaitReporting => Runtime.InputWait.Mode != InputWaitPolicyMode.Disabled;
}

public enum ResumeRecoveryStrategy
{
    ResumeOnly,
    Reconstructed,
    Certified,
}

public sealed record ResumePolicy(
    bool Enabled,
    ResumeRecoveryStrategy RecoveryStrategy)
{
    public static ResumePolicy Create(bool enabled, ResumeRecoveryStrategy strategy) =>
        new(enabled, strategy);
}

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
/// effective or explicitly rejected — unknown keys, malformed values, and duplicate overrides
/// all reject resolution instead of being silently ignored.
/// Schema policy-v2 (M6): the artifactPolicy toggles are removed by owner ruling — the
/// implementation-first prompt policy is template-owned and unconditional, so there is no
/// artifact-policy field to configure; a settings file or override that still names one is
/// rejected as an unknown key.
/// Schema policy-v4 consolidates recovery/resume and the M7 runtime controls as typed components
/// under one identity. Every execution-affecting value is represented in the canonical document;
/// compatibility inputs are translated before this authority resolves them.
/// </summary>
public static class OperationalPolicyResolver
{
    public const string SchemaVersion = "policy-v4";

    public const string MaxUnboundedContinuationStepsKey = "execution.maxUnboundedContinuationSteps";
    public const string MaxNoChangesCommitsKey = "execution.maxNoChangesCommits";
    public const string OperationalContextGrowthWarningStreakKey = "execution.operationalContextGrowthWarningStreak";
    public const string DecisionSessionResumeKey = "decisions.sessionResume";
    public const string DecisionRecoveryPolicyKey = "decisions.recoveryPolicy";
    public const string SessionTelemetryKey = "runtime.sessionTelemetry";
    public const string UsageLimitWaitRetryKey = "runtime.usageLimitWaitRetry";
    public const string InputWaitReportingKey = "runtime.inputWaitReporting";

    public const int DefaultMaxUnboundedContinuationSteps = 32;
    public const int DefaultMaxNoChangesCommits = 2;
    public const int DefaultOperationalContextGrowthWarningStreak = 2;
    public const bool DefaultDecisionSessionResume = true;
    public const bool DefaultSessionTelemetry = true;
    public const bool DefaultUsageLimitWaitRetry = true;
    public const bool DefaultInputWaitReporting = true;
    public const int DefaultUsageLimitRetryAttempts = 3;

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
        ResumeRecoveryStrategy recoveryStrategy = ResolveRecoveryStrategy(
            workspacePolicy.DecisionRecoveryStrategy,
            workspaceSource,
            overrides,
            provenance);
        bool sessionTelemetry = ResolveBool(
            SessionTelemetryKey,
            DefaultSessionTelemetry,
            workspacePolicy.SessionTelemetry,
            workspaceSource,
            overrides,
            provenance);
        bool usageLimitWaitRetry = ResolveBool(
            UsageLimitWaitRetryKey,
            DefaultUsageLimitWaitRetry,
            workspacePolicy.UsageLimitWaitRetry,
            workspaceSource,
            overrides,
            provenance);
        bool inputWaitReporting = ResolveBool(
            InputWaitReportingKey,
            DefaultInputWaitReporting,
            workspacePolicy.InputWaitReporting,
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

        ResumePolicy resume = ResumePolicy.Create(decisionSessionResume, recoveryStrategy);
        var execution = new ResolvedExecutionPolicy(
            maxUnboundedContinuationSteps,
            maxNoChangesCommits,
            operationalContextGrowthWarningStreak);
        var runtime = new RuntimePolicy(
            new TelemetryPolicy(sessionTelemetry ? TelemetryPolicyMode.Enabled : TelemetryPolicyMode.Disabled),
            new InputWaitPolicy(inputWaitReporting ? InputWaitPolicyMode.Enabled : InputWaitPolicyMode.Disabled));
        var recovery = new RecoveryPolicy(
            resume,
            new UsageLimitRetryPolicy(
                usageLimitWaitRetry ? UsageLimitRetryMode.Bounded : UsageLimitRetryMode.Never,
                usageLimitWaitRetry ? DefaultUsageLimitRetryAttempts : 0),
            UnknownOutcomePolicy.RequiresReconciliation);

        string resolvedJson = JsonSerializer.Serialize(
            new CanonicalPolicySnapshot(
                SchemaVersion,
                new CanonicalExecutionPolicy(
                    maxUnboundedContinuationSteps,
                    maxNoChangesCommits,
                    operationalContextGrowthWarningStreak),
                new CanonicalRuntimePolicy(
                    runtime.Telemetry.Mode.ToString(),
                    runtime.InputWait.Mode.ToString()),
                new CanonicalRecoveryPolicy(
                    new CanonicalResumePolicy(resume.Enabled, resume.RecoveryStrategy.ToString()),
                    new CanonicalUsageLimitRetryPolicy(
                        recovery.UsageLimitRetry.Mode.ToString(),
                        recovery.UsageLimitRetry.MaxAttempts),
                    recovery.UnknownOutcome.ToString()),
                CanonicalPermissions.From(permissions)),
            CanonicalJsonOptions);
        string policyId = ComputePolicyId(resolvedJson);

        return new ResolvedOperationalPolicy(
            policyId,
            SchemaVersion,
            execution,
            runtime,
            recovery,
            permissions,
            provenance,
            resolvedJson,
            workspaceSource);
    }

    private static readonly IReadOnlyList<string> KnownKeys =
    [
        MaxUnboundedContinuationStepsKey,
        MaxNoChangesCommitsKey,
        OperationalContextGrowthWarningStreakKey,
        DecisionSessionResumeKey,
        DecisionRecoveryPolicyKey,
        SessionTelemetryKey,
        UsageLimitWaitRetryKey,
        InputWaitReportingKey,
    ];

    private static ResumeRecoveryStrategy ResolveRecoveryStrategy(
        string? workspace,
        string workspaceSource,
        IReadOnlyDictionary<string, PolicyOverride> overrides,
        List<PolicyFieldProvenance> provenance)
    {
        if (overrides.TryGetValue(DecisionRecoveryPolicyKey, out PolicyOverride? value))
        {
            ResumeRecoveryStrategy invocation = ParseRecoveryStrategy(value.Value);
            provenance.Add(CreateProvenance(
                DecisionRecoveryPolicyKey,
                PolicyLayer.Invocation,
                value.Origin,
                value.Value,
                FormatRecoveryStrategy(invocation),
                "resume-only",
                workspace,
                workspaceSource,
                overrides));
            return invocation;
        }

        if (workspace is not null)
        {
            ResumeRecoveryStrategy configured = ParseRecoveryStrategy(workspace);
            provenance.Add(CreateProvenance(
                DecisionRecoveryPolicyKey,
                PolicyLayer.Workspace,
                workspaceSource,
                workspace,
                FormatRecoveryStrategy(configured),
                "resume-only",
                workspace,
                workspaceSource,
                overrides));
            return configured;
        }

        provenance.Add(CreateProvenance(
            DecisionRecoveryPolicyKey,
            PolicyLayer.BuiltIn,
            BuiltInOrigin,
            null,
            "resume-only",
            "resume-only",
            workspace,
            workspaceSource,
            overrides));
        return ResumeRecoveryStrategy.ResumeOnly;
    }

    private static ResumeRecoveryStrategy ParseRecoveryStrategy(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "resume-only" => ResumeRecoveryStrategy.ResumeOnly,
            "reconstructed" => ResumeRecoveryStrategy.Reconstructed,
            "certified" => ResumeRecoveryStrategy.Certified,
            _ => throw new PolicyResolutionException(
                $"Policy value `{value}` for `{DecisionRecoveryPolicyKey}` must be resume-only, reconstructed, or certified."),
        };

    private static string FormatRecoveryStrategy(ResumeRecoveryStrategy strategy) => strategy switch
    {
        ResumeRecoveryStrategy.ResumeOnly => "resume-only",
        ResumeRecoveryStrategy.Reconstructed => "reconstructed",
        _ => "certified",
    };

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
        string? workspaceText = workspace?.ToString().ToLowerInvariant();
        string builtInText = builtIn.ToString().ToLowerInvariant();
        (string raw, PolicyLayer layer, string origin, bool resolvedFromText) = SelectLayer(
            key, workspaceText, workspaceSource, overrides);
        if (!resolvedFromText)
        {
            provenance.Add(CreateProvenance(
                key, PolicyLayer.BuiltIn, BuiltInOrigin, null, builtInText,
                builtInText, workspaceText, workspaceSource, overrides));
            return builtIn;
        }

        bool value = raw.ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => throw new PolicyResolutionException(
                $"Policy value `{raw}` for `{key}` (from {origin}) is not a boolean; expected true/false."),
        };
        provenance.Add(CreateProvenance(
            key, layer, origin, raw, value.ToString().ToLowerInvariant(),
            builtInText, workspaceText, workspaceSource, overrides));
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
        string? workspaceText = workspace?.ToString(CultureInfo.InvariantCulture);
        string builtInText = builtIn.ToString(CultureInfo.InvariantCulture);
        (string raw, PolicyLayer layer, string origin, bool resolvedFromText) = SelectLayer(
            key, workspaceText, workspaceSource, overrides);
        if (!resolvedFromText)
        {
            provenance.Add(CreateProvenance(
                key, PolicyLayer.BuiltIn, BuiltInOrigin, null, builtInText,
                builtInText, workspaceText, workspaceSource, overrides));
            return builtIn;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 1)
        {
            throw new PolicyResolutionException(
                $"Policy value `{raw}` for `{key}` (from {origin}) is not a positive integer.");
        }

        provenance.Add(CreateProvenance(
            key, layer, origin, raw, value.ToString(CultureInfo.InvariantCulture),
            builtInText, workspaceText, workspaceSource, overrides));
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

    private static PolicyFieldProvenance CreateProvenance(
        string key,
        PolicyLayer layer,
        string origin,
        string? configuredValue,
        string effectiveValue,
        string builtInValue,
        string? workspaceValue,
        string workspaceSource,
        IReadOnlyDictionary<string, PolicyOverride> overrides)
    {
        var chain = new List<string> { $"built-in:{builtInValue}" };
        if (workspaceValue is not null)
        {
            chain.Add($"workspace:{workspaceSource}={workspaceValue}");
        }

        if (overrides.TryGetValue(key, out PolicyOverride? invocation))
        {
            chain.Add($"invocation:{invocation.Origin}={invocation.Value}");
        }

        string reason = layer switch
        {
            PolicyLayer.Invocation => "Invocation override selected over workspace and built-in inputs.",
            PolicyLayer.Workspace => "Workspace configuration selected over the built-in default.",
            _ => "No workspace or invocation value was configured; the built-in default is effective.",
        };
        return new PolicyFieldProvenance(key, layer, origin)
        {
            ConfiguredValue = configuredValue,
            EffectiveValue = effectiveValue,
            Reason = reason,
            OverrideChain = chain,
        };
    }

    // The `pol_v1_` prefix versions the identity SCHEME (sha256 over canonical JSON, first 32
    // hex); the field-set version lives inside the hashed JSON as SchemaVersion, so a schema
    // change already yields different identities without a new prefix.
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
        CanonicalExecutionPolicy Execution,
        CanonicalRuntimePolicy Runtime,
        CanonicalRecoveryPolicy Recovery,
        CanonicalPermissions Permissions);

    private sealed record CanonicalExecutionPolicy(
        int MaxUnboundedContinuationSteps,
        int MaxNoChangesCommits,
        int OperationalContextGrowthWarningStreak);

    private sealed record CanonicalRuntimePolicy(
        string Telemetry,
        string InputWait);

    private sealed record CanonicalRecoveryPolicy(
        CanonicalResumePolicy Resume,
        CanonicalUsageLimitRetryPolicy UsageLimitRetry,
        string UnknownOutcome);

    private sealed record CanonicalResumePolicy(
        bool Enabled,
        string RecoveryStrategy);

    private sealed record CanonicalUsageLimitRetryPolicy(
        string Mode,
        int MaxAttempts);

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
