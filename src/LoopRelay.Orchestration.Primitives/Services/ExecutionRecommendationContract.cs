using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Orchestration.Models;
using LoopRelay.Permissions.Models.Configuration;

namespace LoopRelay.Orchestration.Services;

public static class ExecutionRecommendationContract
{
    public static ExecutionRecommendation ParseAgentOutput(string output)
    {
        Dictionary<string, string> values = ParseExactObject(output, ["Model", "Effort"]);
        return new ExecutionRecommendation(
            ParseModel(values["Model"]),
            ParseEffort(values["Effort"]));
    }

    public static PersistedExecutionRecommendation ParsePersisted(string content)
    {
        Dictionary<string, string> values = ParseExactObject(content, ["Model", "Effort", "PromptHash"]);
        string hash = values["PromptHash"];
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)) ||
            !string.Equals(hash, hash.ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new InvalidDataException("PromptHash must be a lowercase SHA-256 hexadecimal value.");
        }

        return new PersistedExecutionRecommendation(
            ParseModel(values["Model"]),
            ParseEffort(values["Effort"]),
            hash);
    }

    public static string SerializePersisted(PersistedExecutionRecommendation recommendation) =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Model"] = AgentConfigurationCatalog.Format(recommendation.Model),
            ["Effort"] = AgentConfigurationCatalog.Format(recommendation.Effort),
            ["PromptHash"] = recommendation.PromptHash,
        }, new JsonSerializerOptions { WriteIndented = true });

    public static PersistedExecutionRecommendation Bind(
        string prompt,
        ExecutionRecommendation recommendation) =>
        new(recommendation.Model, recommendation.Effort, ComputePromptHash(prompt));

    public static ValidatedExecutionRecommendation ValidatePair(
        string prompt,
        string persistedRecommendation)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidDataException("The execution prompt is empty.");
        }

        PersistedExecutionRecommendation recommendation = ParsePersisted(persistedRecommendation);
        string actualHash = ComputePromptHash(prompt);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actualHash),
            Encoding.ASCII.GetBytes(recommendation.PromptHash)))
        {
            throw new InvalidDataException("The execution recommendation does not match the current execution prompt.");
        }

        return new ValidatedExecutionRecommendation(
            prompt,
            recommendation.Model,
            recommendation.Effort,
            actualHash);
    }

    public static string ComputePromptHash(string prompt) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prompt))).ToLowerInvariant();

    public static string RenderPrompt(string executionPrompt) =>
        $$"""
        Select the model and reasoning effort for the execution agent whose exact system prompt appears below.

        Return exactly one JSON object with exactly two string properties named `Model` and `Effort`.
        Do not use Markdown fences or add commentary.

        Allowed Model values: {{string.Join(", ", AgentConfigurationCatalog.AllowedModelNames)}}
        Allowed Effort values: {{string.Join(", ", AgentConfigurationCatalog.AllowedEffortNames)}}

        <execution-system-prompt>
        {{executionPrompt}}
        </execution-system-prompt>
        """;

    private static AgentModel ParseModel(string value)
    {
        if (!AgentConfigurationCatalog.TryParseModel(value, out AgentModel model))
        {
            throw new InvalidDataException(
                $"Model must be one of: {string.Join(", ", AgentConfigurationCatalog.AllowedModelNames)}.");
        }

        return model;
    }

    private static AgentEffort ParseEffort(string value)
    {
        if (!AgentConfigurationCatalog.TryParseEffort(value, out AgentEffort effort))
        {
            throw new InvalidDataException(
                $"Effort must be one of: {string.Join(", ", AgentConfigurationCatalog.AllowedEffortNames)}.");
        }

        return effort;
    }

    private static Dictionary<string, string> ParseExactObject(
        string content,
        IReadOnlyCollection<string> expectedProperties)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidDataException("Recommendation JSON is empty.");
        }

        try
        {
            var reader = new Utf8JsonReader(
                Encoding.UTF8.GetBytes(content),
                new JsonReaderOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw new InvalidDataException("Recommendation must be one JSON object.");
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new InvalidDataException("Recommendation properties must be JSON strings.");
                }

                string property = reader.GetString()!;
                if (!expectedProperties.Contains(property))
                {
                    throw new InvalidDataException($"Unknown recommendation property '{property}'.");
                }

                if (!values.TryAdd(property, string.Empty))
                {
                    throw new InvalidDataException($"Duplicate recommendation property '{property}'.");
                }

                if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                {
                    throw new InvalidDataException($"Recommendation property '{property}' must be a string.");
                }

                values[property] = reader.GetString()!;
            }

            if (reader.TokenType != JsonTokenType.EndObject || reader.Read())
            {
                throw new InvalidDataException("Recommendation must contain one JSON object with no trailing content.");
            }

            string? missing = expectedProperties.FirstOrDefault(property => !values.ContainsKey(property));
            if (missing is not null)
            {
                throw new InvalidDataException($"Missing recommendation property '{missing}'.");
            }

            return values;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Malformed recommendation JSON: {exception.Message}", exception);
        }
    }
}
