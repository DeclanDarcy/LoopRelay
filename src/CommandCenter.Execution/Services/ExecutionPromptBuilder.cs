using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Decisions.Models;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Services;

/// <summary>
/// Renders the operational execution prompt from the <c>CommandCenter.Core.Prompts</c> catalog —
/// the single prompt authority. This is the catalog's first runtime consumer.
/// <para>
/// Plan/Milestone/OperationalContext artifact contents compose the <c>{plan}</c> hole; the current
/// handoff fills <c>{handoff}</c>; and the governed <see cref="ExecutionDecisionProjection"/> is
/// rendered into <c>{decisions}</c> on continuation. The previous literal/<see cref="System.Text.StringBuilder"/>
/// chrome (repository headers, instruction block, snapshot, diagnostics, and the inline projection
/// section) was deleted — structured diagnostics survive only in <see cref="ExecutionPromptMetadata"/>
/// and the persisted manifest. The raw <c>decisions.md</c> artifact no longer feeds the prompt; the
/// structured projection is the governance the agent receives.
/// </para>
/// </summary>
public sealed class ExecutionPromptBuilder : IExecutionPromptBuilder
{
    // Canonical artifact ordering for the manifest's IncludedArtifactPaths.
    private static readonly string[] ArtifactRoleOrder =
    [
        "Plan",
        "Milestone",
        "OperationalContext",
        "CurrentHandoff"
    ];

    // Roles whose contents compose the catalog {plan} hole, in execution order.
    private static readonly string[] PlanContextRoles =
    [
        "Plan",
        "Milestone",
        "OperationalContext"
    ];

    public ExecutionPrompt Build(ExecutionContext context)
    {
        string? plan = ComposePlanContext(context.Artifacts);
        string? handoff = ContentForRole(context.Artifacts, "CurrentHandoff");
        string? decisions = ComposeDecisions(context.DecisionProjection);

        // A prior handoff means we are continuing an in-flight milestone; otherwise this is a
        // first-milestone start (StartExecution has no handoff/decisions holes).
        string text = handoff is not null
            ? ContinueExecution.Render(plan, handoff, decisions)
            : StartExecution.Render(plan);

        return new ExecutionPrompt
        {
            Text = text,
            Metadata = new ExecutionPromptMetadata
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                RepositoryPath = context.Path,
                IncludedArtifactPaths = OrderedArtifacts(context.Artifacts)
                    .Select(artifact => artifact.RelativePath)
                    .ToArray(),
                TotalContextBytes = context.Diagnostics.TotalBytes,
                TotalContextCharacters = context.Diagnostics.TotalCharacters,
                DirtyRepository = context.Snapshot?.DirtyState.IsClean == false
            }
        };
    }

    private static string? ComposePlanContext(IReadOnlyList<LoadedArtifact> artifacts)
    {
        string[] sections = PlanContextRoles
            .Select(role => ContentForRole(artifacts, role))
            .Where(content => content is not null)
            .Select(content => content!)
            .ToArray();

        return sections.Length == 0 ? null : string.Join("\n\n", sections);
    }

    // Renders the governed decision projection into the catalog {decisions} hole. This replaces the
    // raw decisions.md artifact: the structured projection is the canonical governance the agent must
    // honor. Empty sections are omitted; a projection with no governance renders as null (empty hole).
    private static string? ComposeDecisions(ExecutionDecisionProjection? projection)
    {
        if (projection is null)
        {
            return null;
        }

        var sections = new List<string>();
        AddSection(sections, "Constraints", projection.Constraints
            .OrderBy(constraint => constraint.DecisionId, StringComparer.Ordinal)
            .Select(constraint => $"{constraint.DecisionId} ({constraint.ProjectionKind}, {constraint.Classification}): {constraint.Statement}"));
        AddSection(sections, "Directives", projection.Directives
            .OrderBy(directive => directive.DecisionId, StringComparer.Ordinal)
            .Select(directive => $"{directive.DecisionId} ({directive.ProjectionKind}, {directive.Classification}): {directive.Statement}"));
        AddSection(sections, "Priorities", projection.Priorities
            .OrderBy(priority => priority.Rank)
            .ThenBy(priority => priority.DecisionId, StringComparer.Ordinal)
            .Select(priority => $"P{priority.Rank} {priority.DecisionId} ({priority.ProjectionKind}, {priority.Classification}): {priority.Statement}"));
        AddSection(sections, "Architecture Rules", projection.ArchitectureRules
            .OrderBy(rule => rule.DecisionId, StringComparer.Ordinal)
            .Select(rule => $"{rule.DecisionId} ({rule.ProjectionKind}, {rule.Classification}): {rule.Statement}"));
        AddSection(sections, "Conflicts", projection.Conflicts
            .OrderBy(conflict => conflict.Id, StringComparer.Ordinal)
            .Select(conflict => $"{conflict.DecisionId}: {conflict.Statement} conflicts with `{conflict.ConflictingExcerpt}`"));

        return sections.Count == 0 ? null : string.Join("\n", sections);
    }

    private static void AddSection(List<string> sections, string label, IEnumerable<string> entries)
    {
        string[] lines = entries.ToArray();
        if (lines.Length == 0)
        {
            return;
        }

        sections.Add($"{label}:\n" + string.Join("\n", lines.Select(line => $"- {line}")));
    }

    private static string? ContentForRole(IReadOnlyList<LoadedArtifact> artifacts, string role)
    {
        return artifacts.FirstOrDefault(artifact => artifact.Role == role)?.Content;
    }

    private static IEnumerable<LoadedArtifact> OrderedArtifacts(IReadOnlyList<LoadedArtifact> artifacts)
    {
        return artifacts.OrderBy(artifact =>
            {
                int index = Array.IndexOf(ArtifactRoleOrder, artifact.Role);
                return index < 0 ? ArtifactRoleOrder.Length : index;
            })
            .ThenBy(artifact => artifact.RelativePath, StringComparer.Ordinal);
    }
}
