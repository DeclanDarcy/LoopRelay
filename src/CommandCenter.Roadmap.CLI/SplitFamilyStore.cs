using System.Globalization;

namespace CommandCenter.Roadmap.Cli;

internal sealed class SplitFamilyStore(RoadmapArtifacts artifacts)
{
    public async Task<string> WriteAsync(SplitFamily family)
    {
        string markdownPath = RoadmapArtifactPaths.SplitFamily(family.FamilyId);
        await StructuredStore(RoadmapArtifactPaths.SplitFamilyJson(family.FamilyId))
            .SaveAsync(SplitFamilyPersistenceDocument.FromDomain(family));
        await artifacts.WriteAsync(markdownPath, Render(family));
        return markdownPath;
    }

    public async Task<bool> ExistsForChildAsync(string childEpicPath)
    {
        IReadOnlyList<string> structuredFamilies = await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.json");
        foreach (string path in structuredFamilies.Order(StringComparer.Ordinal))
        {
            SplitFamilyPersistenceDocument? document = await StructuredStore(path).LoadAsync();
            if (document?.Family.ChildEpicPaths.Contains(childEpicPath, StringComparer.Ordinal) == true)
            {
                return true;
            }
        }

        IReadOnlyList<string> legacyFamilies = await artifacts.ListAsync(RoadmapArtifactPaths.SplitFamiliesDirectory, "split-family-*.md");
        foreach (string path in legacyFamilies.Order(StringComparer.Ordinal))
        {
            string familyId = FamilyIdFromPath(path);
            if (await artifacts.ExistsAsync(RoadmapArtifactPaths.SplitFamilyJson(familyId)))
            {
                continue;
            }

            string? content = await artifacts.ReadAsync(path);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            SplitFamily family;
            try
            {
                family = ParseLegacyMarkdown(path, content);
            }
            catch (MarkdownParseException exception)
            {
                throw new RoadmapStepException($"Legacy split family `{path}` cannot be migrated: {exception.Message}");
            }

            await WriteAsync(family);
            if (family.ChildEpicPaths.Contains(childEpicPath, StringComparer.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static string Render(SplitFamily family)
    {
        var lines = new List<string>
        {
            "# Split Family",
            string.Empty,
            "| Field | Value |",
            "|---|---|",
            $"| Family ID | {EscapeCell(family.FamilyId)} |",
            $"| Created At | {family.CreatedAt:O} |",
            $"| Selected Child | {EscapeCell(family.SelectedChildPath)} |",
            $"| Selected Child Rationale | {EscapeCell(family.SelectedChildRationale)} |",
            string.Empty,
            "## Proposal",
            string.Empty,
            family.Proposal,
            string.Empty,
            "## Child Epics",
            string.Empty,
        };

        foreach (string child in family.ChildEpicPaths)
        {
            lines.Add($"- {child}");
        }

        lines.AddRange(
        [
            string.Empty,
            "## Dependency Order",
            string.Empty,
        ]);

        foreach (string child in family.DependencyOrder)
        {
            lines.Add($"- {child}");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private SplitFamily ParseLegacyMarkdown(string path, string content)
    {
        MarkdownTableParser.ValidateTables(content);
        IReadOnlyDictionary<string, string> fields = MarkdownTableParser.ParseFieldTableStrict(content, "# Split Family");
        string familyId = Field(fields, "Family ID", FamilyIdFromPath(path));
        var family = new SplitFamily(
            familyId,
            ExtractSectionBody(content, "## Proposal"),
            ParseBulletList(content, "## Child Epics"),
            ParseBulletList(content, "## Dependency Order"),
            Field(fields, "Selected Child", string.Empty),
            Field(fields, "Selected Child Rationale", string.Empty),
            ParseTimestamp(Field(fields, "Created At", string.Empty)) ?? DateTimeOffset.MinValue);

        SplitFamilyPersistenceDocument migrated = SplitFamilyPersistenceDocument.FromDomain(family);
        IReadOnlyList<string> errors = SplitFamilyPersistenceDocument.Validate(migrated);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException($"Legacy split family `{path}` cannot be migrated because validation failed: {string.Join("; ", errors)}");
        }

        return family;
    }

    private StructuredDocumentStore<SplitFamilyPersistenceDocument> StructuredStore(string path) =>
        new(
            artifacts,
            path,
            SplitFamilyPersistenceDocument.CurrentSchemaVersion,
            document => document.SchemaVersion,
            SplitFamilyPersistenceDocument.Validate);

    private static string Field(IReadOnlyDictionary<string, string> row, string field, string fallback) =>
        row.TryGetValue(field, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private static IReadOnlyList<string> ParseBulletList(string content, string heading)
    {
        string section = MarkdownTableParser.TryExtractSection(content, heading) ?? string.Empty;
        return section.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim())
            .Where(line => line.Length > 0)
            .ToArray();
    }

    private static string ExtractSectionBody(string content, string heading)
    {
        string section = MarkdownTableParser.TryExtractSection(content, heading) ?? string.Empty;
        string[] lines = section.Split('\n');
        return string.Join(
            '\n',
            lines
                .Skip(1)
                .Select(line => line.TrimEnd('\r'))
                .SkipWhile(line => string.IsNullOrWhiteSpace(line))
                .Reverse()
                .SkipWhile(line => string.IsNullOrWhiteSpace(line))
                .Reverse());
    }

    private static DateTimeOffset? ParseTimestamp(string value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTimeOffset parsed)
            ? parsed
            : null;

    private static string FamilyIdFromPath(string path)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        const string prefix = "split-family-";
        return fileName.StartsWith(prefix, StringComparison.Ordinal)
            ? fileName[prefix.Length..]
            : fileName;
    }

    private static string EscapeCell(string? value) =>
        (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r\n", "<br>", StringComparison.Ordinal)
            .Replace("\n", "<br>", StringComparison.Ordinal)
            .Replace("\r", "<br>", StringComparison.Ordinal);
}
