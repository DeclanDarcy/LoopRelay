using System.Text.RegularExpressions;

namespace LoopRelay.Roadmap.Cli;

internal sealed partial class EpicArtifactValidator : IArtifactValidator
{
    private static readonly string[] RequiredSections =
    [
        "## Epic Metadata",
        "## Desired Capability",
        "## Acceptance Criteria",
        "## Milestone Roadmap",
    ];

    private static readonly string[] RequiredMilestoneColumns =
    [
        "Milestone ID",
        "Milestone Name",
        "Purpose",
        "Outcome",
        "Depends On",
        "Completion Signal",
    ];

    private static readonly string[] RequiredNonEmptyMilestoneColumns =
    [
        "Milestone ID",
        "Milestone Name",
        "Purpose",
        "Outcome",
        "Completion Signal",
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

        return ValidateMilestoneRoadmap(content);
    }

    private static bool ContainsHeading(string content, string heading) =>
        content.Split('\n')
            .Select(line => line.Trim())
            .Any(line => string.Equals(line, heading, StringComparison.OrdinalIgnoreCase));

    private static ArtifactValidationResult ValidateMilestoneRoadmap(string content)
    {
        string? milestoneRoadmap = TryExtractHeadingSection(content, "## Milestone Roadmap");
        if (milestoneRoadmap is null)
        {
            return ArtifactValidationResult.Invalid("Active epic is missing required section `## Milestone Roadmap`.");
        }

        IReadOnlyList<IReadOnlyDictionary<string, string>> rows;
        try
        {
            rows = MarkdownTableParser.ParseTablesStrict(milestoneRoadmap);
        }
        catch (MarkdownParseException exception)
        {
            return ArtifactValidationResult.Invalid(exception.Message);
        }

        IReadOnlyList<IReadOnlyDictionary<string, string>> milestoneRows = rows
            .Where(row => RequiredMilestoneColumns.All(row.ContainsKey))
            .ToArray();
        if (milestoneRows.Count == 0)
        {
            return ArtifactValidationResult.Invalid("Active epic milestone roadmap must include at least one milestone row with the required milestone columns.");
        }

        foreach (IReadOnlyDictionary<string, string> row in milestoneRows)
        {
            foreach (string column in RequiredNonEmptyMilestoneColumns)
            {
                if (!row.TryGetValue(column, out string? value) || string.IsNullOrWhiteSpace(value))
                {
                    return ArtifactValidationResult.Invalid($"Active epic milestone roadmap row is missing `{column}`.");
                }
            }
        }

        return ArtifactValidationResult.Valid();
    }

    private static string? TryExtractHeadingSection(string content, string heading)
    {
        string[] lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int start = -1;
        for (int index = 0; index < lines.Length; index++)
        {
            if (string.Equals(lines[index].Trim(), heading, StringComparison.OrdinalIgnoreCase))
            {
                start = index;
                break;
            }
        }

        if (start < 0)
        {
            return null;
        }

        int end = lines.Length;
        for (int index = start + 1; index < lines.Length; index++)
        {
            if (lines[index].TrimStart().StartsWith("## ", StringComparison.Ordinal))
            {
                end = index;
                break;
            }
        }

        return string.Join("\n", lines.Skip(start).Take(end - start));
    }
}
