using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommandCenter.Backend.Tests;

public sealed class ContractConsumerVerificationTests
{
    [Fact]
    public void RepositoryDashboardRustMirrorReportsKnownDecisionSessionSummaryOmission()
    {
        JsonElement backendDashboardItem = ReadRepositoryDashboardGoldenFixture()[0];
        IReadOnlySet<string> backendFields = backendDashboardItem
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        IReadOnlySet<string> rustMirrorFields = ReadRustStructJsonFields("RepositoryDashboardProjection");

        ConsumerContractDrift[] drifts = CompareConsumerFields(
            backendFields,
            rustMirrorFields,
            "$[]").ToArray();

        ConsumerContractDrift drift = Assert.Single(drifts);
        Assert.Equal(ConsumerContractDriftKind.MissingDownstreamField, drift.Kind);
        Assert.Equal("$[].decisionSessionSummary", drift.Path);
        Assert.Equal("Rust shell RepositoryDashboardProjection", drift.Consumer);
    }

    private static JsonElement ReadRepositoryDashboardGoldenFixture()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "ContractFixtures",
            "repository-dashboard.golden.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        return document.RootElement.Clone();
    }

    private static IReadOnlySet<string> ReadRustStructJsonFields(string structName)
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.Shell", "src", "main.rs"));

        var match = Regex.Match(
            source,
            $@"struct\s+{Regex.Escape(structName)}\s*\{{(?<body>.*?)\n\}}",
            RegexOptions.Singleline);
        Assert.True(match.Success, $"Rust struct {structName} should exist.");

        return match.Groups["body"].Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("#", StringComparison.Ordinal))
            .Select(line => Regex.Match(line, @"^(?<name>[A-Za-z_][A-Za-z0-9_]*):"))
            .Where(field => field.Success)
            .Select(field => ToCamelCase(field.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<ConsumerContractDrift> CompareConsumerFields(
        IReadOnlySet<string> backendFields,
        IReadOnlySet<string> consumerFields,
        string path)
    {
        foreach (string missing in backendFields.Except(consumerFields, StringComparer.Ordinal))
        {
            yield return new ConsumerContractDrift(
                "Rust shell RepositoryDashboardProjection",
                ConsumerContractDriftKind.MissingDownstreamField,
                $"{path}.{missing}",
                "backend serialized field is omitted by the downstream mirror");
        }

        foreach (string extra in consumerFields.Except(backendFields, StringComparer.Ordinal))
        {
            yield return new ConsumerContractDrift(
                "Rust shell RepositoryDashboardProjection",
                ConsumerContractDriftKind.ExtraDownstreamField,
                $"{path}.{extra}",
                "downstream mirror declares a field not present in backend serialization");
        }
    }

    private static string ToCamelCase(string snakeCase)
    {
        string[] parts = snakeCase.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return snakeCase;
        }

        return string.Concat(
            parts[0],
            string.Concat(parts.Skip(1).Select(part =>
                string.Concat(char.ToUpperInvariant(part[0]), part.AsSpan(1).ToString()))));
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(directory.Combine("src", "CommandCenter.Shell", "src", "main.rs")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed record ConsumerContractDrift(
        string Consumer,
        ConsumerContractDriftKind Kind,
        string Path,
        string Message);

    private enum ConsumerContractDriftKind
    {
        MissingDownstreamField,
        ExtraDownstreamField
    }
}

internal static class DirectoryInfoExtensions
{
    public static string Combine(this DirectoryInfo directory, params string[] paths)
    {
        return Path.Combine([directory.FullName, .. paths]);
    }
}
