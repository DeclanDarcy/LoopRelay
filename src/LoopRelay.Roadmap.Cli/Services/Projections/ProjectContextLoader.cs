using System.Text;
using LoopRelay.Core.Services.ProjectContext;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.State;

namespace LoopRelay.Roadmap.Cli.Services.Projections;

internal sealed class ProjectContextLoader(RoadmapArtifacts _artifacts)
{
    public async Task<ProjectContext> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var missing = new List<string>();
        var contents = new List<(string Path, string FileName, string Content)>();
        foreach (string path in RoadmapArtifactPaths.ProjectContextSourceFiles)
        {
            string? content = await _artifacts.ReadAsync(path);
            if (content is null)
            {
                missing.Add(path);
            }
            else
            {
                contents.Add((path, Path.GetFileName(path), content));
            }
        }

        IReadOnlyList<string> numberedFiles = await _artifacts.ListAsync(RoadmapArtifactPaths.ProjectContextDirectory, "*.md");
        string[] extras = numberedFiles
            .Where(path => ProjectContextSourceContract.IsNumberedSourceFileName(Path.GetFileName(path)))
            .Where(path => !ProjectContextSourceContract.IsCanonicalSourceFile(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missing.Count > 0 || extras.Length > 0)
        {
            throw new RoadmapStepException(ProjectContextSourceContract.BuildViolationMessage(missing, extras));
        }

        var builder = new StringBuilder();
        foreach ((_, string fileName, string content) in contents)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine().AppendLine();
            }

            builder.Append("<!-- BEGIN PROJECT-CONTEXT FILE: ").Append(fileName).AppendLine(" -->")
                .AppendLine()
                .AppendLine(content.TrimEnd())
                .AppendLine()
                .Append("<!-- END PROJECT-CONTEXT FILE: ").Append(fileName).Append(" -->");
        }

        string result = builder.ToString();
        return new ProjectContext(RoadmapArtifactPaths.ProjectContextSourceFiles, result, RoadmapHash.Sha256(result));
    }
}
