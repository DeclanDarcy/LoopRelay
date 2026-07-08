using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Completion;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;
using ProjectContextLoader = LoopRelay.Roadmap.Cli.ProjectContextLoader;
using RoadmapArtifacts = LoopRelay.Roadmap.Cli.RoadmapArtifacts;

namespace LoopRelay.Roadmap.Cli.Tests;

internal sealed class TempRepo : IDisposable
{
    public TempRepo()
    {
        Root = Path.Combine(Path.GetTempPath(), "cc-roadmap-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = Root,
        };
        Store = new FileSystemArtifactStore();
        Artifacts = new RoadmapArtifacts(Store, Repository);
    }

    public string Root { get; }
    public Repository Repository { get; }
    public FileSystemArtifactStore Store { get; }
    public RoadmapArtifacts Artifacts { get; }

    public void Write(string relativePath, string content)
    {
        string path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public void SeedProjectContext()
    {
        int index = 1;
        foreach (string path in Cli.RoadmapArtifactPaths.ProjectContextSourceFiles)
        {
            Write(path, $"project context {index:00}");
            index++;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
