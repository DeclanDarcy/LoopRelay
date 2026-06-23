using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionProjectionService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionGovernanceService governanceService,
    IArtifactStore? artifactStore = null) : IDecisionProjectionService
{
    private static readonly (string Prefix, bool IsPositive)[] DirectivePrefixes =
    [
        ("do not use ", false),
        ("do not enable ", false),
        ("do not allow ", false),
        ("disable ", false),
        ("avoid ", false),
        ("exclude ", false),
        ("forbid ", false),
        ("reject ", false),
        ("remove ", false),
        ("prevent ", false),
        ("use ", true),
        ("enable ", true),
        ("adopt ", true),
        ("include ", true),
        ("allow ", true),
        ("require ", true),
        ("keep ", true),
        ("preserve ", true)
    ];

    private static readonly string[] TechnologyTerms =
    [
        "technology",
        "framework",
        "library",
        "package",
        "dependency",
        "provider",
        "runtime",
        "api",
        "sdk",
        ".net",
        "react",
        "tauri",
        "rust",
        "typescript"
    ];

    private static readonly string[] WorkflowTerms =
    [
        "workflow",
        "process",
        "review",
        "approval",
        "promotion",
        "governance",
        "handoff",
        "commit",
        "push",
        "rotation",
        "certification"
    ];

    private static readonly string[] RepositoryConventionTerms =
    [
        "repository",
        "repo",
        "artifact",
        "path",
        "file",
        "directory",
        "folder",
        ".agents",
        "markdown",
        "json",
        "projection",
        "naming",
        "convention"
    ];

    public async Task<ExecutionDecisionProjection> BuildExecutionProjectionAsync(
        Guid repositoryId,
        string? executionRequest = null,
        string? milestoneContent = null)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        DecisionGovernanceReport governanceReport = await governanceService.GetCurrentReportAsync(repositoryId);
        HashSet<string> blockedDecisionIds = governanceReport.Findings
            .Where(finding => finding.BlocksExecutionProjection)
            .SelectMany(finding => finding.RelatedDecisionIds)
            .ToHashSet(StringComparer.Ordinal);
        var constraints = new List<ExecutionConstraint>();
        var directives = new List<ExecutionDirective>();
        var priorities = new List<ExecutionDecisionPriority>();
        var architectureRules = new List<ExecutionArchitectureRule>();
        var diagnostics = new List<string>();
        var includedDecisions = new List<DecisionProjectionDecisionDiagnostic>();
        var excludedDecisions = new List<DecisionProjectionDecisionDiagnostic>();
        var supersededDecisions = new List<DecisionProjectionDecisionDiagnostic>();
        var projectedStatements = new List<DecisionProjectedStatement>();

        foreach (Decision decision in decisions.OrderBy(decision => decision.Id.Value, StringComparer.Ordinal))
        {
            if (!IsAcceptedResolvedDecision(decision))
            {
                string reason = BuildExclusionReason(decision);
                var excludedDecision = new DecisionProjectionDecisionDiagnostic(
                    decision.Id.Value,
                    decision.Title,
                    decision.State,
                    decision.Resolution?.Outcome,
                    decision.Classification,
                    reason,
                    []);
                excludedDecisions.Add(excludedDecision);
                if (decision.State is DecisionState.Superseded or DecisionState.Archived)
                {
                    diagnostics.Add($"Excluded {decision.Id.Value}: {reason}");
                }

                if (decision.State == DecisionState.Superseded)
                {
                    supersededDecisions.Add(excludedDecision);
                }

                continue;
            }

            if (blockedDecisionIds.Contains(decision.Id.Value))
            {
                const string reason = "Blocking governance finding prevents execution projection.";
                excludedDecisions.Add(new DecisionProjectionDecisionDiagnostic(
                    decision.Id.Value,
                    decision.Title,
                    decision.State,
                    decision.Resolution?.Outcome,
                    decision.Classification,
                    reason,
                    []));
                diagnostics.Add($"Excluded {decision.Id.Value}: {reason}");
                continue;
            }

            string statement = BuildStatement(decision);
            ExecutionProjectionKind projectionKind = ClassifyProjectionKind(decision, statement);
            DecisionSourceReference[] sources = BuildSources(decision);
            var statementIds = new List<string>();
            if (ProjectsAsConstraint(projectionKind))
            {
                string constraintId = $"ECON-{constraints.Count + 1:0000}";
                var constraint = new ExecutionConstraint(
                    constraintId,
                    decision.Id.Value,
                    decision.Title,
                    statement,
                    decision.Classification,
                    projectionKind,
                    sources);
                constraints.Add(constraint);
                statementIds.Add(constraintId);
                projectedStatements.Add(ToProjectedStatement(constraint, "Constraint"));

                string ruleId = $"EARC-{architectureRules.Count + 1:0000}";
                architectureRules.Add(new ExecutionArchitectureRule(
                    ruleId,
                    decision.Id.Value,
                    decision.Title,
                    statement,
                    decision.Classification,
                    projectionKind,
                    sources));
                statementIds.Add(ruleId);
                projectedStatements.Add(new DecisionProjectedStatement(
                    ruleId,
                    decision.Id.Value,
                    decision.Title,
                    statement,
                    decision.Classification,
                    projectionKind,
                    "ArchitectureRule",
                    sources));
            }
            else
            {
                string directiveId = $"EDIR-{directives.Count + 1:0000}";
                var directive = new ExecutionDirective(
                    directiveId,
                    decision.Id.Value,
                    decision.Title,
                    statement,
                    decision.Classification,
                    projectionKind,
                    sources);
                directives.Add(directive);
                statementIds.Add(directiveId);
                projectedStatements.Add(ToProjectedStatement(directive, "Directive"));
                if (ProjectsAsPriority(decision, statement))
                {
                    string priorityId = $"EPRI-{priorities.Count + 1:0000}";
                    priorities.Add(new ExecutionDecisionPriority(
                        priorityId,
                        decision.Id.Value,
                        decision.Title,
                        statement,
                        decision.Classification,
                        projectionKind,
                        priorities.Count + 1,
                        sources));
                    statementIds.Add(priorityId);
                    projectedStatements.Add(new DecisionProjectedStatement(
                        priorityId,
                        decision.Id.Value,
                        decision.Title,
                        statement,
                        decision.Classification,
                        projectionKind,
                        "Priority",
                        sources));
                }
            }

            includedDecisions.Add(new DecisionProjectionDecisionDiagnostic(
                decision.Id.Value,
                decision.Title,
                decision.State,
                decision.Resolution?.Outcome,
                decision.Classification,
                "Accepted resolved decision projected into execution context.",
                statementIds));
        }

        ExecutionDecisionConflict[] conflicts = DetectConflicts(
            [.. constraints.Select(constraint => new ProjectedStatement(
                constraint.DecisionId,
                constraint.Title,
                constraint.Statement,
                constraint.Sources)),
             .. directives.Select(directive => new ProjectedStatement(
                 directive.DecisionId,
                 directive.Title,
                 directive.Statement,
                 directive.Sources))],
            executionRequest,
            milestoneContent);

        string[] orderedDiagnostics = diagnostics.Order(StringComparer.Ordinal).ToArray();
        var context = new ExecutionDecisionContext(
            constraints,
            directives,
            priorities,
            architectureRules,
            conflicts,
            orderedDiagnostics);

        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        var projectionDiagnostics = BuildProjectionDiagnostics(
            repositoryId,
            generatedAt,
            includedDecisions,
            excludedDecisions,
            supersededDecisions,
            projectedStatements,
            conflicts,
            orderedDiagnostics);
        await PersistProjectionDiagnosticsAsync(repository, projectionDiagnostics);

        return new ExecutionDecisionProjection(
            repositoryId,
            generatedAt,
            constraints,
            directives,
            priorities,
            architectureRules,
            conflicts,
            orderedDiagnostics,
            context);
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private static bool IsAcceptedResolvedDecision(Decision decision)
    {
        return decision.State == DecisionState.Resolved &&
            decision.Resolution?.Outcome == DecisionOutcome.Accepted;
    }

    private static string BuildExclusionReason(Decision decision)
    {
        if (decision.State != DecisionState.Resolved)
        {
            return $"Decision state is {decision.State}.";
        }

        return decision.Resolution?.Outcome is null
            ? "Decision has no human resolution."
            : $"Resolution outcome is {decision.Resolution.Outcome}.";
    }

    private static string BuildStatement(Decision decision)
    {
        DecisionResolution resolution = decision.Resolution!;
        DecisionOption? selectedOption = decision.Resolution?.SourceProposalSnapshot?.Options
            .FirstOrDefault(option => string.Equals(option.Id, resolution.SelectedOptionId, StringComparison.Ordinal));
        if (selectedOption is not null)
        {
            return string.IsNullOrWhiteSpace(selectedOption.Description)
                ? selectedOption.Title
                : $"{selectedOption.Title}: {selectedOption.Description}";
        }

        return string.IsNullOrWhiteSpace(resolution.Rationale)
            ? decision.Context
            : resolution.Rationale;
    }

    private static bool ProjectsAsConstraint(ExecutionProjectionKind projectionKind)
    {
        return projectionKind is ExecutionProjectionKind.ArchitecturalConstraint
            or ExecutionProjectionKind.TechnologyChoice
            or ExecutionProjectionKind.RepositoryConvention;
    }

    private static bool ProjectsAsPriority(Decision decision, string statement)
    {
        string searchable = Normalize(string.Join(" ", decision.Title, decision.Context, statement));
        return decision.Classification == DecisionClassification.Strategic ||
            searchable.Contains("priority", StringComparison.Ordinal) ||
            searchable.Contains("prioritize", StringComparison.Ordinal) ||
            searchable.Contains("before ", StringComparison.Ordinal) ||
            searchable.Contains("first ", StringComparison.Ordinal);
    }

    private static ExecutionProjectionKind ClassifyProjectionKind(Decision decision, string statement)
    {
        string primaryText = Normalize(string.Join(
            " ",
            decision.Title,
            decision.Context,
            statement));
        ExecutionProjectionKind? primaryKind = TryClassifyByTerms(primaryText, includeRepositoryConvention: true);
        if (primaryKind is not null)
        {
            return primaryKind.Value;
        }

        string evidenceText = Normalize(string.Join(
            " ",
            string.Join(" ", decision.Evidence.Select(evidence => evidence.Summary)),
            string.Join(" ", decision.Resolution?.SourceProposalSnapshot?.Evidence.Select(evidence => evidence.Summary) ?? [])));
        ExecutionProjectionKind? evidenceKind = TryClassifyByTerms(evidenceText, includeRepositoryConvention: false);
        if (evidenceKind is not null)
        {
            return evidenceKind.Value;
        }

        return decision.Classification switch
        {
            DecisionClassification.Architectural => ExecutionProjectionKind.ArchitecturalConstraint,
            DecisionClassification.Strategic => ExecutionProjectionKind.WorkflowPolicy,
            DecisionClassification.Tactical => ExecutionProjectionKind.ImplementationDirective,
            DecisionClassification.Operational => ExecutionProjectionKind.WorkflowPolicy,
            _ => ExecutionProjectionKind.ImplementationDirective
        };
    }

    private static ExecutionProjectionKind? TryClassifyByTerms(string searchable, bool includeRepositoryConvention)
    {
        if (ContainsAny(searchable, TechnologyTerms))
        {
            return ExecutionProjectionKind.TechnologyChoice;
        }

        if (ContainsAny(searchable, WorkflowTerms))
        {
            return ExecutionProjectionKind.WorkflowPolicy;
        }

        if (includeRepositoryConvention && ContainsAny(searchable, RepositoryConventionTerms))
        {
            return ExecutionProjectionKind.RepositoryConvention;
        }

        return null;
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.Ordinal));
    }

    private static DecisionSourceReference[] BuildSources(Decision decision)
    {
        return decision.Resolution?.Sources.Count > 0
            ? decision.Resolution.Sources.ToArray()
            :
            [
                new DecisionSourceReference(
                    "DecisionRecord",
                    $".agents/decisions/records/{decision.Id.Value}/decision.json",
                    DecisionId: decision.Id)
            ];
    }

    private static DecisionProjectedStatement ToProjectedStatement(ExecutionConstraint constraint, string category)
    {
        return new DecisionProjectedStatement(
            constraint.Id,
            constraint.DecisionId,
            constraint.Title,
            constraint.Statement,
            constraint.Classification,
            constraint.ProjectionKind,
            category,
            constraint.Sources);
    }

    private static DecisionProjectedStatement ToProjectedStatement(ExecutionDirective directive, string category)
    {
        return new DecisionProjectedStatement(
            directive.Id,
            directive.DecisionId,
            directive.Title,
            directive.Statement,
            directive.Classification,
            directive.ProjectionKind,
            category,
            directive.Sources);
    }

    private static DecisionProjectionDiagnostics BuildProjectionDiagnostics(
        Guid repositoryId,
        DateTimeOffset generatedAt,
        IReadOnlyList<DecisionProjectionDecisionDiagnostic> includedDecisions,
        IReadOnlyList<DecisionProjectionDecisionDiagnostic> excludedDecisions,
        IReadOnlyList<DecisionProjectionDecisionDiagnostic> supersededDecisions,
        IReadOnlyList<DecisionProjectedStatement> projectedStatements,
        IReadOnlyList<ExecutionDecisionConflict> conflicts,
        IReadOnlyList<string> diagnostics)
    {
        string id = $"execution.{generatedAt.ToUniversalTime():yyyyMMddHHmmssfffffff}";
        string fingerprint = FingerprintProjection(
            repositoryId,
            includedDecisions,
            excludedDecisions,
            supersededDecisions,
            projectedStatements,
            conflicts,
            diagnostics);
        return new DecisionProjectionDiagnostics(
            id,
            repositoryId,
            generatedAt,
            fingerprint,
            includedDecisions.OrderBy(decision => decision.DecisionId, StringComparer.Ordinal).ToArray(),
            excludedDecisions.OrderBy(decision => decision.DecisionId, StringComparer.Ordinal).ToArray(),
            supersededDecisions.OrderBy(decision => decision.DecisionId, StringComparer.Ordinal).ToArray(),
            projectedStatements.OrderBy(statement => statement.Id, StringComparer.Ordinal).ToArray(),
            conflicts.OrderBy(conflict => conflict.Id, StringComparer.Ordinal).ToArray(),
            diagnostics.Order(StringComparer.Ordinal).ToArray());
    }

    private async Task PersistProjectionDiagnosticsAsync(
        Repository repository,
        DecisionProjectionDiagnostics diagnostics)
    {
        if (artifactStore is null)
        {
            return;
        }

        var document = new DecisionArtifactDocument<DecisionProjectionDiagnostics>(
            DecisionArtifactPaths.SchemaVersion,
            repository.Id,
            diagnostics.GeneratedAt,
            diagnostics.GeneratedAt,
            diagnostics);
        await artifactStore.WriteAsync(
            DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.ExecutionProjectionJson(diagnostics.Id)),
            JsonSerializer.Serialize(document, DecisionJson.Options));
        await artifactStore.WriteAsync(
            DecisionArtifactPaths.Resolve(repository, DecisionArtifactPaths.ExecutionProjectionMarkdown(diagnostics.Id)),
            RenderProjectionDiagnostics(diagnostics));
    }

    private static string FingerprintProjection(
        Guid repositoryId,
        IReadOnlyList<DecisionProjectionDecisionDiagnostic> includedDecisions,
        IReadOnlyList<DecisionProjectionDecisionDiagnostic> excludedDecisions,
        IReadOnlyList<DecisionProjectionDecisionDiagnostic> supersededDecisions,
        IReadOnlyList<DecisionProjectedStatement> projectedStatements,
        IReadOnlyList<ExecutionDecisionConflict> conflicts,
        IReadOnlyList<string> diagnostics)
    {
        var payload = new
        {
            RepositoryId = repositoryId,
            IncludedDecisions = includedDecisions.OrderBy(decision => decision.DecisionId, StringComparer.Ordinal),
            ExcludedDecisions = excludedDecisions.OrderBy(decision => decision.DecisionId, StringComparer.Ordinal),
            SupersededDecisions = supersededDecisions.OrderBy(decision => decision.DecisionId, StringComparer.Ordinal),
            ProjectedStatements = projectedStatements.OrderBy(statement => statement.Id, StringComparer.Ordinal),
            Conflicts = conflicts.OrderBy(conflict => conflict.Id, StringComparer.Ordinal),
            Diagnostics = diagnostics.Order(StringComparer.Ordinal)
        };
        string json = JsonSerializer.Serialize(payload, DecisionJson.Options);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string RenderProjectionDiagnostics(DecisionProjectionDiagnostics diagnostics)
    {
        var builder = new StringBuilder();
        AppendLine($"# {diagnostics.Id}: Execution Decision Projection");
        AppendLine();
        AppendFields(
            ("Repository", diagnostics.RepositoryId.ToString()),
            ("Generated", diagnostics.GeneratedAt.ToUniversalTime().ToString("O")),
            ("Projection fingerprint", diagnostics.ProjectionFingerprint),
            ("Included decisions", diagnostics.IncludedDecisions.Count.ToString()),
            ("Excluded decisions", diagnostics.ExcludedDecisions.Count.ToString()),
            ("Superseded decisions", diagnostics.SupersededDecisions.Count.ToString()),
            ("Projected statements", diagnostics.ProjectedStatements.Count.ToString()),
            ("Conflicts", diagnostics.Conflicts.Count.ToString()));

        AppendDecisionSection("Included Decisions", diagnostics.IncludedDecisions);
        AppendDecisionSection("Excluded Decisions", diagnostics.ExcludedDecisions);
        AppendDecisionSection("Superseded Decisions", diagnostics.SupersededDecisions);

        AppendLine("## Projected Statements");
        AppendLine();
        foreach (DecisionProjectedStatement statement in diagnostics.ProjectedStatements
            .OrderBy(statement => statement.Id, StringComparer.Ordinal))
        {
            AppendLine($"- {statement.Id} | {statement.ProjectionCategory} | {statement.DecisionId} | {statement.ProjectionKind} | {statement.Statement}");
        }

        AppendEmptyIf(diagnostics.ProjectedStatements.Count == 0);

        AppendLine("## Conflicts");
        AppendLine();
        foreach (ExecutionDecisionConflict conflict in diagnostics.Conflicts.OrderBy(conflict => conflict.Id, StringComparer.Ordinal))
        {
            AppendLine($"- {conflict.Id} | {conflict.DecisionId} | {conflict.Statement} | conflicts with: {conflict.ConflictingExcerpt}");
        }

        AppendEmptyIf(diagnostics.Conflicts.Count == 0);

        AppendLine("## Diagnostics");
        AppendLine();
        foreach (string diagnostic in diagnostics.Diagnostics.Order(StringComparer.Ordinal))
        {
            AppendLine($"- {diagnostic}");
        }

        AppendEmptyIf(diagnostics.Diagnostics.Count == 0);
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

        void AppendFields(params (string Label, string Value)[] fields)
        {
            foreach ((string label, string value) in fields)
            {
                AppendLine($"- {label}: {value}");
            }

            AppendLine();
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

    private static ExecutionDecisionConflict[] DetectConflicts(
        IReadOnlyList<ProjectedStatement> statements,
        string? executionRequest,
        string? milestoneContent)
    {
        var conflicts = new List<ExecutionDecisionConflict>();
        DetectProjectedStatementConflicts(statements, conflicts);
        string combinedInput = string.Join(Environment.NewLine, new[] { executionRequest, milestoneContent }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(combinedInput))
        {
            return conflicts.ToArray();
        }

        string normalizedInput = Normalize(combinedInput);
        foreach (ProjectedStatement statement in statements)
        {
            (bool HasDirective, bool IsPositive, string Subject) directive = ParseDirective(statement.Statement);
            if (!directive.HasDirective || directive.Subject.Length < 3)
            {
                continue;
            }

            string? conflictPrefix = DirectivePrefixes
                .Where(prefix => prefix.IsPositive != directive.IsPositive)
                .Select(prefix => prefix.Prefix)
                .FirstOrDefault(prefix => normalizedInput.Contains(prefix + directive.Subject, StringComparison.Ordinal));
            if (conflictPrefix is null)
            {
                continue;
            }

            conflicts.Add(new ExecutionDecisionConflict(
                $"ECONFLICT-{conflicts.Count + 1:0000}",
                statement.DecisionId,
                statement.Title,
                statement.Statement,
                $"{conflictPrefix}{directive.Subject}",
                statement.Sources));
        }

        return conflicts.ToArray();
    }

    private static void DetectProjectedStatementConflicts(
        IReadOnlyList<ProjectedStatement> statements,
        List<ExecutionDecisionConflict> conflicts)
    {
        ProjectedDirective[] directives = statements
            .Select(statement =>
            {
                (bool hasDirective, bool isPositive, string subject) = ParseDirective(statement.Statement);
                return new ProjectedDirective(statement, hasDirective, isPositive, subject);
            })
            .Where(directive => directive.HasDirective && directive.Subject.Length >= 3)
            .ToArray();

        for (int leftIndex = 0; leftIndex < directives.Length; leftIndex++)
        {
            for (int rightIndex = leftIndex + 1; rightIndex < directives.Length; rightIndex++)
            {
                ProjectedDirective left = directives[leftIndex];
                ProjectedDirective right = directives[rightIndex];
                if (left.IsPositive == right.IsPositive ||
                    !string.Equals(left.Subject, right.Subject, StringComparison.Ordinal))
                {
                    continue;
                }

                conflicts.Add(new ExecutionDecisionConflict(
                    $"ECONFLICT-{conflicts.Count + 1:0000}",
                    left.Statement.DecisionId,
                    left.Statement.Title,
                    left.Statement.Statement,
                    $"{right.Statement.DecisionId}: {right.Statement.Statement}",
                    [.. left.Statement.Sources, .. right.Statement.Sources]));
            }
        }
    }

    private static (bool HasDirective, bool IsPositive, string Subject) ParseDirective(string statement)
    {
        string normalized = Normalize(statement);
        foreach ((string prefix, bool isPositive) in DirectivePrefixes)
        {
            if (!normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            string subject = normalized[prefix.Length..].Trim();
            int punctuationIndex = subject.IndexOfAny(['.', ':', ';', ',']);
            if (punctuationIndex >= 0)
            {
                subject = subject[..punctuationIndex].Trim();
            }

            return (true, isPositive, subject);
        }

        return (false, false, string.Empty);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private sealed record ProjectedStatement(
        string DecisionId,
        string Title,
        string Statement,
        IReadOnlyList<DecisionSourceReference> Sources);

    private sealed record ProjectedDirective(
        ProjectedStatement Statement,
        bool HasDirective,
        bool IsPositive,
        string Subject);
}
