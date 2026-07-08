using LoopRelay.Roadmap.Cli.Models;
using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Services;

internal static class ProjectionFreshnessEvaluator
{
    public static ProjectionFreshness Evaluate(ProjectionProvenance current, ProjectionManifestEntry? persisted)
    {
        if (persisted is null)
        {
            return ProjectionFreshness.Unknown(ProjectionStaleReason.MissingManifest);
        }

        if (persisted.ProvenanceStatus != ProjectionProvenanceStatus.Trusted)
        {
            return ProjectionFreshness.Unknown(ProjectionStaleReason.UnknownProvenance);
        }

        var reasons = new List<ProjectionStaleReason>();
        if (!string.Equals(persisted.EffectiveProjectionIdentity, current.ProjectionIdentity, StringComparison.Ordinal))
        {
            reasons.Add(ProjectionStaleReason.ProjectionIdentityDrift);
        }

        if (!string.Equals(persisted.ProjectionPromptName, current.Prompt.PromptName, StringComparison.Ordinal))
        {
            reasons.Add(ProjectionStaleReason.PromptIdentityDrift);
        }

        if (!string.Equals(persisted.ProjectionPromptSourceHash, current.Prompt.SourceHash, StringComparison.Ordinal))
        {
            reasons.Add(ProjectionStaleReason.PromptTemplateDrift);
        }

        if (!string.Equals(persisted.ProjectContextHash, current.ProjectContextHash, StringComparison.Ordinal))
        {
            reasons.Add(ProjectionStaleReason.ProjectContextDrift);
        }

        AddCausalInputReasons(current, persisted, reasons);

        if (reasons.Count == 0)
        {
            return ProjectionFreshness.Fresh;
        }

        return reasons.Contains(ProjectionStaleReason.UnknownProvenance)
            ? ProjectionFreshness.Unknown(reasons.ToArray())
            : ProjectionFreshness.Stale(reasons.ToArray());
    }

    private static void AddCausalInputReasons(
        ProjectionProvenance current,
        ProjectionManifestEntry persisted,
        List<ProjectionStaleReason> reasons)
    {
        IReadOnlyList<ProjectionCausalInput> persistedInputs = persisted.EffectiveCausalInputs;
        if (persistedInputs.Count == 0)
        {
            reasons.Add(ProjectionStaleReason.UnknownProvenance);
            return;
        }

        Dictionary<string, ProjectionCausalInput> previous = persistedInputs.ToDictionary(InputKey, StringComparer.Ordinal);
        Dictionary<string, ProjectionCausalInput> next = current.CausalInputs.ToDictionary(InputKey, StringComparer.Ordinal);

        foreach (ProjectionCausalInput currentInput in current.CausalInputs)
        {
            if (!previous.TryGetValue(InputKey(currentInput), out ProjectionCausalInput? persistedInput))
            {
                reasons.Add(ReasonForMissingInput(currentInput));
                continue;
            }

            if (!string.Equals(persistedInput.Version, currentInput.Version, StringComparison.Ordinal))
            {
                reasons.Add(ReasonForVersionChange(currentInput));
            }
        }

        foreach (ProjectionCausalInput persistedInput in persistedInputs)
        {
            if (!next.ContainsKey(InputKey(persistedInput)))
            {
                reasons.Add(ReasonForMissingInput(persistedInput));
            }
        }
    }

    private static ProjectionStaleReason ReasonForVersionChange(ProjectionCausalInput input) =>
        input.Kind switch
        {
            ProjectionProvenance.ProjectContextInputKind => ProjectionStaleReason.ProjectContextDrift,
            ProjectionProvenance.ProjectionPromptTemplateInputKind => ProjectionStaleReason.PromptTemplateDrift,
            _ => ProjectionStaleReason.CausalInputDrift,
        };

    private static ProjectionStaleReason ReasonForMissingInput(ProjectionCausalInput input) =>
        input.Kind switch
        {
            ProjectionProvenance.ProjectContextInputKind => ProjectionStaleReason.ProjectContextDrift,
            ProjectionProvenance.ProjectionPromptTemplateInputKind => ProjectionStaleReason.PromptIdentityDrift,
            _ => ProjectionStaleReason.CausalInputDrift,
        };

    private static string InputKey(ProjectionCausalInput input) => $"{input.Kind}:{input.Identity}";
}
