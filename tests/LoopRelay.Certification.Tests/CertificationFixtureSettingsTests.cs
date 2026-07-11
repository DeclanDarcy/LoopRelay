using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class CertificationFixtureSettingsTests
{
    [Fact]
    public async Task Materialized_settings_use_the_certification_brain_profile()
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
                """{"brainModel":"gpt-5.6-sol","brainEffort":"xhigh","artifactPolicy":{}}""");

            string path = await CertificationFixtureSettings.WriteAsync(caseDirectory, cliPath);
            JsonNode settings = JsonNode.Parse(await File.ReadAllTextAsync(path))!;

            Assert.Equal(CertificationFixtureSettings.BrainModel, settings["brainModel"]?.GetValue<string>());
            Assert.Equal(CertificationFixtureSettings.BrainEffort, settings["brainEffort"]?.GetValue<string>());
            Assert.NotNull(settings["artifactPolicy"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("gpt-5.4-mini", "medium", true)]
    [InlineData("gpt-5.3-codex-spark", "medium", true)]
    [InlineData("gpt-5.6-sol", "high", true)]
    [InlineData("", "medium", false)]
    [InlineData("gpt-5.4-mini", "", false)]
    public void Continuous_evidence_accepts_any_declared_fixture_profile(
        string model,
        string effort,
        bool expected)
    {
        using JsonDocument document = JsonDocument.Parse(
            $$"""{"evidence":["model:{{model}}",{"nested":["effort:{{effort}}"]}]}""");

        Assert.Equal(expected, ContinuousCertificationRunner.EvidenceUsesFixtureProfile(document.RootElement));
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
}
