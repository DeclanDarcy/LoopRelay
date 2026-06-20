using System.Security.Cryptography;
using System.Text;

namespace CommandCenter.Backend.Continuity;

public sealed class MarkdownOperationalContextParser : IOperationalContextParser
{
    private static readonly IReadOnlyDictionary<string, SectionDefinition> KnownSections =
        new Dictionary<string, SectionDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Current Mental Model"] = new("Current Mental Model", OperationalContextItemKind.MentalModel),
            ["Architecture"] = new("Architecture", OperationalContextItemKind.Architecture),
            ["Authority Boundaries"] = new("Authority Boundaries", OperationalContextItemKind.AuthorityBoundary),
            ["Constraints"] = new("Constraints", OperationalContextItemKind.Constraint),
            ["Stable Decisions"] = new("Stable Decisions", OperationalContextItemKind.StableDecision),
            ["Decision Rationale"] = new("Decision Rationale", OperationalContextItemKind.DecisionRationale),
            ["Open Questions"] = new("Open Questions", OperationalContextItemKind.OpenQuestion),
            ["Active Risks"] = new("Active Risks", OperationalContextItemKind.ActiveRisk),
            ["Recent Understanding Changes"] = new("Recent Understanding Changes", OperationalContextItemKind.RecentChange)
        };

    public OperationalContextDocument Parse(string markdown)
    {
        var title = "Operational Context";
        var sections = new List<ParsedSection>();
        ParsedSection? currentSection = null;

        foreach (var rawLine in SplitLines(markdown))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                title = line[2..].Trim();
                currentSection = null;
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                currentSection = new ParsedSection(line[3..].Trim());
                sections.Add(currentSection);
                continue;
            }

            currentSection?.Lines.Add(line);
        }

        return new OperationalContextDocument
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Operational Context" : title,
            CurrentMentalModel = GetItems(sections, "Current Mental Model"),
            Architecture = GetItems(sections, "Architecture"),
            AuthorityBoundaries = GetItems(sections, "Authority Boundaries"),
            Constraints = GetItems(sections, "Constraints"),
            StableDecisions = GetItems(sections, "Stable Decisions"),
            DecisionRationale = GetItems(sections, "Decision Rationale"),
            OpenQuestions = GetItems(sections, "Open Questions"),
            ActiveRisks = GetItems(sections, "Active Risks"),
            RecentUnderstandingChanges = GetItems(sections, "Recent Understanding Changes"),
            AdditionalSections = sections
                .Where(section => !KnownSections.ContainsKey(section.Heading))
                .Select(section => new OperationalContextSection
                {
                    Heading = section.Heading,
                    Content = string.Join(Environment.NewLine, TrimOuterBlankLines(section.Lines)),
                    Items = ExtractItems(section, OperationalContextItemKind.Unknown)
                })
                .ToArray()
        };
    }

    public string Render(OperationalContextDocument document)
    {
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(string.IsNullOrWhiteSpace(document.Title) ? "Operational Context" : document.Title.Trim());
        AppendSection(builder, "Current Mental Model", document.CurrentMentalModel);
        AppendSection(builder, "Architecture", document.Architecture);
        AppendSection(builder, "Authority Boundaries", document.AuthorityBoundaries);
        AppendSection(builder, "Constraints", document.Constraints);
        AppendSection(builder, "Stable Decisions", document.StableDecisions);
        AppendSection(builder, "Decision Rationale", document.DecisionRationale);
        AppendSection(builder, "Open Questions", document.OpenQuestions);
        AppendSection(builder, "Active Risks", document.ActiveRisks);
        AppendSection(builder, "Recent Understanding Changes", document.RecentUnderstandingChanges);

        foreach (var section in document.AdditionalSections)
        {
            if (string.IsNullOrWhiteSpace(section.Heading))
            {
                continue;
            }

            builder.AppendLine();
            builder.Append("## ").AppendLine(section.Heading.Trim());
            if (!string.IsNullOrWhiteSpace(section.Content))
            {
                builder.AppendLine();
                builder.AppendLine(section.Content.Trim());
            }
            else if (section.Items.Count > 0)
            {
                builder.AppendLine();
                foreach (var item in section.Items)
                {
                    builder.Append("- ").AppendLine(item.Text.Trim());
                }
            }
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static IReadOnlyList<OperationalContextItem> GetItems(
        IReadOnlyList<ParsedSection> sections,
        string heading)
    {
        if (!KnownSections.TryGetValue(heading, out var definition))
        {
            return [];
        }

        return sections
            .Where(section => string.Equals(section.Heading, definition.Heading, StringComparison.OrdinalIgnoreCase))
            .SelectMany(section => ExtractItems(section, definition.Kind))
            .ToArray();
    }

    private static IReadOnlyList<OperationalContextItem> ExtractItems(
        ParsedSection section,
        OperationalContextItemKind kind)
    {
        return section.Lines
            .Select(TryExtractListItem)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!)
            .Select(text => new OperationalContextItem
            {
                Id = CreateItemId(section.Heading, text),
                Kind = kind,
                Text = text
            })
            .ToArray();
    }

    private static string? TryExtractListItem(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3)
        {
            return null;
        }

        if ((trimmed[0] == '-' || trimmed[0] == '*' || trimmed[0] == '+') && char.IsWhiteSpace(trimmed[1]))
        {
            return trimmed[2..].Trim();
        }

        return null;
    }

    private static void AppendSection(
        StringBuilder builder,
        string heading,
        IReadOnlyList<OperationalContextItem> items)
    {
        builder.AppendLine();
        builder.Append("## ").AppendLine(heading);
        if (items.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.Text))
            {
                builder.Append("- ").AppendLine(item.Text.Trim());
            }
        }
    }

    private static string CreateItemId(string heading, string text)
    {
        var normalized = NormalizeForIdentity($"{heading}:{text}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"{NormalizeForIdentity(heading).Replace(' ', '-')}-{Convert.ToHexString(bytes)[..12].ToLowerInvariant()}";
    }

    private static string NormalizeForIdentity(string value)
    {
        return string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static IEnumerable<string> SplitLines(string markdown)
    {
        return markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    private static IReadOnlyList<string> TrimOuterBlankLines(IReadOnlyList<string> lines)
    {
        var start = 0;
        var end = lines.Count - 1;
        while (start <= end && string.IsNullOrWhiteSpace(lines[start]))
        {
            start++;
        }

        while (end >= start && string.IsNullOrWhiteSpace(lines[end]))
        {
            end--;
        }

        return start > end ? [] : lines.Skip(start).Take(end - start + 1).ToArray();
    }

    private sealed record SectionDefinition(string Heading, OperationalContextItemKind Kind);

    private sealed class ParsedSection(string heading)
    {
        public string Heading { get; } = heading;

        public List<string> Lines { get; } = [];
    }
}
