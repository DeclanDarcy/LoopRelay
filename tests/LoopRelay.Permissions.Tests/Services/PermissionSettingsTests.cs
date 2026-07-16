using System.Text.Json.Nodes;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Services.Configuration;

namespace LoopRelay.Permissions.Tests.Services;

public sealed class PermissionSettingsTests
{
    [Fact]
    public void Default_settings_load_as_pure_configuration_and_policy_inputs()
    {
        CliSettingsLoadResult result =
            CliSettingsLoader.LoadFromFile(DefaultSettingsPath(), isDefaultTemplate: true);

        Assert.Equal(CliSettingsLoader.CurrentSchemaVersion, result.Source.SchemaVersion);
        Assert.Equal(AgentModel.Gpt56Sol, result.Runtime.Brain.Model);
        Assert.Equal(AgentEffort.XHigh, result.Runtime.Brain.Effort);
        Assert.Empty(result.Runtime.SupportedCodexProfiles);
        Assert.Equal("v1", result.PermissionInputs.FingerprintVersion);
        Assert.Equal(32, result.PolicyInputs.MaxUnboundedContinuationSteps);
        Assert.Equal(2, result.PolicyInputs.MaxNoChangesCommits);
        Assert.Equal(2, result.PolicyInputs.OperationalContextGrowthWarningStreak);
        Assert.True(result.PolicyInputs.DecisionSessionResume);
        Assert.Equal("resume-only", result.PolicyInputs.DecisionRecoveryStrategy);
        Assert.True(result.PolicyInputs.SessionTelemetry);
        Assert.True(result.PolicyInputs.UsageLimitWaitRetry);
        Assert.True(result.PolicyInputs.InputWaitReporting);
        Assert.Null(result.PolicyInputs.LegacyArtifactPolicy);
        Assert.Empty(result.CompatibilityWarnings);
    }

    [Fact]
    public void Policy_v3_is_explicitly_translated_to_policy_v4_inputs()
    {
        JsonObject settings = DefaultSettings();
        JsonObject policy = Object(settings, "policy");
        policy["schemaVersion"] = "policy-v3";
        policy.Remove("recovery");

        CliSettingsLoadResult result = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.Null(result.PolicyInputs.DecisionRecoveryStrategy);
        Assert.True(result.PolicyInputs.SessionTelemetry);
        Assert.True(result.PolicyInputs.UsageLimitWaitRetry);
        Assert.True(result.PolicyInputs.InputWaitReporting);
        Assert.Contains(
            result.CompatibilityWarnings,
            warning => warning.Code == "policy-v3-compatibility");
    }

