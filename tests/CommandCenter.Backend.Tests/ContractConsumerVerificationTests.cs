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
    public void RepositoryDashboardGeneratedTypeScriptAliasMatchesGoldenFixture()
    {
        JsonElement backendDashboardItem = ReadRepositoryDashboardGoldenFixture()[0];
        TypeScriptContractShapeProvider typeScriptShapes = ReadTypeScriptContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "Generated TypeScript RepositoryDashboardGeneratedProjection",
            "generated compile-time consumer",
            typeScriptShapes.GetShape("RepositoryDashboardGeneratedProjection")));

        ConsumerContractDrift[] drifts = verifier.Compare("$[]", backendDashboardItem).ToArray();

        Assert.Empty(drifts);
    }

    [Fact]
    public void RepositoryDashboardProductionConsumerCandidateStructurallyMatchesCompatibilityWrapper()
    {
        TypeScriptContractShapeProvider typeScriptShapes = ReadTypeScriptContractShapes();

        AssertConsumerShapeEquivalent(
            "$[]",
            typeScriptShapes.GetShape("RepositoryDashboardProjection"),
            typeScriptShapes.GetShape("RepositoryDashboardConsumerCandidateProjection"));
    }

    [Fact]
    public void RepositoryDashboardProductionConsumerCandidateCarriesSemanticCompatibilityMetadata()
    {
        TypeScriptContractShapeProvider typeScriptShapes = ReadTypeScriptContractShapes();

        AssertConsumerShapeEquivalent(
            "$[].availability",
            typeScriptShapes.GetPropertyShape("RepositoryDashboardProjection", "availability"),
            typeScriptShapes.GetPropertyShape("RepositoryDashboardConsumerCandidateProjection", "availability"));
        AssertConsumerShapeEquivalent(
            "$[].executionSummary",
            typeScriptShapes.GetPropertyShape("RepositoryDashboardProjection", "executionSummary"),
            typeScriptShapes.GetPropertyShape("RepositoryDashboardConsumerCandidateProjection", "executionSummary"));
        AssertConsumerShapeEquivalent(
            "$[].activeExecutionSession",
            typeScriptShapes.GetPropertyShape("RepositoryDashboardProjection", "activeExecutionSession"),
            typeScriptShapes.GetPropertyShape("RepositoryDashboardConsumerCandidateProjection", "activeExecutionSession"));
        AssertConsumerShapeEquivalent(
            "$[].decisionSessionSummary.state",
            typeScriptShapes.GetPropertyShape("RepositoryDashboardProjection", "decisionSessionSummary", "state"),
            typeScriptShapes.GetPropertyShape("RepositoryDashboardConsumerCandidateProjection", "decisionSessionSummary", "state"));

        string generatedSource = File.ReadAllText(FindRepositoryRoot().Combine(
            "src",
            "CommandCenter.UI",
            "src",
            "contracts",
            "generated",
            "repository-dashboard.generated.ts"));
        Assert.Contains("availability: 'Available' | 'Missing' | 'AccessDenied'", generatedSource, StringComparison.Ordinal);
        Assert.Contains("executionState: 'Ready' | 'Executing' | 'AwaitingAcceptance' | 'Accepted' | 'AwaitingCommit' | 'AwaitingPush' | 'Failed' | 'Cancelled'", generatedSource, StringComparison.Ordinal);
        Assert.Contains("state: 'Created' | 'Active' | 'TransferPending' | 'Transferred' | 'Retired' | null", generatedSource, StringComparison.Ordinal);
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
    public void RepositoryWorkspaceRustMirrorReportsKnownDecisionSessionSummaryOmission()
    {
        JsonElement backendWorkspace = ReadRepositoryWorkspaceGoldenFixture();
        RustContractShapeProvider rustShapes = ReadRustContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "Rust shell RepositoryWorkspaceProjection",
            "runtime consumer",
            rustShapes.GetShape("RepositoryWorkspaceProjection")));

        ConsumerContractDrift[] drifts = verifier.Compare("$", backendWorkspace).ToArray();

        ConsumerContractDrift drift = Assert.Single(drifts);
        Assert.Equal(ConsumerContractDriftKind.MissingDownstreamField, drift.Kind);
        Assert.Equal("$.decisionSessionSummary", drift.Path);
        Assert.Equal("Rust shell RepositoryWorkspaceProjection", drift.Consumer);
        Assert.Equal("runtime consumer", drift.ConsumerCategory);
        Assert.Equal(
            "backend serialized field is omitted by the downstream mirror",
            drift.Message);
    }

    [Fact]
    public void RepositoryWorkspaceRustMirrorRecursivelyVerifiesMirroredNestedShape()
    {
        JsonElement backendWorkspace = ReadRepositoryWorkspaceGoldenFixture();
        RustContractShapeProvider rustShapes = ReadRustContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "Rust shell RepositoryWorkspaceProjection",
            "runtime consumer",
            rustShapes.GetShape("RepositoryWorkspaceProjection")));

        ConsumerContractDrift[] drifts = verifier.Compare("$", backendWorkspace).ToArray();

        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$.repository.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$.executionSummary.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$.executionHistory[].", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$.artifactInventory.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$.operationalContextProposalSummary.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$.operationalContext.", StringComparison.Ordinal));
        Assert.DoesNotContain(drifts, drift => drift.Path.StartsWith("$.reasoningSummary.", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryWorkspaceTypeScriptTypeMatchesGoldenFixture()
    {
        JsonElement backendWorkspace = ReadRepositoryWorkspaceGoldenFixture();
        TypeScriptContractShapeProvider typeScriptShapes = ReadTypeScriptContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "TypeScript RepositoryWorkspaceProjection",
            "compile-time consumer",
            typeScriptShapes.GetShape("RepositoryWorkspaceProjection")));

        ConsumerContractDrift[] drifts = verifier.Compare("$", backendWorkspace).ToArray();

        Assert.Empty(drifts);
    }

    [Fact]
    public void RepositoryWorkspaceDevTauriMockPayloadMatchesGoldenFixture()
    {
        JsonElement backendWorkspace = ReadRepositoryWorkspaceGoldenFixture();
        TypeScriptContractShapeProvider typeScriptShapes = ReadTypeScriptContractShapes();
        DevTauriMockShapeProvider mockShapes = ReadDevTauriMockShapes(typeScriptShapes);
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "devTauriMock get_repository_workspace payload",
            "development/test consumer",
            mockShapes.GetWorkspaceCommandPayloadShape()));

        ConsumerContractDrift[] drifts = verifier.Compare("$", backendWorkspace).ToArray();

        Assert.Empty(drifts);
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

    private static JsonElement ReadRepositoryWorkspaceGoldenFixture()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "ContractFixtures",
            "repository-workspace.golden.json");
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
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        DirectoryInfo typesDirectory = repositoryRoot
            .CombineDirectory("src", "CommandCenter.UI", "src", "types");
        DirectoryInfo generatedContractsDirectory = repositoryRoot
            .CombineDirectory("src", "CommandCenter.UI", "src", "contracts", "generated");

        return TypeScriptContractShapeProvider.Parse([typesDirectory, generatedContractsDirectory]);
    }

    private static DevTauriMockShapeProvider ReadDevTauriMockShapes(TypeScriptContractShapeProvider typeScriptShapes)
    {
        string source = File.ReadAllText(FindRepositoryRoot()
            .Combine("src", "CommandCenter.UI", "src", "devTauriMock.ts"));

        return DevTauriMockShapeProvider.Parse(source, typeScriptShapes);
    }

    private static void AssertConsumerShapeEquivalent(
        string path,
        ConsumerContractShape expected,
        ConsumerContractShape actual)
    {
        Assert.Equal(expected.IsNullable, actual.IsNullable);

        ConsumerContractShape nonNullableExpected = expected.WithoutNullability();
        ConsumerContractShape nonNullableActual = actual.WithoutNullability();
        Assert.Equal(nonNullableExpected.Kind, nonNullableActual.Kind);
        Assert.Equal(nonNullableExpected.PrimitiveKind, nonNullableActual.PrimitiveKind);

        if (nonNullableExpected.Kind == ConsumerContractShapeKind.Object)
        {
            Assert.Empty(nonNullableExpected.Properties.Keys.Except(nonNullableActual.Properties.Keys, StringComparer.Ordinal));
            Assert.Empty(nonNullableActual.Properties.Keys.Except(nonNullableExpected.Properties.Keys, StringComparer.Ordinal));

            foreach ((string name, ConsumerContractShape expectedProperty) in nonNullableExpected.Properties)
            {
                AssertConsumerShapeEquivalent($"{path}.{name}", expectedProperty, nonNullableActual.Properties[name]);
            }
        }

        if (nonNullableExpected.Kind == ConsumerContractShapeKind.Array)
        {
            Assert.NotNull(nonNullableExpected.ItemShape);
            Assert.NotNull(nonNullableActual.ItemShape);
            AssertConsumerShapeEquivalent($"{path}[]", nonNullableExpected.ItemShape, nonNullableActual.ItemShape);
        }
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
