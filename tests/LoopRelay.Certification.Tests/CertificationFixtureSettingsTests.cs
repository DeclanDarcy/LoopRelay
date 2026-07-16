using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class CertificationFixtureSettingsTests
{
    [Fact]
    public async Task Materialized_settings_use_the_certified_luna_brain_profile()
    {
        const string model = "gpt-5.6-luna";
        string root = Directory.CreateTempSubdirectory("looprelay-fixture-settings").FullName;
        try
        {
            string cliDirectory = Path.Combine(root, "cli");
            string caseDirectory = Path.Combine(root, "case");
            Directory.CreateDirectory(cliDirectory);
            Directory.CreateDirectory(caseDirectory);
            string cliPath = Path.Combine(cliDirectory, "LoopRelay.Cli.exe");
            await File.WriteAllTextAsync(cliPath, string.Empty);
            await File.WriteAllTextAsync(
                Path.Combine(cliDirectory, "settings.default.json"),
                """{"schemaVersion":"settings-v3","runtime":{"brain":{"model":"gpt-5.6-sol","effort":"xhigh"}},"permissions":{}}""");

            string path = await CertificationFixtureSettings.WriteAsync(
                caseDirectory,
                cliPath,
                model);
            JsonNode settings = JsonNode.Parse(await File.ReadAllTextAsync(path))!;

            Assert.Equal(
                model,
                settings["runtime"]?["brain"]?["model"]?.GetValue<string>());
            Assert.Equal(
                CertificationFixtureSettings.BrainEffort,
                settings["runtime"]?["brain"]?["effort"]?.GetValue<string>());
            Assert.Null(settings["brainModel"]);
            Assert.Null(settings["brainEffort"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("gpt-5.6-luna", "medium", true)]
    [InlineData("gpt-5.3-codex-spark", "medium", false)]
    [InlineData("gpt-5.4-mini", "medium", false)]
    [InlineData("gpt-5.6-sol", "high", false)]
    [InlineData("", "medium", false)]
    [InlineData("gpt-5.6-luna", "", false)]
    public void Release_gate_credits_only_the_exact_certified_fixture_profile(
        string model,
        string effort,
        bool expected)
    {
        using JsonDocument document = JsonDocument.Parse(
            $$"""{"evidence":["model:{{model}}",{"nested":["effort:{{effort}}"]}]}""");

        Assert.Equal(expected, ReleaseGateRunner.EvidenceUsesFixtureProfile(document.RootElement));
    }

    [Theory]
    [InlineData(null, "gpt-5.6-luna")]
    [InlineData("", "gpt-5.6-luna")]
    [InlineData("gpt-5.6-luna", "gpt-5.6-luna")]
    public void Model_selection_accepts_only_the_certified_luna_profile(
        string? configured,
        string expected)
    {
        Assert.Equal(expected, CertificationFixtureSettings.ResolveBrainModel(configured));
    }

    [Theory]
    [InlineData("gpt-5.3-codex-spark")]
    [InlineData("gpt-5.4-mini")]
    [InlineData("gpt-5.6-sol")]
    public void Model_selection_rejects_every_non_certified_model(string model)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            CertificationFixtureSettings.ResolveBrainModel(model));

        Assert.Contains("gpt-5.6-luna", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Empty_turn_at_exhausted_provider_quota_is_a_provider_regression()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-provider-quota").FullName;
        try
        {
            string sessions = Path.Combine(root, "sessions", "2026", "07", "11");
            Directory.CreateDirectory(sessions);
            await File.WriteAllTextAsync(
                Path.Combine(sessions, "rollout.jsonl"),
                """
                {"type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":100.0}}}}
                {"type":"event_msg","payload":{"type":"task_complete","last_agent_message":null}}
                """);

            Assert.True(LiveProviderFailureClassifier.HasQuotaExhaustion(root));
            Assert.Equal(
                CertificationClassification.ProviderRegression,
                LiveProviderFailureClassifier.Classify(false, root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void User_satisfactory_no_rerun_adjudication_preserves_raw_evidence_and_is_accepted()
    {
        var adjudication = new CertificationEvidenceAdjudication(
            "1",
            "eval-full-chain.latest.json",
            "satisfactory-no-rerun",
            "user",
            DateTimeOffset.UtcNow,
            "retained-case",
            true,
            1,
            1,
            "provisional-release-budget:test",
            ["gpt-5.6-luna/medium"],
            ["RunCompletionCertification"],
            ["InterpretCompletionRoute", "VerifyWorkflowExitGate"],
            ["explicit-user-adjudication"]);

        Assert.True(ReleaseGateRunner.IsAcceptedAdjudication(
            adjudication,
            "eval-full-chain.latest.json"));
        Assert.False(ReleaseGateRunner.IsAcceptedAdjudication(
            adjudication,
            "traditional-full-chain.latest.json"));
    }
}
