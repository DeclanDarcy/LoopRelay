using System.Text;
using System.Text.RegularExpressions;

namespace CommandCenter.Roadmap.Cli;

internal sealed partial class NorthStarContextLoader(RoadmapArtifacts artifacts)
{
    public async Task<NorthStarContext> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var missing = new List<string>();
        var contents = new List<(string Path, string FileName, string Content)>();
        foreach (string path in RoadmapArtifactPaths.NorthStarSourceFiles)
        {
            string? content = await artifacts.ReadAsync(path);
            if (content is null)
            {
                missing.Add(path);
            }
            else
            {
                contents.Add((path, Path.GetFileName(path), content));
            }
        }

        IReadOnlyList<string> numberedFiles = await artifacts.ListAsync(".agents/north-star", "*.md");
        string[] extras = numberedFiles
            .Where(path => NumberedNorthStarFileRegex().IsMatch(Path.GetFileName(path)))
            .Where(path => !RoadmapArtifactPaths.NorthStarSourceFiles.Contains(path, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missing.Count > 0 || extras.Length > 0)
        {
            var message = new StringBuilder("North-star source contract violation.");
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
                message.Append("\nUnexpected numbered north-star source files were found. ")
                    .Append("The Core contract is exactly 01 through 08:");
                foreach (string path in extras)
                {
                    message.Append("\n- ").Append(path);
                }
            }

            throw new RoadmapStepException(message.ToString());
        }

        var builder = new StringBuilder();
        foreach ((_, string fileName, string content) in contents)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine().AppendLine();
            }

            builder.Append("<!-- BEGIN NORTH-STAR FILE: ").Append(fileName).AppendLine(" -->")
                .AppendLine()
                .AppendLine(content.TrimEnd())
                .AppendLine()
                .Append("<!-- END NORTH-STAR FILE: ").Append(fileName).Append(" -->");
        }

        string result = builder.ToString();
        return new NorthStarContext(RoadmapArtifactPaths.NorthStarSourceFiles, result, RoadmapHash.Sha256(result));
    }

    [GeneratedRegex(@"^\d{2}-.+\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedNorthStarFileRegex();
}

internal sealed record NorthStarContext(IReadOnlyList<string> SourceFiles, string Content, string Hash);
