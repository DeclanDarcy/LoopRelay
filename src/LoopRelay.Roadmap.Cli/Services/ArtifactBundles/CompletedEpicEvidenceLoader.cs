using System.Text;
using LoopRelay.Roadmap.Cli.Models.ArtifactBundles;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.Projections;

namespace LoopRelay.Roadmap.Cli.Services.ArtifactBundles;

internal sealed class CompletedEpicEvidenceLoader(RoadmapArtifacts artifacts)
{
    private readonly RoadmapArtifacts _artifacts = artifacts;
    internal const int MaxRenderedContentPerEpic = 4_000;
    internal const int MaxTotalRenderedCharacters = 16_000;

    private static readonly string[] EvidenceSectionNames =
    [
        "Strategic Purpose",
        "Desired Capability",
        "Outcome",
        "Acceptance Criteria",
        "Completion Evidence",
        "Implementation Evidence",
        "Drift",
        "Follow-Up",
    ];

    public async Task<IReadOnlyList<CompletedEpicEvidence>> LoadAsync()
    {
        IReadOnlyList<string> paths = await _artifacts.ListAsync(RoadmapArtifactPaths.CompletedEpicsDirectory, "*.md");
        var evidence = new List<CompletedEpicEvidence>();
        foreach (string path in paths.Order(StringComparer.Ordinal))
        {
            string? content = await _artifacts.ReadAsync(path);
            if (content is null)
            {
                continue;
            }

            evidence.Add(CreateEvidence(path, content));
        }

        return evidence;
    }

    public async Task<string> RenderAsync() => Render(await LoadAsync());

    public static string Render(IReadOnlyList<CompletedEpicEvidence> completedEpics)
    {
        if (completedEpics.Count == 0)
        {
            return $"""
            # Completed Epic Evidence

            No completed epic markdown files were found under `{RoadmapArtifactPaths.CompletedEpicsPattern}`.
            """;
        }

        var builder = new StringBuilder();
        builder.AppendLine("# Completed Epic Evidence");
        builder.AppendLine();
        builder.AppendLine((string?)$"Completed epic source glob: `{RoadmapArtifactPaths.CompletedEpicsPattern}`");

        foreach (CompletedEpicEvidence epic in completedEpics)
        {
            string block = RenderBlock(epic);
            if (builder.Length + block.Length > MaxTotalRenderedCharacters)
            {
                int available = MaxTotalRenderedCharacters - builder.Length;
                const string totalTruncationNote = "\n\n_Completed epic evidence truncated because the archive exceeded the total evidence budget._\n";
                int contentBudget = Math.Max(0, available - totalTruncationNote.Length);
                if (contentBudget > 0)
                {
                    builder.Append(block[..Math.Min(block.Length, contentBudget)].TrimEnd());
                }

                builder.Append(totalTruncationNote);
                break;
            }

            builder.Append(block);
        }

        return builder.ToString().TrimEnd();
    }

    private static CompletedEpicEvidence CreateEvidence(string path, string content)
    {
        string? title = ExtractTitle(content);
        string? extractedEpicId = ExtractEpicId(content);
        string? epicId = extractedEpicId ?? Path.GetFileNameWithoutExtension(path);
        string renderedContent = ExtractRenderedContent(content);
        return new CompletedEpicEvidence(
            path,
            title,
            string.IsNullOrWhiteSpace(epicId) ? null : epicId,
            EvidenceQuality(content, title, extractedEpicId, renderedContent),
            renderedContent);
    }

    private static string RenderBlock(CompletedEpicEvidence epic)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine();
        builder.Append("## Completed Epic: ").AppendLine(EscapeHeading(epic.Title ?? Path.GetFileNameWithoutExtension(epic.Path)));
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("|---|---|");
        builder.Append("| Source Path | ").Append(EscapeCell(epic.Path)).AppendLine(" |");
        builder.Append("| Epic ID | ").Append(EscapeCell(epic.EpicId ?? "Unknown")).AppendLine(" |");
        builder.Append("| Evidence Quality | ").Append(EscapeCell(epic.EvidenceQuality)).AppendLine(" |");
        builder.AppendLine();
        builder.AppendLine(epic.RenderedContent);
        return builder.ToString();
    }

    private static string ExtractRenderedContent(string content)
    {
        string normalized = Normalize(content);
        string extracted = ExtractKnownSections(normalized);
        if (string.IsNullOrWhiteSpace(extracted))
        {
            extracted = string.IsNullOrWhiteSpace(normalized)
                ? "_No extractable content._"
                : normalized.Trim();
        }

        return Limit(extracted.Trim(), MaxRenderedContentPerEpic, "_Per-epic evidence truncated because this archived epic exceeded the per-file evidence budget._");
    }

    private static string ExtractKnownSections(string content)
    {
        string[] lines = content.Split('\n');
        var sections = new List<string>();
        for (int index = 0; index < lines.Length; index++)
        {
            if (!TryParseHeading(lines[index], out int level, out string? heading) || !IsEvidenceSection(heading))
            {
                continue;
            }

            int end = index + 1;
            while (end < lines.Length)
            {
                if (TryParseHeading(lines[end], out int nextLevel, out _) && nextLevel <= level)
                {
                    break;
                }

                end++;
            }

            sections.Add(string.Join('\n', lines[index..end]).Trim());
            index = end - 1;
        }

        return string.Join("\n\n", sections);
    }

    private static string? ExtractTitle(string content)
    {
        foreach (string line in Normalize(content).Split('\n'))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                string title = line[2..].Trim();
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        return null;
    }

    private static string? ExtractEpicId(string content)
    {
        foreach (IReadOnlyDictionary<string, string> row in MarkdownTableParser.ParseTables(content))
        {
            if (row.TryGetValue("Field", out string? field) &&
                row.TryGetValue("Value", out string? value) &&
                string.Equals(field, "Epic ID", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string EvidenceQuality(string content, string? title, string? epicId, string renderedContent)
    {
        if (ContainsAny(content, "Completion Evidence", "Implementation Evidence", "Epic Complete", "Status | Complete", "Status | Completed"))
        {
            return "Strong";
        }

        if (!string.IsNullOrWhiteSpace(title) ||
            !string.IsNullOrWhiteSpace(epicId) ||
            ContainsAny(renderedContent, "Strategic Purpose", "Desired Capability", "Acceptance Criteria", "Outcome"))
        {
            return "Weak";
        }

        return "Unclear";
    }

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static bool IsEvidenceSection(string heading) =>
        EvidenceSectionNames.Contains(heading.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool TryParseHeading(string line, out int level, out string heading)
    {
        level = 0;
        heading = string.Empty;
        string trimmed = line.TrimStart();
        while (level < trimmed.Length && trimmed[level] == '#')
        {
            level++;
        }

        if (level == 0 || level >= trimmed.Length || trimmed[level] != ' ')
        {
            return false;
        }

        heading = trimmed[(level + 1)..].Trim();
        return heading.Length > 0;
    }

    private static string Limit(string value, int maxCharacters, string truncationNote)
    {
        if (value.Length <= maxCharacters)
        {
            return value;
        }

        int contentLength = Math.Max(0, maxCharacters - truncationNote.Length - 2);
        return value[..contentLength].TrimEnd() + "\n\n" + truncationNote;
    }

    private static string Normalize(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string EscapeCell(string value) =>
        value.Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Trim();

    private static string EscapeHeading(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
}
