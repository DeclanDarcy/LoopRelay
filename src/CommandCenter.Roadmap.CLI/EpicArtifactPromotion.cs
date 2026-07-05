using System.Text.RegularExpressions;

namespace CommandCenter.Roadmap.Cli;

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

internal sealed partial class EpicArtifactValidator : IArtifactValidator
{
    private static readonly string[] RequiredSections =
    [
        "## Epic Metadata",
        "## Desired Capability",
        "## Acceptance Criteria",
    ];

    public ArtifactValidationResult Validate(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ArtifactValidationResult.Invalid("Active epic content is empty.");
        }

        ArtifactOutputClassification classification = new EpicAuthoringOutputClassifier().Classify(content);
        if (classification.Kind != ArtifactOutputKind.Promotable)
        {
            return ArtifactValidationResult.Invalid(classification.Reason);
        }

        foreach (string section in RequiredSections)
        {
            if (!ContainsHeading(content, section))
            {
                return ArtifactValidationResult.Invalid($"Active epic is missing required section `{section}`.");
            }
        }

        if (!ContainsHeading(content, "## Strategic Purpose") &&
            !ContainsHeading(content, "## Strategic Continuity"))
        {
            return ArtifactValidationResult.Invalid("Active epic is missing a strategic purpose or continuity section.");
        }

        IReadOnlyDictionary<string, string> metadata;
        try
        {
            metadata = MarkdownTableParser.ParseFieldTable(content, "## Epic Metadata");
        }
        catch (MarkdownParseException exception)
        {
            return ArtifactValidationResult.Invalid(exception.Message);
        }

        foreach (string field in new[] { "Epic ID", "Status" })
        {
            if (!metadata.TryGetValue(field, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                return ArtifactValidationResult.Invalid($"Active epic metadata is missing `{field}`.");
            }
        }

        return ArtifactValidationResult.Valid();
    }

    private static bool ContainsHeading(string content, string heading) =>
        content.Split('\n')
            .Select(line => line.Trim())
            .Any(line => string.Equals(line, heading, StringComparison.OrdinalIgnoreCase));
}
