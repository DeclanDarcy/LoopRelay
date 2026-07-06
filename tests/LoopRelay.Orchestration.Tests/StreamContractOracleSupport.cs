using System.Text.Json;

namespace LoopRelay.Orchestration.Tests;

/// <summary>
/// Self-contained copy of the Contract Oracle comparison engine, lifted to an internal shared helper so the
/// m8 "Stream contracts" test files can reuse one structural-equality engine instead of triple-duplicating the
/// private-nested copy in <c>ContractOracleFixtureTests</c> / <c>OrchestrationSnapshotContractTests</c>. The
/// semantics are identical: deep structural equality, no additive compatibility drift allowed under
/// <see cref="StreamContractDriftPolicy.NoCompatibilityDriftAllowed(string)"/>.
/// </summary>
internal static class StreamContractAssert
{
    public static void MatchesFixture(JsonElement expected, JsonElement actual, StreamContractDriftPolicy policy)
    {
        StreamContractDrift[] drifts = Compare(expected, actual, "$", policy).ToArray();
        if (drifts.Length == 0)
        {
            return;
        }

        throw new StreamContractOracleDriftException(policy.ContractName, drifts);
    }

    private static IEnumerable<StreamContractDrift> Compare(
        JsonElement expected,
        JsonElement actual,
        string path,
        StreamContractDriftPolicy policy)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            yield return StreamContractDrift.Structural(
                StreamContractDriftKind.ValueKindChanged,
                path,
                $"expected {expected.ValueKind}, actual {actual.ValueKind}");
            yield break;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (StreamContractDrift drift in CompareObjects(expected, actual, path, policy))
                {
                    yield return drift;
                }

                break;
            case JsonValueKind.Array:
                foreach (StreamContractDrift drift in CompareArrays(expected, actual, path, policy))
                {
                    yield return drift;
                }

                break;
            case JsonValueKind.String:
                if (!StringComparer.Ordinal.Equals(expected.GetString(), actual.GetString()))
                {
                    yield return StreamContractDrift.Structural(
                        StreamContractDriftKind.ValueChanged,
                        path,
                        $"expected {expected.GetRawText()}, actual {actual.GetRawText()}");
                }

                break;
            case JsonValueKind.Number:
                if (!StringComparer.Ordinal.Equals(expected.GetRawText(), actual.GetRawText()))
                {
                    yield return StreamContractDrift.Structural(
                        StreamContractDriftKind.ValueChanged,
                        path,
                        $"expected {expected.GetRawText()}, actual {actual.GetRawText()}");
                }

                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (expected.GetBoolean() != actual.GetBoolean())
                {
                    yield return StreamContractDrift.Structural(
                        StreamContractDriftKind.ValueChanged,
                        path,
                        $"expected {expected.GetBoolean()}, actual {actual.GetBoolean()}");
                }

                break;
            case JsonValueKind.Null:
                break;
            default:
                if (!StringComparer.Ordinal.Equals(expected.GetRawText(), actual.GetRawText()))
                {
                    yield return StreamContractDrift.Structural(
                        StreamContractDriftKind.ValueChanged,
                        path,
                        $"expected {expected.GetRawText()}, actual {actual.GetRawText()}");
                }

