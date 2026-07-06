using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Planning;
using LoopRelay.Core.Repositories;

namespace LoopRelay.Core.Tests;

public sealed class PlanningServiceTests
{
    [Fact]
    public async Task PlanPresenceReflectsPlanArtifact()
    {
        Repository repository = CreateRepository();
        var service = new PlanningService(new FileSystemArtifactStore());

        Assert.False(await service.HasPlanAsync(repository));

        await WriteAsync(repository, ".agents/plan.md", "plan");

        Assert.True(await service.HasPlanAsync(repository));
    }

    [Fact]
    public async Task MissingMilestoneDirectoryReturnsNoMilestones()
    {
        Repository repository = CreateRepository();
        var service = new PlanningService(new FileSystemArtifactStore());

        IReadOnlyList<Milestone> milestones = await service.GetMilestonesAsync(repository);

        Assert.Empty(milestones);
    }

    [Fact]
    public async Task EmptyMilestoneDirectoryReturnsNoMilestones()
    {
        Repository repository = CreateRepository();
        Directory.CreateDirectory(Path.Combine(repository.Path, ".agents", "milestones"));
        var service = new PlanningService(new FileSystemArtifactStore());

        IReadOnlyList<Milestone> milestones = await service.GetMilestonesAsync(repository);

        Assert.Empty(milestones);
    }

    [Fact]
    public async Task DiscoversMilestoneMarkdownFilesWithoutParsingContent()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/milestones/m2.md", "not structured milestone content");
        await WriteAsync(repository, ".agents/milestones/m1.md", "# M1");
        await WriteAsync(repository, ".agents/milestones/notes.txt", "not a milestone");
        var service = new PlanningService(new FileSystemArtifactStore());

        IReadOnlyList<Milestone> milestones = await service.GetMilestonesAsync(repository);

        Assert.Collection(
            milestones,
            milestone =>
            {
                Assert.Equal("m1.md", milestone.Name);
                Assert.Equal(".agents/milestones/m1.md", milestone.RelativePath);
            },
            milestone =>
            {
                Assert.Equal("m2.md", milestone.Name);
                Assert.Equal(".agents/milestones/m2.md", milestone.RelativePath);
            });
    }

    [Fact]
    public async Task MissingPlanReturnsMissingPlanReadiness()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/milestones/m1.md", "# M1");
        var service = new PlanningService(new FileSystemArtifactStore());

        ExecutionReadiness readiness = await service.DetermineReadinessAsync(repository);

        Assert.Equal(ExecutionReadiness.MissingPlan, readiness);
    }

    [Fact]
    public async Task PlanWithoutMilestonesReturnsMissingMilestonesReadiness()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "plan");
        var service = new PlanningService(new FileSystemArtifactStore());

        ExecutionReadiness readiness = await service.DetermineReadinessAsync(repository);

        Assert.Equal(ExecutionReadiness.MissingMilestones, readiness);
    }

    [Fact]
    public async Task PlanWithMilestonesReturnsReadyReadiness()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "plan");
        await WriteAsync(repository, ".agents/milestones/m1.md", "# M1");
        var service = new PlanningService(new FileSystemArtifactStore());

        ExecutionReadiness readiness = await service.DetermineReadinessAsync(repository);

        Assert.Equal(ExecutionReadiness.Ready, readiness);
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
    }

    private static Repository CreateRepository()
    {
        string directory = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, ".git"));

        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(directory),
            Path = directory
        };
    }
}
