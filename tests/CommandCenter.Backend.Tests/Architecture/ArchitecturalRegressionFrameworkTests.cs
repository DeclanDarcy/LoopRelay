using System.Reflection;

namespace CommandCenter.Backend.Tests.Architecture;

public sealed class ArchitecturalRegressionFrameworkTests
{
    private static readonly string[] RequiredInvariantCatalogColumns =
    [
        "Invariant",
        "Protecting mechanism",
        "Owner",
        "Severity",
        "Evidence",
        "Drift model",
        "Current coverage",
        "Enforcement strength"
    ];

    private static readonly string[] RequiredInvariantCatalogEntries =
    [
        "Backend domain services compute semantic meaning.",
        "Projections expose authoritative meaning and do not create new meaning.",
        "Contracts describe externally observable projection shape through the canonical Oracle.",
        "Transport preserves request, response, status, null, empty, and error semantics without domain participation.",
        "TypeScript clients and React consume authoritative facts without semantic inference.",
        "Every mutable state has one owner.",
        "Feature controllers own resources, actions, refresh, loading, errors, and view-model construction.",
        "Workspaces compose controllers and local interaction flow only.",
        "Application root composes repository selection, global shell state, primary navigation, and workspaces only.",
        "Runtime failures are typed, scoped, observable, and recoverable at the smallest valid boundary.",
        "Architectural decisions govern changes to authority, ownership, contracts, transport, runtime, and mechanisms.",
        "Architectural evidence supports decisions, mechanisms, certification, acceptance, and baselines.",
        "Generated artifacts are replaced wholesale and manual edits to generated output are forbidden.",
        "Compatibility fields are transitional and derive from structured authority fields.",
        "Projection, authority, and presentation taxonomies remain distinct.",
        "Architectural mechanisms cannot disappear, weaken, or lose fixture wiring silently."
    ];

    private static readonly ArchitecturalRegressionMechanism[] RequiredMechanisms =
    [
        new(
            "Contract Oracle fixture drift detection",
            typeof(ContractOracleFixtureTests),
            "backend architecture tests",
            "local build failure",
            "Backend serialization must not drift from accepted Oracle fixtures without review.",
            "Update the backend projection intentionally, refresh the Oracle fixture, and record evidence."),
        new(
            "Contract consumer verification",
            typeof(ContractConsumerVerificationTests),
            "backend architecture tests",
            "local build failure",
            "Downstream contract mirrors must stay visible while generated contracts are deferred.",
            "Update or quarantine the consumer representation and record compatibility evidence."),
        new(
            "Contract artifact freshness",
            typeof(ContractGeneratedArtifactFreshnessTests),
            "backend architecture tests",
            "local build failure",
            "Verified manual contract artifacts must remain tied to the fixture baseline.",
            "Refresh the manual artifact or manifest through the Oracle change workflow."),
        new(
            "Contract request boundary",
            typeof(ContractRequestBoundaryTests),
            "backend architecture tests",
            "local build failure",
            "Request shape must remain explicit at backend, shell, and TypeScript boundaries.",
            "Update the route or command contract intentionally and record request-boundary evidence."),
        new(
            "Architectural regression framework wiring",
            typeof(ArchitecturalRegressionFrameworkTests),
            "backend architecture tests",
            "local build failure",
            "Architectural mechanisms must be discoverable as regression targets.",
            "Keep the mechanism catalog, test namespace, and fixture wiring aligned.")
    ];

    [Fact]
    public void RequiredArchitectureMechanismsAreDiscoverableByBackendTestAssembly()
    {
        Type[] testTypes = typeof(ArchitecturalRegressionFrameworkTests)
            .Assembly
            .GetTypes()
            .Where(HasXunitTestMethod)
            .ToArray();

        foreach (ArchitecturalRegressionMechanism mechanism in RequiredMechanisms)
        {
            Assert.Contains(mechanism.TestType, testTypes);
        }
    }

    [Fact]
    public void RequiredArchitectureMechanismsExplainIntentAndRemediation()
    {
        foreach (ArchitecturalRegressionMechanism mechanism in RequiredMechanisms)
        {
            Assert.False(string.IsNullOrWhiteSpace(mechanism.Name));
            Assert.False(string.IsNullOrWhiteSpace(mechanism.Owner));
            Assert.False(string.IsNullOrWhiteSpace(mechanism.Severity));
            Assert.False(string.IsNullOrWhiteSpace(mechanism.Intent));
            Assert.False(string.IsNullOrWhiteSpace(mechanism.Remediation));
        }
    }