                break;
        }
    }

    private static IEnumerable<StreamContractDrift> CompareObjects(
        JsonElement expected,
        JsonElement actual,
        string path,
        StreamContractDriftPolicy policy)
    {
        Dictionary<string, JsonElement> expectedProperties = expected.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value);
        Dictionary<string, JsonElement> actualProperties = actual.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value);

        foreach (string missing in expectedProperties.Keys.Except(actualProperties.Keys, StringComparer.Ordinal))
        {
            yield return StreamContractDrift.Structural(
                StreamContractDriftKind.MissingField,
                BuildPropertyPath(path, missing),
                "field exists in the fixture but not in backend serialization");
        }

        foreach (string unexpected in actualProperties.Keys.Except(expectedProperties.Keys, StringComparer.Ordinal))
        {
            string unexpectedPath = BuildPropertyPath(path, unexpected);
            if (!policy.IsReviewedCompatibilityAddition(unexpectedPath))
            {
                yield return StreamContractDrift.CompatibilityReview(
                    StreamContractDriftKind.UnexpectedField,
                    unexpectedPath,
                    "backend serialization added a field that requires fixture and consumer review");
            }
        }

        foreach ((string name, JsonElement expectedValue) in expectedProperties)
        {
            if (actualProperties.TryGetValue(name, out JsonElement actualValue))
            {
                foreach (StreamContractDrift drift in Compare(expectedValue, actualValue, BuildPropertyPath(path, name), policy))
                {
                    yield return drift;
                }
            }
        }
    }

    private static IEnumerable<StreamContractDrift> CompareArrays(
        JsonElement expected,
        JsonElement actual,
        string path,
        StreamContractDriftPolicy policy)
    {
        JsonElement[] expectedItems = expected.EnumerateArray().ToArray();
        JsonElement[] actualItems = actual.EnumerateArray().ToArray();
        if (expectedItems.Length != actualItems.Length)
        {
            yield return StreamContractDrift.Structural(
                StreamContractDriftKind.ArrayLengthChanged,
                path,
                $"expected {expectedItems.Length}, actual {actualItems.Length}");
            yield break;
        }

        for (int i = 0; i < expectedItems.Length; i++)
        {
            foreach (StreamContractDrift drift in Compare(expectedItems[i], actualItems[i], $"{path}[{i}]", policy))
            {
                yield return drift;
            }
        }
    }

    private static string BuildPropertyPath(string path, string propertyName)
    {
        return $"{path}.{propertyName}";
    }
}

internal sealed class StreamContractOracleDriftException(string contractName, IReadOnlyList<StreamContractDrift> drifts)
    : Exception(CreateMessage(contractName, drifts))
{
    private static string CreateMessage(string contractName, IReadOnlyList<StreamContractDrift> drifts)
    {
        string details = string.Join(
            Environment.NewLine,
            drifts.Select(drift => $"- {drift.Category} {drift.Kind} at {drift.Path}: {drift.Message}"));

        return $"{contractName} Contract Oracle detected contract drift:{Environment.NewLine}{details}";
    }
}

internal sealed class StreamContractDriftPolicy
{
    private readonly HashSet<string> _reviewedCompatibilityAdditions;

    private StreamContractDriftPolicy(string contractName, IEnumerable<string> reviewedCompatibilityAdditions)
    {
        ContractName = contractName;
        _reviewedCompatibilityAdditions = new HashSet<string>(
            reviewedCompatibilityAdditions,
            StringComparer.Ordinal);
    }

    public string ContractName { get; }

    public static StreamContractDriftPolicy NoCompatibilityDriftAllowed(string contractName)
    {
        return new StreamContractDriftPolicy(contractName, []);
    }

    public bool IsReviewedCompatibilityAddition(string path)
    {
        return _reviewedCompatibilityAdditions.Contains(path);
    }
}

internal sealed record StreamContractDrift(
    StreamContractDriftCategory Category,
    StreamContractDriftKind Kind,
    string Path,
    string Message)
{
    public static StreamContractDrift Structural(StreamContractDriftKind kind, string path, string message)
    {
        return new StreamContractDrift(StreamContractDriftCategory.Structural, kind, path, message);
    }

    public static StreamContractDrift CompatibilityReview(StreamContractDriftKind kind, string path, string message)
    {
        return new StreamContractDrift(StreamContractDriftCategory.CompatibilityReview, kind, path, message);
    }
}

internal enum StreamContractDriftCategory
{
    Structural,
    CompatibilityReview
}

internal enum StreamContractDriftKind
{
    MissingField,
    UnexpectedField,
    ValueKindChanged,
    ValueChanged,
    ArrayLengthChanged
}
