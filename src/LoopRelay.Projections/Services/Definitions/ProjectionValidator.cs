using LoopRelay.Projections.Models.Definitions;

namespace LoopRelay.Projections.Services.Definitions;

public sealed class ProjectionValidator(ProjectionDefinitionRegistry _registry)
{
    private static readonly string[] RequiredSections =
    [
        "## Purpose",
        "## Authority Boundary",
        "## Projection Metadata",
        "## Canonical Vocabulary",
        "## Downstream Use Instructions",
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

        ProjectionDefinition definition = _registry.Get(runtimePromptName);
        if (!content.Contains(definition.RequiredTitle, StringComparison.Ordinal))
        {
            return ProjectionValidationResult.Invalid($"Projection is missing required title `{definition.RequiredTitle}`.");
        }

        foreach (string section in RequiredSections)
        {
            if (!content.Contains(section, StringComparison.Ordinal))
            {
                return ProjectionValidationResult.Invalid($"Projection is missing required section `{section}`.");
            }
        }

        if (!ContainsMetadataValue(content, "Intended Consumer", definition.IntendedConsumer))
        {
            return ProjectionValidationResult.Invalid($"Projection intended consumer does not match `{definition.IntendedConsumer}`.");
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

    private static bool ContainsMetadataValue(string content, string field, string expectedValue)
    {
        foreach (string line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] cells = line.Split('|', StringSplitOptions.TrimEntries);
            if (cells.Length < 4)
            {
                continue;
            }

            string actualField = cells[1].Trim().Trim('`');
            string actualValue = cells[2].Trim().Trim('`');
            if (string.Equals(actualField, field, StringComparison.Ordinal)
                && string.Equals(actualValue, expectedValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
