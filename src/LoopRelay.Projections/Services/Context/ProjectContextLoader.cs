using System.Text;
using LoopRelay.Core.Services.ProjectContext;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Services.Definitions;

namespace LoopRelay.Projections.Services.Context;

public sealed class ProjectContextLoader(ProjectionArtifacts.ProjectionArtifacts _artifacts)
{
    public async Task<ProjectContext> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var missing = new List<string>();
        var contents = new List<(string Path, string FileName, string Content)>();
        foreach (string path in ProjectionArtifactPaths.ProjectContextSourceFiles)
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

        IReadOnlyList<string> numberedFiles = await _artifacts.ListAsync(ProjectionArtifactPaths.ProjectContextDirectory, "*.md");
        string[] extras = numberedFiles
            .Where(path => ProjectContextSourceContract.IsNumberedSourceFileName(Path.GetFileName(path)))
            .Where(path => !ProjectContextSourceContract.IsCanonicalSourceFile(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missing.Count > 0 || extras.Length > 0)
        {
            throw new ProjectionException(ProjectContextSourceContract.BuildViolationMessage(missing, extras));
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
        return new ProjectContext(ProjectionArtifactPaths.ProjectContextSourceFiles, result, ProjectionHash.Sha256(result));
    }
}
