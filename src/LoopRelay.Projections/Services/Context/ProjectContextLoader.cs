using System.Text;
using System.Text.RegularExpressions;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Services.Definitions;

namespace LoopRelay.Projections.Services.Context;

public sealed partial class ProjectContextLoader(ProjectionArtifacts.ProjectionArtifacts _artifacts)
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
            .Where(path => NumberedProjectContextFileRegex().IsMatch(Path.GetFileName(path)))
            .Where(path => !ProjectionArtifactPaths.ProjectContextSourceFiles.Contains(path, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missing.Count > 0 || extras.Length > 0)
        {
            var message = new StringBuilder("Project Context source contract violation.");
            if (missing.Count > 0)
            {
                message.Append("\nMissing required files:");
                foreach (string path in missing)
                {
                    message.Append("\n- ").Append(path);
                }
            }

            if (extras.Length > 0)
            {
                message.Append("\nUnexpected numbered Project Context source files were found. ")
                    .Append("The Core contract is exactly 01 through 08:");
                foreach (string path in extras)
                {
                    message.Append("\n- ").Append(path);
                }
            }

            throw new ProjectionException(message.ToString());
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

    [GeneratedRegex(@"^\d{2}-.+\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedProjectContextFileRegex();
}
