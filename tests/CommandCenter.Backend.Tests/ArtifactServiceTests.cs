using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Tests;

public sealed class ArtifactServiceTests
{
    [Fact]
    public async Task DiscoversKnownArtifactCategories()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "plan");
        await WriteAsync(repository, ".agents/operational_context.md", "context");
        await WriteAsync(repository, ".agents/milestones/m1.md", "milestone");
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "handoff");
        await WriteAsync(repository, ".agents/handoffs/handoff.0001.md", "historical handoff");
        await WriteAsync(repository, ".agents/handoffs/notes.md", "not a handoff artifact");
        await WriteAsync(repository, ".agents/handoffs/handoff.0000.md", "invalid historical handoff");
        await WriteAsync(repository, ".agents/handoffs/handoff.001.md", "invalid historical handoff");
        await WriteAsync(repository, ".agents/decisions/decisions.md", "decisions");
        await WriteAsync(repository, ".agents/decisions/decisions.0001.md", "historical decisions");
        await WriteAsync(repository, ".agents/decisions/notes.md", "not a decision artifact");
        var service = new ArtifactService(new FileSystemArtifactStore());

        IReadOnlyList<Artifact> artifacts = await service.DiscoverAsync(repository);

        Assert.Contains(artifacts, artifact => artifact.RelativePath == ".agents/plan.md" && artifact.Type == ArtifactType.Plan && artifact.Family == ArtifactFamily.Plan);
        Assert.Contains(artifacts, artifact => artifact.RelativePath == ".agents/operational_context.md" && artifact.Type == ArtifactType.OperationalContext && artifact.Family == ArtifactFamily.OperationalContext);
        Assert.Contains(artifacts, artifact => artifact.RelativePath == ".agents/milestones/m1.md" && artifact.Type == ArtifactType.Milestone && artifact.Family == ArtifactFamily.Milestone);
        Assert.Contains(artifacts, artifact => artifact.RelativePath == ".agents/handoffs/handoff.md" && artifact.VersionKind == ArtifactVersionKind.Current);
        Assert.Contains(artifacts, artifact => artifact.RelativePath == ".agents/handoffs/handoff.0001.md" && artifact.VersionKind == ArtifactVersionKind.Historical);
        Assert.Contains(artifacts, artifact => artifact.RelativePath == ".agents/decisions/decisions.md" && artifact.VersionKind == ArtifactVersionKind.Current);
        Assert.Contains(artifacts, artifact => artifact.RelativePath == ".agents/decisions/decisions.0001.md" && artifact.VersionKind == ArtifactVersionKind.Historical);
        Assert.DoesNotContain(artifacts, artifact => artifact.RelativePath == ".agents/handoffs/notes.md");
        Assert.DoesNotContain(artifacts, artifact => artifact.RelativePath == ".agents/handoffs/handoff.0000.md");
        Assert.DoesNotContain(artifacts, artifact => artifact.RelativePath == ".agents/handoffs/handoff.001.md");
        Assert.DoesNotContain(artifacts, artifact => artifact.RelativePath == ".agents/decisions/notes.md");
    }

    [Fact]
    public async Task MissingArtifactsAndDirectoriesDoNotFailDiscovery()
    {
        Repository repository = CreateRepository();
        var service = new ArtifactService(new FileSystemArtifactStore());

        IReadOnlyList<Artifact> artifacts = await service.DiscoverAsync(repository);

        Assert.Empty(artifacts);
    }

    [Fact]
    public async Task LoadsAndSavesArtifactContent()
    {
        Repository repository = CreateRepository();
        var service = new ArtifactService(new FileSystemArtifactStore());

        await service.SaveAsync(repository, ".agents/handoffs/handoff.md", "updated");

        Assert.True(await service.ExistsAsync(repository, ".agents/handoffs/handoff.md"));
        Assert.Equal("updated", await service.LoadAsync(repository, ".agents/handoffs/handoff.md"));
    }

    [Fact]
    public async Task CurrentArtifactsResolveOnlyCurrentFiles()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/handoffs/handoff.0001.md", "historical handoff");
        await WriteAsync(repository, ".agents/decisions/decisions.0001.md", "historical decisions");
        var service = new ArtifactService(new FileSystemArtifactStore());

        Assert.Null(await service.GetCurrentHandoffAsync(repository));
        Assert.Null(await service.GetCurrentOperationalContextAsync(repository));
        Assert.Null(await service.GetCurrentDecisionsAsync(repository));

        await WriteAsync(repository, ".agents/operational_context.md", "context");
        await WriteAsync(repository, ".agents/handoffs/handoff.md", "handoff");
        await WriteAsync(repository, ".agents/decisions/decisions.md", "decisions");

        Assert.Equal(".agents/handoffs/handoff.md", (await service.GetCurrentHandoffAsync(repository))?.RelativePath);
        Assert.Equal(".agents/operational_context.md", (await service.GetCurrentOperationalContextAsync(repository))?.RelativePath);
        Assert.Equal(".agents/decisions/decisions.md", (await service.GetCurrentDecisionsAsync(repository))?.RelativePath);
    }

    [Fact]
    public async Task RejectsRelativePathTraversal()
    {
        Repository repository = CreateRepository();
        var service = new ArtifactService(new FileSystemArtifactStore());

        await Assert.ThrowsAsync<ArgumentException>(() => service.LoadAsync(repository, "../outside.md"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(repository, "../outside.md", "outside"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExistsAsync(repository, "../outside.md"));
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
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
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
