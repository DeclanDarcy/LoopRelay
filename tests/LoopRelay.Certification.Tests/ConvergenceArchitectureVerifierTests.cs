using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class ConvergenceArchitectureVerifierTests
{
    [Fact]
    public void Verifier_derives_reduced_solution_and_singleton_production_graph_without_override()
    {
        string root = RepositoryRoot();
        ConvergenceArchitectureVerification result = new ConvergenceArchitectureVerifier().Verify(
            root, "test-build", "test-config", "test-profile");

        Assert.DoesNotContain(result.SolutionProjects,
            project => project.Contains("LoopRelay.Roadmap.Cli", StringComparison.Ordinal) ||
                project.Contains("LoopRelay.Plan.Cli", StringComparison.Ordinal));
        Assert.Equal(1, Metric("Production application boundaries").Actual);
        Assert.Equal(1, Metric("Production composition roots").Actual);
        Assert.Equal(1, Metric("Production orchestration kernels").Actual);
        Assert.Equal(1, Metric("Production workflow catalogs").Actual);
        Assert.Equal(0, Metric("Behavior reachable only through retired code").Actual);
        Assert.Equal("test-build", result.BuildIdentity);
        Assert.False(string.IsNullOrWhiteSpace(result.CatalogIdentity));
        Assert.True(result.Passed, string.Join(Environment.NewLine,
            result.Metrics.Where(metric => !metric.Passed)
                .Select(metric => $"{metric.Name}: actual={metric.Actual}, target={metric.Target}, offenders={string.Join(',', metric.Offenders)}")));

        ArchitectureMetric Metric(string name) => result.Metrics.Single(metric => metric.Name == name);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LoopRelay.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
