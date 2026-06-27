using System.Reflection;

namespace CommandCenter.Backend.Tests.Architecture;

public sealed class ArchitecturalRegressionFrameworkTests
{
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

    private static bool HasXunitTestMethod(Type type)
    {
        return type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(method => method.GetCustomAttributes()
                .Any(attribute => attribute.GetType().FullName is "Xunit.FactAttribute" or "Xunit.TheoryAttribute"));
    }

    private sealed record ArchitecturalRegressionMechanism(
        string Name,
        Type TestType,
        string Owner,
        string Severity,
        string Intent,
        string Remediation);
}
