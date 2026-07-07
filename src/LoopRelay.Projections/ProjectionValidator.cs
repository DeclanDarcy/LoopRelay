namespace LoopRelay.Projections;

public sealed class ProjectionValidator(ProjectionDefinitionRegistry registry)
{
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

        ProjectionDefinition definition = registry.Get(runtimePromptName);
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

        if (!content.Contains($"| Intended Consumer | {definition.IntendedConsumer} |", StringComparison.Ordinal))
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
}

public sealed record ProjectionValidationResult(bool IsValid, string? Error)
{
    public static ProjectionValidationResult Valid() => new(true, null);

    public static ProjectionValidationResult Invalid(string error) => new(false, error);
}
