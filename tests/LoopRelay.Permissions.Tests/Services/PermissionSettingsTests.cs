using System.Text.Json.Nodes;
using LoopRelay.Permissions.Models;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Primitives;
using LoopRelay.Permissions.Primitives.Requests;
using LoopRelay.Permissions.Services;
using LoopRelay.Permissions.Services.Configuration;
using LoopRelay.Permissions.Services.Evaluation;
using LoopRelay.Permissions.Services.Parsing;
using LoopRelay.Permissions.Services.Security;

namespace LoopRelay.Permissions.Tests.Services;

public sealed class PermissionSettingsTests
{
    [Fact]
    public void Default_settings_json_reproduces_current_permission_behavior()
    {
        CliSettingsLoadResult result = CliSettingsLoader.LoadFromFile(DefaultSettingsPath(), isDefaultTemplate: true);
        PermissionPolicyOptions policy = result.Permissions;

        Assert.Equal("v1", policy.FingerprintVersion);
        Assert.False(result.ArtifactPolicy.AllowHitlRequestedNonImplementationFiles);
        Assert.False(result.ArtifactPolicy.AllowAuxiliaryNonImplementationFiles);
        Assert.True(Object(DefaultSettings(), "artifactPolicy").ContainsKey("allowHitlRequestedNonImplementationFiles"));
        Assert.True(Object(DefaultSettings(), "artifactPolicy").ContainsKey("allowAuxiliaryNonImplementationFiles"));
        Assert.Equal(AgentModel.Gpt56Sol, result.Brain.Model);
        Assert.Equal(AgentEffort.XHigh, result.Brain.Effort);

        AssertAllowed(policy, "pwd");
        AssertAllowed(policy, "git status");
        AssertAllowed(policy, "dotnet test");
        AssertAllowed(policy, "npm run lint");
        AssertAllowed(policy, "pytest");
        AssertAllowed(policy, "go test ./...");

        AssertDenied(policy, "sudo id", "Privilege escalation");
        AssertDenied(policy, "rm -rf build", "rm -rf");
        AssertDenied(policy, "curl https://example.com", "Network fetch");
        AssertDenied(policy, "git push --force", "force push");
        AssertDenied(policy, "bash -c ls", "Indirect shell execution");
        AssertDenied(policy, "git commit -m msg", "requires review");
        AssertDenied(policy, "python deploy.py", "closed-world deny");
    }

