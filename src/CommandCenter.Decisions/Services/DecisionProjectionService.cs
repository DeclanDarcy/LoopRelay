using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionProjectionService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionGovernanceService governanceService) : IDecisionProjectionService
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
        var diagnostics = new List<string>();

        foreach (Decision decision in decisions.OrderBy(decision => decision.Id.Value, StringComparer.Ordinal))
        {
            if (!IsAcceptedResolvedDecision(decision))
            {
                continue;
            }

            if (blockedDecisionIds.Contains(decision.Id.Value))
            {
                diagnostics.Add($"Excluded {decision.Id.Value}: blocking governance finding prevents execution projection.");
                continue;
            }

            string statement = BuildStatement(decision);
            DecisionSourceReference[] sources = BuildSources(decision);
            if (decision.Classification is DecisionClassification.Architectural or DecisionClassification.Strategic)
            {
                constraints.Add(new ExecutionConstraint(
                    $"ECON-{constraints.Count + 1:0000}",
                    decision.Id.Value,
                    decision.Title,
                    statement,
                    decision.Classification,
                    sources));
            }
            else
            {
                directives.Add(new ExecutionDirective(
                    $"EDIR-{directives.Count + 1:0000}",
                    decision.Id.Value,
                    decision.Title,
                    statement,
                    decision.Classification,
                    sources));
            }
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

        return new ExecutionDecisionProjection(
            repositoryId,
            DateTimeOffset.UtcNow,
            constraints,
            directives,
            conflicts,
            diagnostics.Order(StringComparer.Ordinal).ToArray());
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

    private static ExecutionDecisionConflict[] DetectConflicts(
        IReadOnlyList<ProjectedStatement> statements,
        string? executionRequest,
        string? milestoneContent)
    {
        string combinedInput = string.Join(Environment.NewLine, new[] { executionRequest, milestoneContent }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(combinedInput))
        {
            return [];
        }

        var conflicts = new List<ExecutionDecisionConflict>();
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
}
