using System.Security.Cryptography;
using System.Text;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Services;

public sealed record EvalRoadmapMilestonePromptContextResult(
    bool IsUsable,
    IReadOnlyList<PromptContextSection> Sections,
    IReadOnlyDictionary<string, string> Metadata,
    string Explanation,
    IReadOnlyList<string> Evidence);

public static class EvalRoadmapMilestonePromptContext
{
    public static WorkflowTransitionIdentity Transition { get; } =
        new("GenerateMilestoneDeepDivesForEpic");

    public const string SectionTitle = "Active Epic";

    public static EvalRoadmapMilestonePromptContextResult Build(string repositoryPath)
    {
        string root = Path.GetFullPath(repositoryPath);
        string relativePath = EvaluationArtifactPaths.PreparedEpic;
        string absolutePath = Path.Combine(root, Normalize(relativePath));
        if (!File.Exists(absolutePath))
        {
            return Unavailable(
                "Active Epic prompt context is missing. Expected `.agents/epic.md` before generating milestone deep dives.",
                [relativePath]);
        }

        string content = File.ReadAllText(absolutePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return Unavailable(
                "Active Epic prompt context is empty. `.agents/epic.md` must contain a selected epic before generating milestone deep dives.",
                [relativePath]);
        }

        IReadOnlyList<string> validationIssues = ValidateActiveEpic(content);
        if (validationIssues.Count > 0)
        {
            return Unavailable(
                "Active Epic prompt context is malformed or ambiguous: " + string.Join("; ", validationIssues),
                [relativePath]);
        }

        string trimmed = content.Trim();
        string hash = Hash(trimmed);
        var section = new PromptContextSection(
            SectionTitle,
            trimmed,
            relativePath,
            [relativePath]);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["context.active_epic.path"] = relativePath,
            ["context.active_epic.section"] = SectionTitle,
            ["context.active_epic.hash"] = hash,
            ["context.active_epic.status"] = "valid",
        };
        return new EvalRoadmapMilestonePromptContextResult(
            IsUsable: true,
            Sections: [section],
            Metadata: metadata,
            Explanation: "Active Epic prompt context loaded from `.agents/epic.md`.",
            Evidence: [relativePath]);
    }

    private static IReadOnlyList<string> ValidateActiveEpic(string content)
    {
        string[] lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var issues = new List<string>();
        int epicHeadings = lines.Count(line => line.TrimStart().StartsWith("# Epic:", StringComparison.Ordinal));
        if (epicHeadings == 0)
        {
            issues.Add("missing top-level `# Epic:` heading");
        }
        else if (epicHeadings > 1)
        {
            issues.Add("multiple top-level `# Epic:` headings");
        }

        RequireHeading(lines, "## Epic Metadata", issues);
        if (!HasHeading(lines, "## Strategic Purpose") && !HasHeading(lines, "## Strategic Continuity"))
        {
            issues.Add("missing `## Strategic Purpose` or `## Strategic Continuity` section");
        }

        RequireHeading(lines, "## Desired Capability", issues);
        RequireHeading(lines, "## Acceptance Criteria", issues);
        RequireHeading(lines, "## Milestone Roadmap", issues);
        if (!HasMilestoneRoadmapTable(lines))
        {
            issues.Add("missing required milestone roadmap table header");
        }

        return issues;
    }

    private static void RequireHeading(string[] lines, string heading, List<string> issues)
    {
        if (!HasHeading(lines, heading))
        {
            issues.Add($"missing `{heading}` section");
        }
    }

    private static bool HasHeading(string[] lines, string heading) =>
        lines.Any(line => string.Equals(line.Trim(), heading, StringComparison.Ordinal));

    private static bool HasMilestoneRoadmapTable(string[] lines)
    {
        const string requiredHeader = "|MilestoneID|MilestoneName|Purpose|Outcome|DependsOn|CompletionSignal|";
        return lines
            .Select(NormalizeTableLine)
            .Any(line => string.Equals(line, requiredHeader, StringComparison.Ordinal));
    }

    private static string NormalizeTableLine(string line) =>
        line.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();

    private static EvalRoadmapMilestonePromptContextResult Unavailable(
        string explanation,
        IReadOnlyList<string> evidence) =>
        new(
            IsUsable: false,
            Sections: [],
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["context.active_epic.path"] = EvaluationArtifactPaths.PreparedEpic,
                ["context.active_epic.status"] = "unavailable",
            },
            Explanation: explanation,
            Evidence: evidence);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Normalize(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
}
