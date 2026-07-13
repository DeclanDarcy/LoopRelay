using Xunit;

namespace LoopRelay.Projections.Tests.Services;

public sealed class ProjectionLayeringTests
{
    [Fact]
    public void PlanMainAndSharedProjectionProjects_DoNotReferenceRoadmapCli()
    {
        string root = FindRepositoryRoot();
        foreach (string project in new[]
        {
            "src/LoopRelay.Cli/LoopRelay.Cli.csproj",
            "src/LoopRelay.Projections/LoopRelay.Projections.csproj",
        })
        {
            string content = File.ReadAllText(Path.Combine(root, project));

            Assert.DoesNotContain("LoopRelay.Roadmap.Cli", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("LoopRelay.Roadmap.Cli.csproj", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LoopRelay.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
