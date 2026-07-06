namespace LoopRelay.Roadmap.Cli;

internal sealed record ProjectionPromptMetadata(
    string PromptName,
    string PromptType,
    string SourceHash);

internal sealed record ProjectionCausalInput(
    string Kind,
    string Identity,
    string Version);

internal sealed record ProjectionProvenance(
    string ProjectionIdentity,
    string RuntimePromptName,
    string ProjectionPath,
    ProjectionPromptMetadata Prompt,
    IReadOnlyList<string> ProjectContextFiles,
    string ProjectContextHash,
    IReadOnlyList<ProjectionCausalInput> CausalInputs)
{
    public const string ProjectContextInputKind = "ProjectContext";
    public const string ProjectionPromptTemplateInputKind = "ProjectionPromptTemplate";

    public static ProjectionProvenance Create(
        ProjectionDefinition definition,
        ProjectionPromptMetadata prompt,
        ProjectContext projectContext) =>
        new(
            definition.RuntimePromptName,
            definition.RuntimePromptName,
            definition.ProjectionPath,
            prompt,
            projectContext.SourceFiles,
            projectContext.Hash,
            [
                new ProjectionCausalInput(ProjectContextInputKind, "ProjectContext", projectContext.Hash),
                new ProjectionCausalInput(ProjectionPromptTemplateInputKind, prompt.PromptName, prompt.SourceHash),
            ]);
}

internal sealed class ProjectionProvenanceFactory(ProjectionRegistry registry)
{
    public ProjectionProvenance Create(string runtimePromptName, ProjectContext projectContext) =>
        Create(registry.Get(runtimePromptName), projectContext);

    public ProjectionProvenance Create(ProjectionDefinition definition, ProjectContext projectContext) =>
        ProjectionProvenance.Create(
            definition,
            RoadmapPromptCatalog.GetProjectionMetadata(definition.ProjectionPromptName),
            projectContext);
}

internal sealed record ProjectionFreshness(
    ProjectionStaleStatus Status,
    IReadOnlyList<ProjectionStaleReason> Reasons)
{
    public bool IsFresh => Status == ProjectionStaleStatus.Fresh;

    public static ProjectionFreshness Fresh { get; } = new(ProjectionStaleStatus.Fresh, []);

    public static ProjectionFreshness Stale(params ProjectionStaleReason[] reasons) =>
        new(ProjectionStaleStatus.Stale, NormalizeReasons(reasons));

    public static ProjectionFreshness Unknown(params ProjectionStaleReason[] reasons) =>
        new(ProjectionStaleStatus.UnknownProvenance, NormalizeReasons(reasons));

    private static IReadOnlyList<ProjectionStaleReason> NormalizeReasons(IReadOnlyList<ProjectionStaleReason> reasons) =>
        reasons.Count == 0
            ? [ProjectionStaleReason.UnknownProvenance]
            : reasons.Distinct().OrderBy(reason => reason.ToString(), StringComparer.Ordinal).ToArray();
}

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

internal enum ProjectionProvenanceStatus
{
    Unknown,
    Trusted,
}

internal enum ProjectionStaleReason
{
    MissingManifest,
    UnknownProvenance,
    ProjectionIdentityDrift,
    PromptIdentityDrift,
    PromptTemplateDrift,
    ProjectContextDrift,
    CausalInputDrift,
}
