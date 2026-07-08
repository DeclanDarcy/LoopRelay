using LoopRelay.Roadmap.Cli.Models.Projections;

namespace LoopRelay.Roadmap.Cli.Services.Projections;

internal sealed class ProjectionValidator
{
    private static readonly IReadOnlyDictionary<string, string> RequiredTitles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CreateRoadmapCompletionContext"] = "# Roadmap Completion Projection",
            ["UpdateRoadmapCompletionContext"] = "# Roadmap Completion Update Projection",
            ["SelectNextEpic"] = "# Select Next Epic Projection",
            ["EpicPreparationAudit"] = "# Epic Preparation Audit Projection",
            ["RealignEpic"] = "# Epic Realignment Projection",
            ["ReimagineEpic"] = "# Epic Reimagination Projection",
            ["CreateNewEpic"] = "# Create New Epic Projection",
            ["SplitEpic"] = "# Split Epic Projection",
            ["GenerateMilestoneDeepDivesForEpic"] = "# Milestone Deep Dive Projection",
            ["EvaluateEpicCompletionAndDrift"] = "# Epic Completion Evaluation Projection",
        };

    private static readonly string[] RequiredSections =
    [
        "## Purpose",
        "## Authority Boundary",
        "## Projection Metadata",
        "## Canonical Vocabulary",
        "## Downstream Use Instructions",
        "## Projection Integrity Checklist",
    ];

    private static readonly string[] ForbiddenRuntimeStateHeadings =
    [
        "## Current Roadmap Completion State",
        "## Selected Epic",
        "## Active Epic",
        "## Completed Epic History",
        "## Codebase Facts",
        "## Runtime State",
    ];

    public ProjectionValidationResult Validate(string runtimePromptName, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ProjectionValidationResult.Invalid("Projection content is empty.");
        }

        if (!RequiredTitles.TryGetValue(runtimePromptName, out string? title))
        {
            return ProjectionValidationResult.Invalid($"No expected projection title is registered for {runtimePromptName}.");
        }

        if (!content.Contains(title, StringComparison.Ordinal))
        {
            return ProjectionValidationResult.Invalid($"Projection is missing required title `{title}`.");
        }

        foreach (string section in RequiredSections)
        {
            if (!content.Contains(section, StringComparison.Ordinal))
            {
                return ProjectionValidationResult.Invalid($"Projection is missing required section `{section}`.");
            }
        }

        if (!content.Contains($"| Intended Consumer | {runtimePromptName} |", StringComparison.Ordinal))
        {
            return ProjectionValidationResult.Invalid($"Projection intended consumer does not match `{runtimePromptName}`.");
        }

        foreach (string heading in ForbiddenRuntimeStateHeadings)
        {
            if (content.Contains(heading, StringComparison.OrdinalIgnoreCase))
            {
                return ProjectionValidationResult.Invalid($"Projection contains forbidden runtime-state section `{heading}`.");
            }
        }

        return ProjectionValidationResult.Valid();
    }
}
