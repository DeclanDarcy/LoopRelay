using System.Text.RegularExpressions;
using LoopRelay.Roadmap.Cli.Abstractions;
using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed partial class EpicAuthoringOutputClassifier : IArtifactOutputClassifier
{
    public ArtifactOutputClassification Classify(string content)
    {
        string? heading = FirstTopLevelHeading(content);
        if (heading is null)
        {
            return new ArtifactOutputClassification(
                ArtifactOutputKind.Ambiguous,
                "Epic authoring output did not contain a top-level Markdown heading.");
        }

        if (BlockedHeadingRegex().IsMatch(heading))
        {
            return new ArtifactOutputClassification(
                ArtifactOutputKind.Blocked,
                $"Epic authoring intentionally blocked with heading `{heading}`.");
        }

        if (EpicHeadingRegex().IsMatch(heading))
        {
            return new ArtifactOutputClassification(
                ArtifactOutputKind.Promotable,
                "Epic authoring output is an epic candidate.");
        }

        if (MalformedEpicHeadingRegex().IsMatch(heading) ||
            content.Contains("## Epic Metadata", StringComparison.OrdinalIgnoreCase))
        {
            return new ArtifactOutputClassification(
                ArtifactOutputKind.Malformed,
                "Epic authoring output resembles an epic but does not use the required `# Epic:` heading.");
        }

        return new ArtifactOutputClassification(
            ArtifactOutputKind.Ambiguous,
            $"Epic authoring output heading `{heading}` is neither blocked nor an epic candidate.");
    }

    private static string? FirstTopLevelHeading(string content)
    {
        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed;
            }
        }

        return null;
    }

    [GeneratedRegex(@"^#\s+Epic\s*:\s+\S", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpicHeadingRegex();

    [GeneratedRegex(@"^#\s+Epic\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MalformedEpicHeadingRegex();

    [GeneratedRegex(@"^#\s+.*\bBlocked\b.*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BlockedHeadingRegex();
}
