using System.Text;
using CommandCenter.Decisions.Models;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;

namespace CommandCenter.Execution.Services;

public sealed class ExecutionPromptBuilder : IExecutionPromptBuilder
{
    private static readonly string[] ArtifactRoleOrder =
    [
        "Plan",
        "Milestone",
        "OperationalContext",
        "CurrentHandoff",
        "CurrentDecisions"
    ];

    public ExecutionPrompt Build(ExecutionContext context)
    {
        var builder = new StringBuilder();

        builder.AppendLine("# Command Center Execution Prompt");
        builder.AppendLine();
        builder.AppendLine("## Repository");
        builder.AppendLine($"Path: {context.RepositoryPath}");
        builder.AppendLine($"Name: {context.RepositoryName}");
        builder.AppendLine();
        builder.AppendLine("## Selected Milestone");
        builder.AppendLine(context.MilestonePath);
        builder.AppendLine();
        builder.AppendLine("## Instructions");
        builder.AppendLine("- Work only inside the repository path shown above.");
        builder.AppendLine("- Execute only the selected milestone slice.");
        builder.AppendLine("- Produce or update `.agents/handoffs/handoff.md` before completing.");
        builder.AppendLine("- Do not commit changes.");
        builder.AppendLine("- Do not push changes.");
        builder.AppendLine("- Leave handoff acceptance, commit, and push control to Command Center.");
        builder.AppendLine();
        AppendRepositorySnapshot(builder, context.RepositorySnapshot);
        AppendContextDiagnostics(builder, context.Diagnostics);
        AppendGovernedDecisions(builder, context.DecisionProjection);
        AppendArtifacts(builder, context.Artifacts);

        return new ExecutionPrompt
        {
            Text = builder.ToString(),
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

    private static void AppendGovernedDecisions(StringBuilder builder, ExecutionDecisionProjection? projection)
    {
        builder.AppendLine("## Governed Decision Projection");
        if (projection is null)
        {
            builder.AppendLine("Projection: unavailable");
            builder.AppendLine();
            return;
        }

        AppendDecisionDiagnostics(builder, projection.Diagnostics);
        AppendConflicts(builder, projection.Conflicts);
        AppendConstraints(builder, projection.Constraints);
        AppendDirectives(builder, projection.Directives);
        builder.AppendLine();
    }

    private static void AppendDecisionDiagnostics(StringBuilder builder, IReadOnlyList<string> diagnostics)
    {
        builder.AppendLine("Diagnostics:");
        if (diagnostics.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (string diagnostic in diagnostics.Order(StringComparer.Ordinal))
        {
            builder.AppendLine($"- {diagnostic}");
        }
    }

    private static void AppendConflicts(StringBuilder builder, IReadOnlyList<ExecutionDecisionConflict> conflicts)
    {
        builder.AppendLine("Conflicts:");
        if (conflicts.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (ExecutionDecisionConflict conflict in conflicts.OrderBy(conflict => conflict.Id, StringComparer.Ordinal))
        {
            builder.AppendLine($"- {conflict.DecisionId}: {conflict.Statement} conflicts with `{conflict.ConflictingExcerpt}`");
        }
    }

    private static void AppendConstraints(StringBuilder builder, IReadOnlyList<ExecutionConstraint> constraints)
    {
        builder.AppendLine("Constraints:");
        if (constraints.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (ExecutionConstraint constraint in constraints.OrderBy(constraint => constraint.DecisionId, StringComparer.Ordinal))
        {
            builder.AppendLine($"- {constraint.DecisionId} ({constraint.Classification}): {constraint.Statement}");
        }
    }

    private static void AppendDirectives(StringBuilder builder, IReadOnlyList<ExecutionDirective> directives)
    {
        builder.AppendLine("Directives:");
        if (directives.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (ExecutionDirective directive in directives.OrderBy(directive => directive.DecisionId, StringComparer.Ordinal))
        {
            builder.AppendLine($"- {directive.DecisionId} ({directive.Classification}): {directive.Statement}");
        }
    }

    private static void AppendRepositorySnapshot(StringBuilder builder, ExecutionRepositorySnapshot? snapshot)
    {
        builder.AppendLine("## Repository Snapshot");
        if (snapshot is null)
        {
            builder.AppendLine("Git snapshot: unavailable");
            builder.AppendLine();
            return;
        }

        builder.AppendLine($"Branch: {snapshot.Branch}");
        builder.AppendLine($"Working tree clean: {FormatBoolean(snapshot.DirtyState.IsClean)}");
        AppendPathGroup(builder, "Staged paths", snapshot.DirtyState.StagedPaths);
        AppendPathGroup(builder, "Modified paths", snapshot.DirtyState.ModifiedPaths);
        AppendPathGroup(builder, "Deleted paths", snapshot.DirtyState.DeletedPaths);
        AppendPathGroup(builder, "Renamed paths", snapshot.DirtyState.RenamedPaths);
        AppendPathGroup(builder, "Untracked paths", snapshot.DirtyState.UntrackedPaths);
        builder.AppendLine();
    }

    private static void AppendContextDiagnostics(StringBuilder builder, ExecutionContextDiagnostics diagnostics)
    {
        builder.AppendLine("## Context Diagnostics");
        builder.AppendLine($"Total bytes: {diagnostics.TotalBytes}");
        builder.AppendLine($"Total characters: {diagnostics.TotalCharacters}");
        builder.AppendLine($"Warning threshold exceeded: {FormatBoolean(diagnostics.WarningThresholdExceeded)}");
        builder.AppendLine($"Hard limit exceeded: {FormatBoolean(diagnostics.HardLimitExceeded)}");
        AppendValueGroup(builder, "Missing optional artifacts", diagnostics.MissingOptionalArtifacts);
        builder.AppendLine();
    }

    private static void AppendArtifacts(StringBuilder builder, IReadOnlyList<ExecutionContextArtifact> artifacts)
    {
        builder.AppendLine("## Context Artifacts");
        foreach (ExecutionContextArtifact artifact in OrderedArtifacts(artifacts))
        {
            builder.AppendLine();
            builder.AppendLine($"### {artifact.Role}: {artifact.RelativePath}");
            builder.AppendLine("```text");
            builder.AppendLine(artifact.Content);
            builder.AppendLine("```");
        }
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

    private static void AppendPathGroup(StringBuilder builder, string label, IReadOnlyList<string> paths)
    {
        AppendValueGroup(builder, label, paths.OrderBy(path => path, StringComparer.Ordinal).ToArray());
    }

    private static void AppendValueGroup(StringBuilder builder, string label, IReadOnlyList<string> values)
    {
        builder.AppendLine($"{label}:");
        if (values.Count == 0)
        {
            builder.AppendLine("- none");
            return;
        }

        foreach (string value in values)
        {
            builder.AppendLine($"- {value}");
        }
    }

    private static string FormatBoolean(bool value)
    {
        return value ? "yes" : "no";
    }
}
