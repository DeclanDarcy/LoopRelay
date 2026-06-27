namespace CommandCenter.Core.Prompts;

/// <summary>
/// The Codex session role a rendered prompt was issued under. Mirrors
/// <c>CommandCenter.Agents.Models.SessionRole</c> (same members, same order) but is redeclared
/// here because Core is the base layer and the prompt authority — it must not reference the
/// Agents runtime, which sits above it. A higher layer that already references both can map
/// between the two enums; Core never does.
/// </summary>
public enum PromptSessionRole
{
    Planning,
    OperationalExecution,
    Decision,
    Transfer,
    ContextUpdate,
}

/// <summary>
/// Per-turn provenance for a prompt rendered from the <c>CommandCenter.Core.Prompts</c> catalog.
/// Per the refactor-plan ("every agent turn records prompt name, generated type, SourceHash,
/// session role, workflow phase, rendered input artifact identities, and produced artifact
/// identities"), this captures which canonical prompt produced a turn and the artifact identities
/// it consumed and was directed to produce, so any turn is auditable back to the catalog.
/// </summary>
/// <remarks>
/// Artifacts are identified by repository-relative path — the only stable identity on
/// <c>LoadedArtifact</c>/<c>Artifact</c> (neither carries an id or content hash today).
/// </remarks>
public sealed record PromptProvenance
{
    /// <summary>The canonical prompt's name, e.g. <c>nameof(StartExecution)</c>.</summary>
    public required string PromptName { get; init; }

    /// <summary>The generated catalog type's full name, e.g. <c>CommandCenter.Core.Prompts.StartExecution</c>.</summary>
    public required string PromptType { get; init; }

    /// <summary>The catalog type's build-time content hash (<c>SourceHash</c>), pinning the exact template text.</summary>
    public required string SourceHash { get; init; }

    /// <summary>The Codex session role the turn ran under.</summary>
    public required PromptSessionRole SessionRole { get; init; }

    /// <summary>The workflow phase within the role's lifecycle (e.g. an operational <c>Start</c> vs <c>Continue</c> turn).</summary>
    public required string WorkflowPhase { get; init; }

    /// <summary>Repository-relative identities of the artifacts rendered into the prompt.</summary>
    public IReadOnlyList<string> InputArtifactIdentities { get; init; } = Array.Empty<string>();

    /// <summary>Repository-relative identities of the artifacts the turn is directed to produce.</summary>
    public IReadOnlyList<string> OutputArtifactIdentities { get; init; } = Array.Empty<string>();
}
