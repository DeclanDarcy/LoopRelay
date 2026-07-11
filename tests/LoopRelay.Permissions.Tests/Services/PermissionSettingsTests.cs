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
    public void Retired_artifact_policy_section_is_rejected()
    {
        // policy-v2 (M6 owner ruling): the implementation-first prompt policy is template-owned
        // and unconditional, so the artifactPolicy section no longer exists. A settings file
        // that still carries it rejects as an unknown top-level key instead of being silently
        // ignored — the same posture as any other configured value with no production effect.
        JsonObject settings = DefaultSettings();
        settings["artifactPolicy"] = new JsonObject
        {
            ["allowHitlRequestedNonImplementationFiles"] = false,
        };

        Assert.Throws<CliSettingsException>(() => CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
    }

    [Fact]
    public void Loader_reads_the_policy_section()
    {
        JsonObject settings = DefaultSettings();
        settings["policy"] = new JsonObject
        {
            ["execution"] = new JsonObject
            {
                ["maxUnboundedContinuationSteps"] = 64,
                ["maxNoChangesCommits"] = 3,
                ["operationalContextGrowthWarningStreak"] = 5,
            },
            ["decisions"] = new JsonObject
            {
                ["sessionResume"] = false,
            },
        };

        CliSettingsLoadResult loaded = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.Equal(64, loaded.Policy.MaxUnboundedContinuationSteps);
        Assert.Equal(3, loaded.Policy.MaxNoChangesCommits);
        Assert.Equal(5, loaded.Policy.OperationalContextGrowthWarningStreak);
        Assert.False(loaded.Policy.DecisionSessionResume);
    }

    [Fact]
    public void Missing_policy_section_loads_as_unconfigured()
    {
        JsonObject settings = DefaultSettings();
        settings.Remove("policy");

        CliSettingsLoadResult loaded = CliSettingsLoader.LoadFromFile(WriteSettings(settings));

        Assert.Null(loaded.Policy.MaxUnboundedContinuationSteps);
        Assert.Null(loaded.Policy.MaxNoChangesCommits);
        Assert.Null(loaded.Policy.OperationalContextGrowthWarningStreak);
        Assert.Null(loaded.Policy.DecisionSessionResume);
    }

    [Fact]
    public void Unknown_key_inside_the_policy_section_is_rejected()
    {
        JsonObject settings = DefaultSettings();
        Object(Object(settings, "policy"), "execution")["maxUnboundedContinuatoinSteps"] = 64;

        Assert.Throws<CliSettingsException>(() => CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
    }

    [Fact]
    public void Unknown_top_level_settings_key_is_rejected()
    {
        JsonObject settings = DefaultSettings();
        settings["polcy"] = new JsonObject();

        Assert.Throws<CliSettingsException>(() => CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
    }

    [Fact]
    public void Unknown_key_inside_the_permissions_section_is_rejected()
    {
        // The permissions snapshot is part of the versioned policy identity, so a typoed key
        // that silently does nothing would be a configured value with no production effect.
        JsonObject settings = DefaultSettings();
        Object(settings, "permissions")["safeBashCommandss"] = new JsonArray("rg");

        Assert.Throws<CliSettingsException>(() => CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
    }

    [Fact]
    public void Unknown_key_inside_a_nested_permissions_section_is_rejected()
    {
        JsonObject settings = DefaultSettings();
        Object(Object(settings, "permissions"), "allow")["alwaysAllowedCommandss"] = new JsonArray("pwd");

        Assert.Throws<CliSettingsException>(() => CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
    }

    [Fact]
    public void Wrongly_typed_policy_value_is_rejected()
    {
        JsonObject settings = DefaultSettings();
        Object(Object(settings, "policy"), "decisions")["sessionResume"] = "sometimes";

        Assert.Throws<CliSettingsException>(() => CliSettingsLoader.LoadFromFile(WriteSettings(settings)));
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
