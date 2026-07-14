using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class CertificationFixtureSettingsTests
{
    [Theory]
    [InlineData("gpt-5.3-codex-spark")]
    [InlineData("gpt-5.4-mini")]
    public async Task Materialized_settings_use_the_manually_selected_certification_brain_profile(
        string model)
    {
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
    [InlineData("gpt-5.4-mini", "medium", true)]
    [InlineData("gpt-5.3-codex-spark", "medium", true)]
    [InlineData("gpt-5.6-sol", "high", false)]
    [InlineData("", "medium", false)]
    [InlineData("gpt-5.4-mini", "", false)]
    public void Continuous_evidence_credits_only_the_exact_certified_fixture_profile(
        string model,
        string effort,
        bool expected)
    {
        using JsonDocument document = JsonDocument.Parse(
            $$"""{"evidence":["model:{{model}}",{"nested":["effort:{{effort}}"]}]}""");

        Assert.Equal(expected, ContinuousCertificationRunner.EvidenceUsesFixtureProfile(document.RootElement));
    }

    [Theory]
    [InlineData(null, "gpt-5.3-codex-spark")]
    [InlineData("", "gpt-5.3-codex-spark")]
    [InlineData("gpt-5.3-codex-spark", "gpt-5.3-codex-spark")]
    [InlineData("gpt-5.4-mini", "gpt-5.4-mini")]
    public void Manual_model_selection_accepts_the_certification_equivalence_set(
        string? configured,
        string expected)
    {
        Assert.Equal(expected, CertificationFixtureSettings.ResolveBrainModel(configured));
    }

    [Fact]
    public void Manual_model_selection_rejects_models_outside_the_certification_equivalence_set()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            CertificationFixtureSettings.ResolveBrainModel("gpt-5.6-sol"));

        Assert.Contains("gpt-5.3-codex-spark", exception.Message, StringComparison.Ordinal);
        Assert.Contains("gpt-5.4-mini", exception.Message, StringComparison.Ordinal);
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
            "milestone-14.latest.json",
            "satisfactory-no-rerun",
            "user",
            DateTimeOffset.UtcNow,
            "retained-case",
            true,
            1,
            1,
            "provisional-release-budget:test",
            ["gpt-5.4-mini/medium", "gpt-5.3-codex-spark/medium"],
            ["RunCompletionCertification"],
            ["InterpretCompletionRoute", "VerifyWorkflowExitGate"],
            ["explicit-user-adjudication"]);

        Assert.True(ContinuousCertificationRunner.IsAcceptedAdjudication(
            adjudication,
            "milestone-14.latest.json"));
        Assert.False(ContinuousCertificationRunner.IsAcceptedAdjudication(
            adjudication,
            "milestone-13.latest.json"));
    }
}
