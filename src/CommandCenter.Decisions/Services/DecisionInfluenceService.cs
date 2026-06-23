using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionInfluenceService(
    IRepositoryService repositoryService,
    IArtifactStore artifactStore) : IDecisionInfluenceService
{
    public async Task<DecisionInfluenceTrace> RecordExecutionInfluenceAsync(
        Guid repositoryId,
        Guid executionSessionId,
        ExecutionDecisionProjection projection)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DateTimeOffset recordedAt = DateTimeOffset.UtcNow;
        string projectionFingerprint = string.IsNullOrWhiteSpace(projection.ProjectionFingerprint)
            ? FingerprintProjection(projection)
            : projection.ProjectionFingerprint;
        var trace = new DecisionInfluenceTrace(
            $"execution-{executionSessionId:N}",
            repositoryId,
            executionSessionId,
            recordedAt,
            projection.GeneratedAt,
            projectionFingerprint,
            BuildStatements(projection),
            BuildDiagnostics(projection));

        var document = new DecisionArtifactDocument<DecisionInfluenceTrace>(
            DecisionArtifactPaths.SchemaVersion,
            repository.Id,
            trace.RecordedAt,
            trace.RecordedAt,
            trace);
        await artifactStore.WriteAsync(
            DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.DecisionInfluenceJson(trace.Id)),
            JsonSerializer.Serialize(document, DecisionJson.Options));
        await artifactStore.WriteAsync(
            DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.DecisionInfluenceMarkdown(trace.Id)),
            RenderTrace(trace));
        return trace;
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static DecisionInfluenceStatement[] BuildStatements(ExecutionDecisionProjection projection)
    {
        return
        [
            .. projection.Constraints.Select(constraint => new DecisionInfluenceStatement(
                constraint.Id,
                constraint.DecisionId,
                constraint.Title,
                constraint.Statement,
                constraint.Classification,
                constraint.ProjectionKind,
                "Constraint",
                "Governed Decision Projection / Constraints",
                null,
                constraint.Sources,
                [])),
            .. projection.Directives.Select(directive => new DecisionInfluenceStatement(
                directive.Id,
                directive.DecisionId,
                directive.Title,
                directive.Statement,
                directive.Classification,
                directive.ProjectionKind,
                "Directive",
                "Governed Decision Projection / Directives",
                null,
                directive.Sources,
                [])),
            .. projection.Priorities.Select(priority => new DecisionInfluenceStatement(
                priority.Id,
                priority.DecisionId,
                priority.Title,
                priority.Statement,
                priority.Classification,
                priority.ProjectionKind,
                "Priority",
                "Governed Decision Projection / Priorities",
                priority.Rank,
                priority.Sources,
                [])),
            .. projection.ArchitectureRules.Select(rule => new DecisionInfluenceStatement(
                rule.Id,
                rule.DecisionId,
                rule.Title,
                rule.Statement,
                rule.Classification,
                rule.ProjectionKind,
                "ArchitectureRule",
                "Governed Decision Projection / Architecture Rules",
                null,
                rule.Sources,
                []))
        ];
    }

    private static string[] BuildDiagnostics(ExecutionDecisionProjection projection)
    {
        var diagnostics = new List<string>();
        diagnostics.AddRange(projection.Diagnostics);
        if (projection.Conflicts.Count > 0)
        {
            diagnostics.Add($"Projection contained {projection.Conflicts.Count} conflict(s) at execution prompt generation.");
        }

        return diagnostics.Order(StringComparer.Ordinal).ToArray();
    }

    private static string FingerprintProjection(ExecutionDecisionProjection projection)
    {
        var payload = new
        {
            projection.RepositoryId,
            projection.GeneratedAt,
            projection.Constraints,
            projection.Directives,
            projection.Priorities,
            projection.ArchitectureRules,
            projection.Conflicts,
            projection.Diagnostics
        };
        string json = JsonSerializer.Serialize(payload, DecisionJson.Options);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string RenderTrace(DecisionInfluenceTrace trace)
    {
        var builder = new StringBuilder();
        AppendLine($"# {trace.Id}: Decision Influence Trace");
        AppendLine();
        AppendLine($"- Repository: {trace.RepositoryId}");
        AppendLine($"- Execution session: {trace.ExecutionSessionId}");
        AppendLine($"- Recorded: {trace.RecordedAt.ToUniversalTime():O}");
        AppendLine($"- Projection generated: {trace.ProjectionGeneratedAt.ToUniversalTime():O}");
        AppendLine($"- Projection fingerprint: {trace.ProjectionFingerprint}");
        AppendLine($"- Statements: {trace.Statements.Count}");
        AppendLine();
        AppendLine("## Statements");
        AppendLine();
        foreach (DecisionInfluenceStatement statement in trace.Statements.OrderBy(statement => statement.StatementId, StringComparer.Ordinal))
        {
            string rank = statement.PriorityRank is null ? string.Empty : $" | P{statement.PriorityRank}";
            AppendLine($"- {statement.StatementId} | {statement.StatementType}{rank} | {statement.DecisionId} | {statement.PromptSection} | {statement.Statement}");
        }

        AppendEmptyIf(trace.Statements.Count == 0);
        AppendLine("## Diagnostics");
        AppendLine();
        foreach (string diagnostic in trace.Diagnostics.Order(StringComparer.Ordinal))
        {
            AppendLine($"- {diagnostic}");
        }

        AppendEmptyIf(trace.Diagnostics.Count == 0);
        return builder.ToString();

        void AppendEmptyIf(bool condition)
        {
            if (condition)
            {
                AppendLine("- None.");
            }

            AppendLine();
        }

        void AppendLine(string text = "")
        {
            builder.Append(text);
            builder.Append('\n');
        }
    }
}
