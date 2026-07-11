using System.Text.Json;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Services.Configuration;
using LoopRelay.Permissions.Services.Evaluation;

namespace LoopRelay.Orchestration.Tests.Policy;

public sealed class OperationalPolicyResolverTests
{
    private const string WorkspaceSource = "settings:/workspace/settings.json";

    private static readonly PermissionPolicyOptions Permissions = PermissionPolicyFactory.Minimum;

    [Fact]
    public void Defaults_only_resolution_yields_built_in_values_with_built_in_provenance()
    {
        ResolvedOperationalPolicy policy = Resolve();

        Assert.Equal(32, policy.MaxUnboundedContinuationSteps);
        Assert.Equal(2, policy.MaxNoChangesCommits);
        Assert.Equal(2, policy.OperationalContextGrowthWarningStreak);
        Assert.True(policy.DecisionSessionResume);
        // D3 (M7): the operational wrappers are product intent — active by default.
        Assert.True(policy.SessionTelemetry);
        Assert.True(policy.UsageLimitWaitRetry);
        Assert.True(policy.InputWaitReporting);
        Assert.All(policy.Provenance, field => Assert.Equal(PolicyLayer.BuiltIn, field.Layer));
        Assert.Equal(7, policy.Provenance.Count);
    }

    [Fact]
    public void Workspace_values_override_built_in_defaults_with_workspace_provenance()
    {
        ResolvedOperationalPolicy policy = Resolve(
            workspace: new CliPolicyDocument(64, null, 5, false, null, null, null));

        Assert.Equal(64, policy.MaxUnboundedContinuationSteps);
        Assert.Equal(2, policy.MaxNoChangesCommits);
        Assert.Equal(5, policy.OperationalContextGrowthWarningStreak);
        Assert.False(policy.DecisionSessionResume);

        PolicyFieldProvenance steps = policy.Provenance.Single(
            field => field.Field == OperationalPolicyResolver.MaxUnboundedContinuationStepsKey);
        Assert.Equal(PolicyLayer.Workspace, steps.Layer);
        Assert.Equal(WorkspaceSource, steps.Origin);
        PolicyFieldProvenance commits = policy.Provenance.Single(
            field => field.Field == OperationalPolicyResolver.MaxNoChangesCommitsKey);
        Assert.Equal(PolicyLayer.BuiltIn, commits.Layer);
    }

    [Fact]
    public void Invocation_overrides_beat_workspace_values_with_invocation_provenance()
    {
        ResolvedOperationalPolicy policy = Resolve(
            workspace: new CliPolicyDocument(64, null, null, null, null, null, null),
            overrides:
            [
                new PolicyOverride(
                    OperationalPolicyResolver.MaxUnboundedContinuationStepsKey,
                    "8",
                    "flag:--policy",
                    IsExplicit: true),
            ]);

        Assert.Equal(8, policy.MaxUnboundedContinuationSteps);
        PolicyFieldProvenance steps = policy.Provenance.Single(
            field => field.Field == OperationalPolicyResolver.MaxUnboundedContinuationStepsKey);
        Assert.Equal(PolicyLayer.Invocation, steps.Layer);
        Assert.Equal("flag:--policy", steps.Origin);
    }

    [Fact]
    public void Explicit_flag_beats_ambient_environment_override_within_the_invocation_layer()
    {
        ResolvedOperationalPolicy policy = Resolve(
            overrides:
            [
                new PolicyOverride(
                    OperationalPolicyResolver.DecisionSessionResumeKey,
                    "false",
                    "env:LoopRelay_DECISION_RESUME",
                    IsExplicit: false),
                new PolicyOverride(
                    OperationalPolicyResolver.DecisionSessionResumeKey,
                    "true",
                    "flag:--policy",
                    IsExplicit: true),
            ]);

        Assert.True(policy.DecisionSessionResume);
        PolicyFieldProvenance resume = policy.Provenance.Single(
            field => field.Field == OperationalPolicyResolver.DecisionSessionResumeKey);
        Assert.Equal("flag:--policy", resume.Origin);
    }