    [Fact]
    public void OracleFixtureWiringIsPresentInBackendTestOutput()
    {
        string fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "ContractFixtures");
        string[] requiredFixtures =
        [
            "repository-dashboard.golden.json",
            "repository-workspace.golden.json",
            "workflow-instance.golden.json"
        ];

        foreach (string fixture in requiredFixtures)
        {
            string fixturePath = Path.Combine(fixtureDirectory, fixture);

            Assert.True(
                File.Exists(fixturePath),
                $"{fixture} must be copied to backend test output so architectural regressions can run from the compiled test assembly. Check CommandCenter.Backend.Tests.csproj ContractFixtures wiring.");
        }
    }

    [Fact]
    public void InvariantCatalogDefinesRequiredMetadataForEveryCoreInvariant()
    {
        IReadOnlyList<IReadOnlyDictionary<string, string>> catalog = ReadInvariantCatalog();

        foreach (string invariant in RequiredInvariantCatalogEntries)
        {
            Assert.Contains(
                catalog,
                row => row["Invariant"] == invariant);
        }

        foreach (IReadOnlyDictionary<string, string> row in catalog)
        {
            foreach (string column in RequiredInvariantCatalogColumns)
            {
                Assert.True(
                    row.TryGetValue(column, out string? value) && HasAcceptedCatalogValue(value),
                    $"Architectural invariant catalog row '{row["Invariant"]}' must populate '{column}'. Architectural regressions need invariant, mechanism, owner, severity, evidence, drift, coverage, and enforcement metadata before future slices rely on the catalog. Add the missing metadata or quarantine the invariant with evidence.");
            }
        }
    }

    private static bool HasXunitTestMethod(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(method => method.GetCustomAttributes()
                .Any(attribute => attribute.GetType().FullName is "Xunit.FactAttribute" or "Xunit.TheoryAttribute"));
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadInvariantCatalog()
    {
        string mechanismsDocumentPath = Path.Combine(
            FindRepositoryRoot().FullName,
            "docs",
            "architectural-mechanisms.md");

        string[] lines = File.ReadAllLines(mechanismsDocumentPath);
        int headingIndex = Array.FindIndex(lines, line => line.Trim() == "### Architectural Invariant Catalog");

        Assert.True(
            headingIndex >= 0,
            "docs/architectural-mechanisms.md must define '### Architectural Invariant Catalog'. This catalog is the M0.3 canonical mapping from core invariants to planned executable mechanisms.");

        int headerIndex = Array.FindIndex(lines, headingIndex, line => line.StartsWith("| Invariant |", StringComparison.Ordinal));

        Assert.True(
            headerIndex > headingIndex,
            "Architectural invariant catalog must use a markdown table whose first column is 'Invariant'.");

        string[] columns = SplitMarkdownTableRow(lines[headerIndex]);

        Assert.Equal(RequiredInvariantCatalogColumns, columns);

        List<IReadOnlyDictionary<string, string>> rows = [];

        for (int i = headerIndex + 2; i < lines.Length; i++)
        {
            string line = lines[i];

            if (!line.StartsWith("|", StringComparison.Ordinal))
            {
                break;
            }

            string[] values = SplitMarkdownTableRow(line);

            Assert.True(
                values.Length == columns.Length,
                $"Architectural invariant catalog row has {values.Length} columns but expected {columns.Length}: {line}");

            rows.Add(columns
                .Zip(values, static (column, value) => new { column, value })
                .ToDictionary(pair => pair.column, pair => pair.value));
        }

        Assert.NotEmpty(rows);

        return rows;
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CommandCenter.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory;
    }

    private static string[] SplitMarkdownTableRow(string line)
    {
        return line
            .Trim()
            .Trim('|')
            .Split('|')
            .Select(value => value.Trim())
            .ToArray();
    }

    private static bool HasAcceptedCatalogValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, "TBD", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value, "TODO", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(value, "None", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ArchitecturalRegressionMechanism(
        string Name,
        Type TestType,
        string Owner,
        string Severity,
        string Intent,
        string Remediation);
}
