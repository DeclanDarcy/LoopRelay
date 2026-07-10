using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;
using LoopRelay.Permissions.Models.Configuration;
using Xunit;

namespace LoopRelay.Orchestration.Tests.Services;

public sealed class ExecutionRecommendationContractTests
{
    public static IEnumerable<object[]> AllowedPairs() =>
        from model in AgentConfigurationCatalog.AllowedModelNames
        from effort in AgentConfigurationCatalog.AllowedEffortNames
        select new object[] { model, effort };

    [Theory]
    [MemberData(nameof(AllowedPairs))]
    public void ParseAgentOutput_accepts_every_allowed_pair(string model, string effort)
    {
        ExecutionRecommendation parsed = ExecutionRecommendationContract.ParseAgentOutput(
            $$"""{"Model":"{{model}}","Effort":"{{effort}}"}""");

        Assert.Equal(model, AgentConfigurationCatalog.Format(parsed.Model));
        Assert.Equal(effort, AgentConfigurationCatalog.Format(parsed.Effort));
    }

    [Theory]
    [InlineData("")]
    [InlineData("```json\n{\"Model\":\"gpt-5.5\",\"Effort\":\"low\"}\n```")]
    [InlineData("comment {\"Model\":\"gpt-5.5\",\"Effort\":\"low\"}")]
    [InlineData("{\"Model\":\"gpt-5.5\"}")]
    [InlineData("{\"Model\":\"gpt-5.5\",\"Effort\":\"low\",\"Extra\":\"x\"}")]
    [InlineData("{\"Model\":\"gpt-5.5\",\"Model\":\"gpt-5.6-sol\",\"Effort\":\"low\"}")]
    [InlineData("{\"Model\":null,\"Effort\":\"low\"}")]
    [InlineData("{\"Model\":\"GPT-5.5\",\"Effort\":\"low\"}")]
    [InlineData("{\"Model\":\"gpt-5.5\",\"Effort\":\"HIGH\"}")]
    [InlineData("{\"Model\":\"gpt-5.5\",\"Effort\":\"low\"} trailing")]
    public void ParseAgentOutput_rejects_noncanonical_output(string output) =>
        Assert.Throws<InvalidDataException>(() => ExecutionRecommendationContract.ParseAgentOutput(output));

    [Fact]
    public void Persisted_pair_round_trips_and_validates_exact_prompt_hash()
    {
        const string prompt = "exact prompt\r\nwith preserved bytes";
        var recommendation = new ExecutionRecommendation(AgentModel.Gpt56Luna, AgentEffort.XHigh);
        PersistedExecutionRecommendation bound = ExecutionRecommendationContract.Bind(prompt, recommendation);
        string json = ExecutionRecommendationContract.SerializePersisted(bound);

        ValidatedExecutionRecommendation validated =
            ExecutionRecommendationContract.ValidatePair(prompt, json);

        Assert.Equal(prompt, validated.Prompt);
        Assert.Equal(AgentModel.Gpt56Luna, validated.Model);
        Assert.Equal(AgentEffort.XHigh, validated.Effort);
        Assert.Equal(ExecutionRecommendationContract.ComputePromptHash(prompt), validated.PromptHash);
    }

    [Fact]
    public void ValidatePair_rejects_a_prompt_mismatch()
    {
        PersistedExecutionRecommendation bound = ExecutionRecommendationContract.Bind(
            "prompt one",
            new ExecutionRecommendation(AgentModel.Gpt55, AgentEffort.Low));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            ExecutionRecommendationContract.ValidatePair(
                "prompt two",
                ExecutionRecommendationContract.SerializePersisted(bound)));

        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }
}
