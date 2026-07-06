using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Persistence;

namespace LoopRelay.Decisions.Services;

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
            BuildInfluenceId(executionSessionId),
            repositoryId,
            executionSessionId,
            recordedAt,
            projection.GeneratedAt,
            projectionFingerprint,
            BuildStatements(projection),
            projection.IncludedDecisions,
            projection.ExcludedDecisions,
            projection.SupersededDecisions,
            projection.ConflictingDecisions,
            projection.IgnoredDecisions,
            projection.BlockedDecisions,
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

    public async Task<DecisionInfluenceTrace?> GetExecutionInfluenceAsync(
        Guid repositoryId,
        Guid executionSessionId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        string influenceId = BuildInfluenceId(executionSessionId);
        return await ReadTraceAsync(repository, DecisionArtifactPaths.DecisionInfluenceJson(influenceId));
    }

    public async Task<IReadOnlyList<DecisionInfluenceTrace>> ListDecisionInfluenceAsync(
        Guid repositoryId,
        string decisionId)
    {
        if (string.IsNullOrWhiteSpace(decisionId))
        {
            throw new ArgumentException("Decision id is required.", nameof(decisionId));
        }

        Repository repository = await GetRepositoryAsync(repositoryId);
        string id = DecisionArtifactPaths.ValidateId(decisionId.Trim(), "DEC");
        string root = DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.InfluenceRootPath());
        IReadOnlyList<string> files = await artifactStore.ListAsync(root, "execution-*.json");
        var traces = new List<DecisionInfluenceTrace>();

        foreach (string file in files
            .Where(file => string.Equals(Path.GetExtension(file), ".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.Ordinal))
        {
            DecisionInfluenceTrace? trace = await ReadTraceAsync(
                repository,
                DecisionArtifactPaths.DecisionInfluenceJson(Path.GetFileNameWithoutExtension(file) ?? string.Empty));
            if (trace is not null &&
                trace.Statements.Any(statement => string.Equals(statement.DecisionId, id, StringComparison.Ordinal)))
            {
                traces.Add(trace);
            }
        }

        return traces
            .OrderByDescending(trace => trace.RecordedAt)
            .ThenBy(trace => trace.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<DecisionInfluenceTrace?> ReadTraceAsync(Repository repository, string relativePath)
    {
        string? json = await artifactStore.ReadAsync(DecisionArtifactPaths.Resolve(repository, relativePath));
        if (json is null)
        {
            return null;
        }

        DecisionArtifactDocument<DecisionInfluenceTrace>? document =
            JsonSerializer.Deserialize<DecisionArtifactDocument<DecisionInfluenceTrace>>(json, DecisionJson.Options);
        if (document is null)
        {
            return null;
        }

        if (!string.Equals(document.SchemaVersion, DecisionArtifactPaths.SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported decision artifact schema version '{document.SchemaVersion}'.");
        }

        if (document.RepositoryId != repository.Id)
        {
            throw new InvalidOperationException("Decision influence trace belongs to a different repository.");
        }

        return document.Payload;
    }

    private static string BuildInfluenceId(Guid executionSessionId)
    {
        return $"execution-{executionSessionId:N}";
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
            projection.Diagnostics,
            projection.IncludedDecisions,
            projection.ExcludedDecisions,
            projection.SupersededDecisions,
            projection.ConflictingDecisions,
            projection.IgnoredDecisions,
            projection.BlockedDecisions,
            projection.ProjectedStatements
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
        AppendLine($"- Included decisions: {trace.IncludedDecisions.Count}");
        AppendLine($"- Excluded decisions: {trace.ExcludedDecisions.Count}");
        AppendLine($"- Superseded decisions: {trace.SupersededDecisions.Count}");
        AppendLine($"- Conflicting decisions: {trace.ConflictingDecisions.Count}");
        AppendLine($"- Ignored decisions: {trace.IgnoredDecisions.Count}");
        AppendLine($"- Blocked decisions: {trace.BlockedDecisions.Count}");
        AppendLine();
        AppendDecisionSection("Included Decisions", trace.IncludedDecisions);
        AppendDecisionSection("Excluded Decisions", trace.ExcludedDecisions);
        AppendDecisionSection("Superseded Decisions", trace.SupersededDecisions);
        AppendDecisionSection("Conflicting Decisions", trace.ConflictingDecisions);
        AppendDecisionSection("Ignored Decisions", trace.IgnoredDecisions);
        AppendDecisionSection("Blocked Decisions", trace.BlockedDecisions);

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

        void AppendDecisionSection(string title, IReadOnlyList<DecisionProjectionDecisionDiagnostic> decisions)
        {
            AppendLine($"## {title}");
            AppendLine();
            foreach (DecisionProjectionDecisionDiagnostic decision in decisions.OrderBy(decision => decision.DecisionId, StringComparer.Ordinal))
            {
                string statementIds = decision.ProjectedStatementIds.Count == 0
                    ? "none"
                    : string.Join(", ", decision.ProjectedStatementIds);
                AppendLine($"- {decision.DecisionId} | {decision.State} | {decision.Outcome?.ToString() ?? "None"} | {decision.Classification} | {decision.Reason} | statements: {statementIds}");
            }

            AppendEmptyIf(decisions.Count == 0);
        }

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
