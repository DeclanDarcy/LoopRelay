using System.Text.Json.Nodes;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Permissions.Primitives.Evaluation;
using LoopRelay.Permissions.Primitives.Parsing;
using LoopRelay.Permissions.Primitives.Requests;
using LoopRelay.Permissions.Services.Configuration;
using LoopRelay.Permissions.Services.Evaluation;

namespace LoopRelay.Permissions.Tests.Services;

/// <summary>
/// Closes the seam between <see cref="PermissionSettingsTests"/> (which loads settings.json and
/// asserts on the resulting <see cref="PermissionPolicyOptions"/> fields, never evaluating a
/// command) and <see cref="PermissionEvaluatorEnginePolicyTests"/> (which constructs
/// <see cref="PermissionPolicyOptions"/> directly in C# and asserts Allow/Deny, never going
/// through JSON). These tests drive a real temp settings.json file through
/// <see cref="CliSettingsLoader"/> and feed the loaded policy into
/// <see cref="PermissionEvaluatorEngine"/>, asserting on the resulting decision.
/// </summary>
public sealed class PermissionSettingsEvaluationSeamTests
{
    [Fact]
    public void ConfiguredSafeBashCommand_FromSettingsJson_IsAllowedByEngine()
    {
        // "mytool" is not in the built-in default safeBashCommands set; only the operator's
        // settings.json makes it safe. This is the test that would have failed before commit
        // e56ad81b, when Evaluate() routed through a static helper that always evaluated
        // against PermissionPolicyOptions.Default instead of the loaded policy.
        JsonObject settings = DefaultSettings();
        Array(Object(settings, "permissions"), "safeBashCommands").Add("mytool");

        PermissionPolicyOptions policy =
            CliSettingsLoader.LoadFromFile(WriteSettings(settings)).PermissionInputs;
        var engine = new PermissionEvaluatorEngine(policy);

        EvalResult result = engine.Evaluate([new CanonicalCommand("mytool", null, [], [])]);

        Assert.Equal(RuleDecision.Allow, result.Decision);
    }

    [Fact]
    public void FloorDeniedCommand_FromPermissiveSettingsJson_IsStillDenied()
    {
        // The loaded settings.json OMITS 'sudo' from its own hardDeny.privilegeEscalationCommands
        // (as Loader_does_not_merge_minimum_permission_policy proves the loader lets through
        // unmerged) and additionally lists 'sudo' as a safe bash command. Only
        // PermissionEvaluatorEngine's constructor, via PermissionPolicyFactory.MergeWithMinimum,
        // puts 'sudo' back into HardDeny; if that merge were skipped, this command would fall
        // through to the allow list and be permitted. Asserting Deny here proves the code-level
        // floor rescues a config that omits the protection - the guarantee that makes the
        // PERF-07 behavior change (honoring configured policy) safe.
        JsonObject settings = DefaultSettings();
        Object(Object(settings, "permissions"), "hardDeny")["privilegeEscalationCommands"] = new JsonArray();
        Array(Object(settings, "permissions"), "safeBashCommands").Add("sudo");

        PermissionPolicyOptions policy =
            CliSettingsLoader.LoadFromFile(WriteSettings(settings)).PermissionInputs;
        var engine = new PermissionEvaluatorEngine(policy);

        EvalResult result = engine.Evaluate([new CanonicalCommand("sudo", null, [], [])]);

        Assert.Equal(RuleDecision.Deny, result.Decision);
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
