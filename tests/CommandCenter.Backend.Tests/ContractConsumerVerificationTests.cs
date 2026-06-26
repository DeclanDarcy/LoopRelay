using System.Text.Json;

namespace CommandCenter.Backend.Tests;

public sealed class ContractConsumerVerificationTests
{
    [Fact]
    public void RepositoryDashboardRustMirrorReportsKnownDecisionSessionSummaryOmission()
    {
        JsonElement backendDashboardItem = ReadRepositoryDashboardGoldenFixture()[0];
        RustContractShapeProvider rustShapes = ReadRustContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "Rust shell RepositoryDashboardProjection",
            "runtime consumer",
            rustShapes.GetShape("RepositoryDashboardProjection")));

        ConsumerContractDrift[] drifts = verifier.Compare("$[]", backendDashboardItem).ToArray();

        ConsumerContractDrift drift = Assert.Single(drifts);
        Assert.Equal(ConsumerContractDriftKind.MissingDownstreamField, drift.Kind);
        Assert.Equal("$[].decisionSessionSummary", drift.Path);
        Assert.Equal("Rust shell RepositoryDashboardProjection", drift.Consumer);
        Assert.Equal("runtime consumer", drift.ConsumerCategory);
        Assert.Equal(
            "backend serialized field is omitted by the downstream mirror",
            drift.Message);
    }

    [Fact]
    public void RepositoryDashboardRustMirrorRecursivelyVerifiesMirroredNestedShape()
    {
        JsonElement backendDashboardItem = ReadRepositoryDashboardGoldenFixture()[0];
        RustContractShapeProvider rustShapes = ReadRustContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "Rust shell RepositoryDashboardProjection",
            "runtime consumer",
            rustShapes.GetShape("RepositoryDashboardProjection")));

        ConsumerContractDrift[] drifts = verifier.Compare("$[]", backendDashboardItem).ToArray();

        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].repository.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].executionSummary.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].executionHistory[].", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].continuitySummary.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].reasoningSummary.", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryDashboardTypeScriptTypeMatchesGoldenFixture()
    {
        JsonElement backendDashboardItem = ReadRepositoryDashboardGoldenFixture()[0];
        TypeScriptContractShapeProvider typeScriptShapes = ReadTypeScriptContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "TypeScript RepositoryDashboardProjection",
            "compile-time consumer",
            typeScriptShapes.GetShape("RepositoryDashboardProjection")));

        ConsumerContractDrift[] drifts = verifier.Compare("$[]", backendDashboardItem).ToArray();

        Assert.Empty(drifts);
    }

    [Fact]
    public void RepositoryDashboardTypeScriptTypeRecursivelyVerifiesImportedNestedShape()
    {
        JsonElement backendDashboardItem = ReadRepositoryDashboardGoldenFixture()[0];
        TypeScriptContractShapeProvider typeScriptShapes = ReadTypeScriptContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "TypeScript RepositoryDashboardProjection",
            "compile-time consumer",
            typeScriptShapes.GetShape("RepositoryDashboardProjection")));

        ConsumerContractDrift[] drifts = verifier.Compare("$[]", backendDashboardItem).ToArray();

        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].executionSummary.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].executionHistory[].", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].decisionSessionSummary.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].decisionSessionSummary.healthDimensions[].", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].decisionSessionSummary.recentTransferLineage[].", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryDashboardDevTauriMockMatchesGoldenFixture()
    {
        JsonElement backendDashboardItem = ReadRepositoryDashboardGoldenFixture()[0];
        TypeScriptContractShapeProvider typeScriptShapes = ReadTypeScriptContractShapes();
        DevTauriMockShapeProvider mockShapes = ReadDevTauriMockShapes(typeScriptShapes);
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "devTauriMock dashboardEntry",
            "development/test consumer",
            mockShapes.GetDashboardEntryShape()));

        ConsumerContractDrift[] drifts = verifier.Compare("$[]", backendDashboardItem).ToArray();

        Assert.Empty(drifts);
    }

    [Fact]
    public void RepositoryDashboardDevTauriMockRecursivelyVerifiesInlineContinuityShape()
    {
        JsonElement backendDashboardItem = ReadRepositoryDashboardGoldenFixture()[0];
        TypeScriptContractShapeProvider typeScriptShapes = ReadTypeScriptContractShapes();
        DevTauriMockShapeProvider mockShapes = ReadDevTauriMockShapes(typeScriptShapes);
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "devTauriMock dashboardEntry",
            "development/test consumer",
            mockShapes.GetDashboardEntryShape()));

        ConsumerContractDrift[] drifts = verifier.Compare("$[]", backendDashboardItem).ToArray();

        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].continuitySummary.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].reasoningSummary.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$[].decisionSessionSummary.", StringComparison.Ordinal));
    }

    [Fact]
    public void ConsumerVerifierReportsNestedMissingFields()
    {
        using JsonDocument backend = JsonDocument.Parse("""
            {
              "repository": {
                "id": "repository-id",
                "name": "Repository",
                "path": "C:/Repository"
              }
            }
            """);
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "Synthetic consumer",
            "synthetic consumer category",
            ConsumerContractShape.Object(new Dictionary<string, ConsumerContractShape>(StringComparer.Ordinal)
            {
                ["repository"] = ConsumerContractShape.Object(new Dictionary<string, ConsumerContractShape>(StringComparer.Ordinal)
                {
                    ["id"] = ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.String),
                    ["name"] = ConsumerContractShape.Primitive(ConsumerContractPrimitiveKind.String)
                })
            })));

        ConsumerContractDrift[] drifts = verifier.Compare("$", backend.RootElement).ToArray();

        ConsumerContractDrift drift = Assert.Single(drifts);
        Assert.Equal(ConsumerContractDriftKind.MissingDownstreamField, drift.Kind);
        Assert.Equal("$.repository.path", drift.Path);
        Assert.Equal("synthetic consumer category", drift.ConsumerCategory);
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

    private static RustContractShapeProvider ReadRustContractShapes()
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.Shell", "src", "main.rs"));

        return RustContractShapeProvider.Parse(source);
    }

    private static TypeScriptContractShapeProvider ReadTypeScriptContractShapes()
    {
        DirectoryInfo typesDirectory = FindRepositoryRoot()
            .CombineDirectory("src", "CommandCenter.UI", "src", "types");

        return TypeScriptContractShapeProvider.Parse(typesDirectory);
    }

    private static DevTauriMockShapeProvider ReadDevTauriMockShapes(TypeScriptContractShapeProvider typeScriptShapes)
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.UI", "src", "devTauriMock.ts"));

        return DevTauriMockShapeProvider.Parse(source, typeScriptShapes);
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

}

internal static class DirectoryInfoExtensions
{
    public static string Combine(this DirectoryInfo directory, params string[] paths)
    {
        return Path.Combine([directory.FullName, .. paths]);
    }

    public static DirectoryInfo CombineDirectory(this DirectoryInfo directory, params string[] paths)
    {
        return new DirectoryInfo(directory.Combine(paths));
    }
}
