using CommandCenter.Core.Prompts;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Services;

/// <summary>
/// Renders the operational execution prompt from the <c>CommandCenter.Core.Prompts</c> catalog —
/// the single prompt authority. This is the catalog's first runtime consumer.
/// <para>
/// The previous literal/<see cref="System.Text.StringBuilder"/> composition (repository headers,
/// instruction block, repository snapshot, context diagnostics, and the governed decision
/// projection rendered inline) has been deleted: that prompt chrome is obsolete under the new
/// architecture. Substantive context flows into the catalog holes — Plan/Milestone/OperationalContext
/// compose the <c>{plan}</c> hole, with the current handoff and decisions filling their own holes on
/// continuation. Structured diagnostics survive only in <see cref="ExecutionPromptMetadata"/> and the
/// persisted prompt manifest, no longer concatenated into the prompt body.
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
        "CurrentHandoff",
        "CurrentDecisions"
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
        string? decisions = ContentForRole(context.Artifacts, "CurrentDecisions");

        // A prior handoff means we are continuing an in-flight milestone; otherwise this is a
        // first-milestone start (the StartExecution template has no handoff/decisions holes).
        string text = handoff is not null
            ? ContinueExecution.Render(plan, handoff, decisions)
            : StartExecution.Render(plan);

        return new ExecutionPrompt
        {
            Text = text,
            Metadata = new ExecutionPromptMetadata
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                RepositoryPath = context.RepositoryPath,
                MilestonePath = context.MilestonePath,
                IncludedArtifactPaths = OrderedArtifacts(context.Artifacts)
                    .Select(artifact => artifact.RelativePath)
                    .ToArray(),
                TotalContextBytes = context.Diagnostics.TotalBytes,
                TotalContextCharacters = context.Diagnostics.TotalCharacters,
                DirtyRepository = context.RepositorySnapshot?.DirtyState.IsClean == false
            }
        };
    }

    private static string? ComposePlanContext(IReadOnlyList<ExecutionContextArtifact> artifacts)
    {
        string[] sections = PlanContextRoles
            .Select(role => ContentForRole(artifacts, role))
            .Where(content => content is not null)
            .Select(content => content!)
            .ToArray();

        return sections.Length == 0 ? null : string.Join("\n\n", sections);
    }

    private static string? ContentForRole(IReadOnlyList<ExecutionContextArtifact> artifacts, string role)
    {
        return artifacts.FirstOrDefault(artifact => artifact.Role == role)?.Content;
    }

    private static IEnumerable<ExecutionContextArtifact> OrderedArtifacts(IReadOnlyList<ExecutionContextArtifact> artifacts)
    {
        return artifacts.OrderBy(artifact =>
            {
                int index = Array.IndexOf(ArtifactRoleOrder, artifact.Role);
                return index < 0 ? ArtifactRoleOrder.Length : index;
            })
            .ThenBy(artifact => artifact.RelativePath, StringComparer.Ordinal);
    }
}