    [Fact]
    public void Duplicate_explicit_overrides_for_one_key_are_a_rejected_conflict()
    {
        PolicyResolutionException exception = Assert.Throws<PolicyResolutionException>(() => Resolve(
            overrides:
            [
                new PolicyOverride(OperationalPolicyResolver.MaxNoChangesCommitsKey, "3", "flag:--policy", IsExplicit: true),
                new PolicyOverride(OperationalPolicyResolver.MaxNoChangesCommitsKey, "4", "flag:--policy", IsExplicit: true),
            ]));

        Assert.Contains("Conflicting policy overrides", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_override_key_is_rejected()
    {
        PolicyResolutionException exception = Assert.Throws<PolicyResolutionException>(() => Resolve(
            overrides: [new PolicyOverride("execution.maxRetries", "3", "flag:--policy", IsExplicit: true)]));

        Assert.Contains("Unknown policy override key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_override_values_are_rejected()
    {
        Assert.Throws<PolicyResolutionException>(() => Resolve(
            overrides:
            [
                new PolicyOverride(OperationalPolicyResolver.MaxNoChangesCommitsKey, "many", "flag:--policy", IsExplicit: true),
            ]));
        Assert.Throws<PolicyResolutionException>(() => Resolve(
            overrides:
            [
                new PolicyOverride(OperationalPolicyResolver.MaxNoChangesCommitsKey, "0", "flag:--policy", IsExplicit: true),
            ]));
        Assert.Throws<PolicyResolutionException>(() => Resolve(
            overrides:
            [
                new PolicyOverride(OperationalPolicyResolver.DecisionSessionResumeKey, "sometimes", "flag:--policy", IsExplicit: true),
            ]));
    }

    [Theory]
    [InlineData("artifactPolicy.allowHitlRequestedNonImplementationFiles")]
    [InlineData("artifactPolicy.allowAuxiliaryNonImplementationFiles")]
    public void Retired_artifact_policy_keys_are_rejected_as_unknown(string retiredKey)
    {
        // policy-v2 (M6 owner ruling): the implementation-first prompt policy is template-owned
        // and unconditional, so the artifactPolicy toggles no longer exist as policy fields. A
        // configuration that still names one rejects loudly instead of being silently ignored.
        PolicyResolutionException exception = Assert.Throws<PolicyResolutionException>(() => Resolve(
            overrides: [new PolicyOverride(retiredKey, "false", "flag:--policy", IsExplicit: true)]));

        Assert.Contains("Unknown policy override key", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("runtime.sessionTelemetry")]
    [InlineData("runtime.usageLimitWaitRetry")]
    [InlineData("runtime.inputWaitReporting")]
    public void Runtime_wrapper_toggles_resolve_from_overrides_and_reject_garbage(string key)
    {
        // policy-v3 (M7): the D3 wrapper knobs join the schema as the wrappers reconnect.
        ResolvedOperationalPolicy policy = Resolve(
            overrides: [new PolicyOverride(key, "false", "flag:--policy", IsExplicit: true)]);

        bool value = key switch
        {
            OperationalPolicyResolver.SessionTelemetryKey => policy.SessionTelemetry,
            OperationalPolicyResolver.UsageLimitWaitRetryKey => policy.UsageLimitWaitRetry,
            _ => policy.InputWaitReporting,
        };
        Assert.False(value);
        PolicyFieldProvenance provenance = policy.Provenance.Single(field => field.Field == key);
        Assert.Equal(PolicyLayer.Invocation, provenance.Layer);

        Assert.Throws<PolicyResolutionException>(() => Resolve(
            overrides: [new PolicyOverride(key, "verbose", "flag:--policy", IsExplicit: true)]));
    }

    [Fact]
    public void Policy_identity_is_deterministic_and_versioned()
    {
        ResolvedOperationalPolicy first = Resolve();
        ResolvedOperationalPolicy second = Resolve();

        Assert.Equal(first.PolicyId, second.PolicyId);
        Assert.StartsWith("pol_v1_", first.PolicyId, StringComparison.Ordinal);
        Assert.Equal(OperationalPolicyResolver.SchemaVersion, first.SchemaVersion);
    }

    [Fact]
    public void Policy_identity_changes_when_any_effective_value_changes()
    {
        string baseline = Resolve().PolicyId;

        Assert.NotEqual(baseline, Resolve(workspace: new CliPolicyDocument(64, null, null, null, null, null, null)).PolicyId);
        Assert.NotEqual(baseline, Resolve(workspace: new CliPolicyDocument(null, 3, null, null, null, null, null)).PolicyId);
        Assert.NotEqual(baseline, Resolve(workspace: new CliPolicyDocument(null, null, 5, null, null, null, null)).PolicyId);
        Assert.NotEqual(baseline, Resolve(workspace: new CliPolicyDocument(null, null, null, false, null, null, null)).PolicyId);
        Assert.NotEqual(baseline, Resolve(workspace: new CliPolicyDocument(null, null, null, null, false, null, null)).PolicyId);
        Assert.NotEqual(baseline, Resolve(workspace: new CliPolicyDocument(null, null, null, null, null, false, null)).PolicyId);
        Assert.NotEqual(baseline, Resolve(workspace: new CliPolicyDocument(null, null, null, null, null, null, false)).PolicyId);
    }

    [Fact]
    public void Policy_identity_covers_effective_values_not_provenance()
    {
        // The same effective policy has the same identity regardless of which layer supplied
        // each value: configuring the built-in default explicitly changes provenance only.
        ResolvedOperationalPolicy fromDefaults = Resolve();
        ResolvedOperationalPolicy fromWorkspace = Resolve(
            workspace: new CliPolicyDocument(32, 2, 2, true, true, true, true));

        Assert.Equal(fromDefaults.PolicyId, fromWorkspace.PolicyId);
        Assert.NotEqual(fromDefaults.Provenance, fromWorkspace.Provenance);
    }

    [Fact]
    public void Canonical_serialization_sorts_permission_collections_for_a_stable_identity()
    {
        // Permission options hold hash-ordered sets and .NET randomizes string hashing per
        // process; without sorted canonicalization the same configuration would hash to a
        // different policy identity on every invocation.
        ResolvedOperationalPolicy policy = OperationalPolicyResolver.Resolve(
            CliPolicyDocument.Empty,
            WorkspaceSource,
            [],
            PermissionPolicyFactory.Default);

        using JsonDocument document = JsonDocument.Parse(policy.ResolvedJson);
        JsonElement permissions = document.RootElement.GetProperty("permissions");
        string[] safeBashCommands = permissions.GetProperty("safeBashCommands")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToArray();
        Assert.NotEmpty(safeBashCommands);
        Assert.Equal(safeBashCommands.OrderBy(value => value, StringComparer.Ordinal), safeBashCommands);
        string[] packageManagerKeys = permissions.GetProperty("allow")
            .GetProperty("packageManagerAllowedSubcommands")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        Assert.NotEmpty(packageManagerKeys);
        Assert.Equal(packageManagerKeys.OrderBy(value => value, StringComparer.Ordinal), packageManagerKeys);
    }

    [Fact]
    public void Policy_identity_covers_the_permissions_snapshot()
    {
        string minimum = Resolve().PolicyId;
        string different = OperationalPolicyResolver.Resolve(
            CliPolicyDocument.Empty,
            WorkspaceSource,
            [],
            PermissionPolicyFactory.Default).PolicyId;

        Assert.NotEqual(minimum, different);
    }

    [Fact]
    public void Shipped_default_template_matches_the_built_in_policy_defaults()
    {
        // config/settings.default.json writes the policy defaults out explicitly, so a changed
        // built-in that is not mirrored there would silently have no effect on default
        // installs. This pins template values to the resolver's built-ins.
        CliSettingsLoadResult template = CliSettingsLoader.LoadFromFile(DefaultTemplatePath(), isDefaultTemplate: true);

        Assert.Equal(
            OperationalPolicyResolver.DefaultMaxUnboundedContinuationSteps,
            template.Policy.MaxUnboundedContinuationSteps);
        Assert.Equal(
            OperationalPolicyResolver.DefaultMaxNoChangesCommits,
            template.Policy.MaxNoChangesCommits);
        Assert.Equal(
            OperationalPolicyResolver.DefaultOperationalContextGrowthWarningStreak,
            template.Policy.OperationalContextGrowthWarningStreak);
        Assert.Equal(
            OperationalPolicyResolver.DefaultDecisionSessionResume,
            template.Policy.DecisionSessionResume);
        Assert.Equal(
            OperationalPolicyResolver.DefaultSessionTelemetry,
            template.Policy.SessionTelemetry);
        Assert.Equal(
            OperationalPolicyResolver.DefaultUsageLimitWaitRetry,
            template.Policy.UsageLimitWaitRetry);
        Assert.Equal(
            OperationalPolicyResolver.DefaultInputWaitReporting,
            template.Policy.InputWaitReporting);

        ResolvedOperationalPolicy fromTemplate = OperationalPolicyResolver.Resolve(
            template.Policy,
            WorkspaceSource,
            [],
            Permissions);
        Assert.Equal(Resolve().PolicyId, fromTemplate.PolicyId);
    }

    private static string DefaultTemplatePath()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "config", "settings.default.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate config/settings.default.json.");
    }

    private static ResolvedOperationalPolicy Resolve(
        CliPolicyDocument? workspace = null,
        IReadOnlyList<PolicyOverride>? overrides = null) =>
        OperationalPolicyResolver.Resolve(
            workspace ?? CliPolicyDocument.Empty,
            WorkspaceSource,
            overrides ?? [],
            Permissions);
}
