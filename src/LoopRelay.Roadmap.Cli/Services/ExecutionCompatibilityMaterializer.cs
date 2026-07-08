using System.Text.RegularExpressions;
using LoopRelay.Roadmap.Cli.Models;

namespace LoopRelay.Roadmap.Cli.Services;

internal sealed partial class ExecutionCompatibilityMaterializer(
    RoadmapArtifacts artifacts,
    ExecutionPreparationProvenanceService provenanceService)
{
    public async Task MaterializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string operationalContext = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.OperationalContext);
        string executionPrompt = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ExecutionPrompt);
        string activeEpic = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        IReadOnlyList<string> specs = await provenanceService.RequireFreshMilestoneSpecPathsAsync(cancellationToken);
        if (specs.Count == 0)
        {
            throw new RoadmapStepException("Cannot materialize execution compatibility artifacts without specs.");
        }

        var plan = new List<string>
        {
            "# Execution Plan",
            string.Empty,
            "## Source Roadmap Epic",
            string.Empty,
            activeEpic,
            string.Empty,
            "## Execution Prompt",
            string.Empty,
            executionPrompt,
            string.Empty,
            "## Operational Context",
            string.Empty,
            operationalContext,
            string.Empty,
            "## Milestones",
            string.Empty,
        };

        int index = 1;
        var milestonePaths = new List<string>();
        foreach (string spec in specs.Order(StringComparer.Ordinal))
        {
            string specContent = await artifacts.ReadRequiredAsync(spec);
            IReadOnlyList<string> checklist = DeriveChecklist(specContent);
            if (checklist.Count == 0)
            {
                throw new RoadmapStepException($"No auditable checklist could be derived from {spec}.");
            }

            string milestonePath = $"{RoadmapArtifactPaths.ExecutionMilestonesDirectory}/m{index:000}.md";
            milestonePaths.Add(milestonePath);
            plan.Add($"- [ ] {milestonePath} from {spec}");

            var milestone = new List<string>
            {
                $"# Milestone {index:000}",
                string.Empty,
                "## Checklist",
                string.Empty,
            };
            milestone.AddRange(checklist.Select(item => item.StartsWith("- [", StringComparison.Ordinal) ? item : "- [ ] " + item.TrimStart('-', ' ')));
            milestone.AddRange(
            [
                string.Empty,
                "## Source Spec",
                string.Empty,
                specContent,
            ]);

            await artifacts.WriteAsync(milestonePath, string.Join(Environment.NewLine, milestone) + Environment.NewLine);
            index++;
        }

        await artifacts.WriteAsync(RoadmapArtifactPaths.ExecutionPlan, string.Join(Environment.NewLine, plan) + Environment.NewLine);
        await provenanceService.RecordCompatibilityArtifactsAsync(milestonePaths, cancellationToken);
    }

    private static IReadOnlyList<string> DeriveChecklist(string specContent)
    {
        string[] checkboxLines = specContent.Split('\n')
            .Select(line => line.Trim())
            .Where(line => CheckboxRegex().IsMatch(line))
            .ToArray();
        if (checkboxLines.Length > 0)
        {
            return checkboxLines;
        }

        int acceptanceIndex = specContent.IndexOf("Acceptance Criteria", StringComparison.OrdinalIgnoreCase);
        if (acceptanceIndex < 0)
        {
            return [];
        }

        string section = specContent[acceptanceIndex..];
        return section.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim())
            .Where(line => line.Length > 0)
            .ToList();
    }

    [GeneratedRegex(@"^- \[[ xX]\]\s+.+$", RegexOptions.CultureInvariant)]
    private static partial Regex CheckboxRegex();
}