    [Fact]
    public void Policy_v3_cannot_smuggle_policy_v4_recovery_fields()
    {
        JsonObject settings = DefaultSettings();
        Object(settings, "policy")["schemaVersion"] = "policy-v3";

        Assert.Throws<CliSettingsException>(() =>
            CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
    }

    [Fact]
    public void Loader_does_not_merge_minimum_permission_policy()
    {
        JsonObject settings = DefaultSettings();
        Object(Object(settings, "permissions"), "hardDeny")["privilegeEscalationCommands"] = new JsonArray();

        PermissionPolicyOptions inputs =
            CliSettingsLoader.LoadFromFile(WriteSettings(settings)).PermissionInputs;

        Assert.Empty(inputs.HardDeny.PrivilegeEscalationCommands);
    }

    [Fact]
    public void Loader_reads_configured_runtime_facts_without_selecting_defaults()
    {
        JsonObject settings = DefaultSettings();
        Object(Object(settings, "runtime"), "brain")["model"] = null;
        Object(Object(settings, "runtime"), "brain")["effort"] = null;

        CliSettingsLoadResult result = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.Null(result.Runtime.Brain.Model);
        Assert.Null(result.Runtime.Brain.Effort);
    }

    [Theory]
    [InlineData("model", "")]
    [InlineData("model", "gpt-unknown")]
    [InlineData("model", "gpt-5.3-codex-spark")]
    [InlineData("model", "gpt-5.4-mini")]
    [InlineData("effort", "")]
    [InlineData("effort", "extreme")]
    public void Loader_rejects_blank_or_unsupported_runtime_facts(string property, string value)
    {
        JsonObject settings = DefaultSettings();
        Object(Object(settings, "runtime"), "brain")[property] = value;

        Assert.Throws<CliSettingsException>(() =>
            CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
    }

    [Fact]
    public void Legacy_layout_is_translated_to_typed_inputs_with_warnings()
    {
        JsonObject settings = DefaultSettings();
        settings.Remove("schemaVersion");
        settings.Remove("runtime");
        settings["brainModel"] = "gpt-5.6-luna";
        settings["brainEffort"] = "medium";
        settings["continuity"] = new JsonObject
        {
            ["decisionResume"] = false,
            ["recoveryPolicy"] = "resume-only",
            ["supportedCodexProfiles"] = new JsonArray("codex-0.144"),
        };
        settings["artifactPolicy"] = new JsonObject
        {
            ["allowHitlRequestedNonImplementationFiles"] = false,
            ["allowAuxiliaryNonImplementationFiles"] = false,
        };
        Object(Object(settings, "policy"), "decisions").Remove("sessionResume");
        Object(settings, "policy").Remove("recovery");

        CliSettingsLoadResult result = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.Equal("legacy-unversioned", result.Source.SchemaVersion);
        Assert.Equal(AgentModel.Gpt56Luna, result.Runtime.Brain.Model);
        Assert.Equal(AgentEffort.Medium, result.Runtime.Brain.Effort);
        Assert.Equal(["codex-0.144"], result.Runtime.SupportedCodexProfiles);
        Assert.False(result.PolicyInputs.DecisionSessionResume);
        Assert.Equal("resume-only", result.PolicyInputs.DecisionRecoveryStrategy);
        Assert.NotNull(result.PolicyInputs.LegacyArtifactPolicy);
        Assert.Contains(result.CompatibilityWarnings, warning => warning.Code == "legacy-settings-layout");
        Assert.Contains(result.CompatibilityWarnings, warning => warning.Code == "legacy-artifact-policy");
    }

    [Fact]
    public void Canonical_and_legacy_runtime_layouts_cannot_be_mixed()
    {
        JsonObject settings = DefaultSettings();
        settings["brainModel"] = "gpt-5.6-sol";

        Assert.Throws<CliSettingsException>(() =>
            CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
    }

    [Fact]
    public void Loader_rejects_duplicate_policy_inputs_across_compatibility_sections()
    {
        JsonObject settings = DefaultSettings();
        settings.Remove("schemaVersion");
        settings.Remove("runtime");
        settings["continuity"] = new JsonObject { ["decisionResume"] = true };

        Assert.Throws<CliSettingsException>(() =>
            CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
    }

    [Fact]
    public void Missing_policy_section_remains_unconfigured()
    {
        JsonObject settings = DefaultSettings();
        settings.Remove("policy");

        CliSettingsLoadResult result = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.Equal(CliPolicyDocument.Empty, result.PolicyInputs);
    }

    [Fact]
    public void Unknown_top_level_or_nested_members_are_rejected()
    {
        JsonObject topLevel = DefaultSettings();
        topLevel["polcy"] = new JsonObject();
        Assert.Throws<CliSettingsException>(() =>
            CliSettingsLoader.LoadFromFile(WriteSettings(topLevel)));

        JsonObject nested = DefaultSettings();
        Object(Object(nested, "runtime"), "brain")["modle"] = "gpt-5.6-sol";
        Assert.Throws<CliSettingsException>(() =>
            CliSettingsLoader.LoadFromFile(WriteSettings(nested)));
    }

    [Fact]
    public void Loader_rejects_missing_required_permission_sections()
    {
        JsonObject settings = DefaultSettings();
        Object(settings, "permissions").Remove("hardDeny");

        CliSettingsException exception = Assert.Throws<CliSettingsException>(() =>
            CliSettingsLoader.LoadFromFile(WriteSettings(settings)));

        Assert.Contains("permissions.hardDeny section is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Loader_rejects_duplicate_configured_entries()
    {
        JsonObject settings = DefaultSettings();
        Array(Object(settings, "permissions"), "commandsWithSubcommands").Insert(1, "GIT");

        Assert.Throws<CliSettingsException>(() =>
            CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
    }

    [Fact]
    public void Loader_reads_environment_override_with_source_provenance()
    {
        string path = WriteSettings(DefaultSettings());

        CliSettingsLoadResult loaded = CliSettingsLoader.Load(
            baseDirectory: Path.GetTempPath(),
            getEnvironmentVariable: name =>
                name == CliSettingsLoader.SettingsPathEnvironmentVariable ? path : null);

        Assert.Equal(Path.GetFullPath(path), loaded.Source.Path);
        Assert.False(loaded.Source.IsDefaultTemplate);
    }

    private static JsonObject DefaultSettings() =>
        JsonNode.Parse(File.ReadAllText(DefaultSettingsPath()))!.AsObject();

    private static JsonObject Object(JsonObject parent, string property) =>
        parent[property]!.AsObject();

    private static JsonArray Array(JsonObject parent, string property) =>
        parent[property]!.AsArray();

    private static string WriteSettings(JsonObject settings)
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "LoopRelay.Permissions.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.json");
        File.WriteAllText(path, settings.ToJsonString());
        return path;
    }

    private static string DefaultSettingsPath()
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
}