    [Theory]
    [InlineData("brainModel", null)]
    [InlineData("brainModel", "")]
    [InlineData("brainModel", "gpt-unknown")]
    [InlineData("brainEffort", null)]
    [InlineData("brainEffort", "")]
    [InlineData("brainEffort", "extreme")]
    public void Loader_rejects_missing_blank_or_unsupported_brain_values(string property, string? value)
    {
        JsonObject settings = DefaultSettings();
        settings[property] = value;

        CliSettingsException exception = Assert.Throws<CliSettingsException>(
            () => CliSettingsLoader.LoadFromFile(WriteSettings(settings)));

        Assert.Contains(property, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loader_accepts_the_certification_fixture_brain_profile()
    {
        JsonObject settings = DefaultSettings();
        settings["brainModel"] = "gpt-5.4-mini";
        settings["brainEffort"] = "medium";

        CliSettingsLoadResult result = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.Equal(AgentModel.Gpt54Mini, result.Brain.Model);
        Assert.Equal(AgentEffort.Medium, result.Brain.Effort);
    }

    [Fact]
    public void Loader_rejects_missing_required_sections()
    {
        JsonObject settings = DefaultSettings();
        Permissions(settings).Remove("hardDeny");

        CliSettingsException exception = Assert.Throws<CliSettingsException>(
            () => CliSettingsLoader.LoadFromFile(WriteSettings(settings)));

        Assert.Contains("permissions.hardDeny section is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Loader_rejects_duplicate_configured_entries()
    {
        JsonObject settings = DefaultSettings();
        Array(Permissions(settings), "commandsWithSubcommands").Insert(1, "GIT");

        CliSettingsException exception = Assert.Throws<CliSettingsException>(
            () => CliSettingsLoader.LoadFromFile(WriteSettings(settings)));

        Assert.Contains("duplicate value", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loader_rejects_malformed_recursive_delete_flag_sets()
    {
        JsonObject settings = DefaultSettings();
        JsonArray flagSets = Array(
            Object(Object(Permissions(settings), "hardDeny"), "recursiveForceDelete"),
            "flagSets");
        flagSets[0] = new JsonArray();

        CliSettingsException exception = Assert.Throws<CliSettingsException>(
            () => CliSettingsLoader.LoadFromFile(WriteSettings(settings)));

        Assert.Contains("flagSets[0] must contain at least one flag", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Edited_policy_cannot_allow_minimum_invariant_commands()
    {
        JsonObject settings = DefaultSettings();
        Object(Permissions(settings), "hardDeny")["privilegeEscalationCommands"] = new JsonArray();
        Array(Object(Permissions(settings), "allow"), "alwaysAllowedCommands").Add("sudo");

        PermissionPolicyOptions policy = CliSettingsLoader.LoadFromFile(WriteSettings(settings)).Permissions;

        AssertDenied(policy, "sudo id", "Privilege escalation");
    }

    [Fact]
    public void Loader_reads_environment_override()
    {
        string path = WriteSettings(DefaultSettings());

        CliSettingsLoadResult loaded = CliSettingsLoader.Load(
            baseDirectory: Path.GetTempPath(),
            getEnvironmentVariable: name => name == CliSettingsLoader.SettingsPathEnvironmentVariable ? path : null);

        Assert.Equal(Path.GetFullPath(path), loaded.Path);
        Assert.False(loaded.IsDefaultTemplate);
    }

    [Fact]
    public void Missing_artifact_policy_defaults_to_implementation_first_mode()
    {
        JsonObject settings = DefaultSettings();
        settings.Remove("artifactPolicy");

        CliSettingsLoadResult loaded = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.False(loaded.ArtifactPolicy.AllowHitlRequestedNonImplementationFiles);
        Assert.False(loaded.ArtifactPolicy.AllowAuxiliaryNonImplementationFiles);
    }

    [Fact]
    public void Missing_artifact_policy_flags_default_to_implementation_first_mode()
    {
        JsonObject settings = DefaultSettings();
        settings["artifactPolicy"] = new JsonObject();

        CliSettingsLoadResult loaded = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.False(loaded.ArtifactPolicy.AllowHitlRequestedNonImplementationFiles);
        Assert.False(loaded.ArtifactPolicy.AllowAuxiliaryNonImplementationFiles);
    }

    [Fact]
    public void Loader_reads_enabled_hitl_requested_non_implementation_mode()
    {
        JsonObject settings = DefaultSettings();
        Object(settings, "artifactPolicy")["allowHitlRequestedNonImplementationFiles"] = true;

        CliSettingsLoadResult loaded = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.True(loaded.ArtifactPolicy.AllowHitlRequestedNonImplementationFiles);
        Assert.False(loaded.ArtifactPolicy.AllowAuxiliaryNonImplementationFiles);
    }

    [Fact]
    public void Loader_reads_enabled_auxiliary_non_implementation_mode()
    {
        JsonObject settings = DefaultSettings();
        Object(settings, "artifactPolicy")["allowAuxiliaryNonImplementationFiles"] = true;

        CliSettingsLoadResult loaded = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.False(loaded.ArtifactPolicy.AllowHitlRequestedNonImplementationFiles);
        Assert.True(loaded.ArtifactPolicy.AllowAuxiliaryNonImplementationFiles);
    }

    [Fact]
    public void Artifact_policy_flags_are_independent()
    {
        JsonObject settings = DefaultSettings();
        Object(settings, "artifactPolicy")["allowHitlRequestedNonImplementationFiles"] = true;
        Object(settings, "artifactPolicy")["allowAuxiliaryNonImplementationFiles"] = true;

        CliSettingsLoadResult loaded = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.True(loaded.ArtifactPolicy.AllowHitlRequestedNonImplementationFiles);
        Assert.True(loaded.ArtifactPolicy.AllowAuxiliaryNonImplementationFiles);
    }

    private static void AssertAllowed(PermissionPolicyOptions policy, string rawCommand)
    {
        PermissionResult result = Evaluate(policy, rawCommand);
        Assert.Equal(RuleDecision.Allow, result.Decision);
    }

    private static void AssertDenied(PermissionPolicyOptions policy, string rawCommand, string reasonFragment)
    {
        PermissionResult result = Evaluate(policy, rawCommand);
        Assert.Equal(RuleDecision.Deny, result.Decision);
        Assert.Contains(reasonFragment, result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static PermissionResult Evaluate(PermissionPolicyOptions policy, string rawCommand)
    {
        var handler = new PermissionHandler(
            new CommandParser(policy),
            new CommandCanonicalizer(),
            new Sha256FingerprintService(policy),
            new InMemoryPermissionCache(),
            new PermissionEvaluatorEngine(policy),
            new InvariantGuard(policy));

        return handler.Evaluate(new PermissionRequest("1", "Bash", rawCommand, "repo", "/repo"));
    }

    private static JsonObject DefaultSettings() =>
        JsonNode.Parse(File.ReadAllText(DefaultSettingsPath()))!.AsObject();

    private static JsonObject Permissions(JsonObject settings) =>
        Object(settings, "permissions");

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
