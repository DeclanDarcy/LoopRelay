using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class RefinementAnalysisService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository) : IRefinementAnalysisService
{
    public async Task<RefinementPlan> AnalyzeRefinementAsync(
        Guid repositoryId,
        string proposalId,
        DecisionRefinementAnalysisRequest request)
    {
        if (request is null)
        {
            throw new ArgumentException("Refinement analysis request is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Guidance))
        {
            throw new ArgumentException("Refinement guidance is required.", nameof(request));
        }

        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        string baseFingerprint = Fingerprint(proposal);
        if (!string.IsNullOrWhiteSpace(request.BaseProposalFingerprint) &&
            !string.Equals(request.BaseProposalFingerprint.Trim(), baseFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refinement base proposal fingerprint is stale.");
        }

        string guidance = request.Guidance.Trim();
        DecisionSourceReference source = ProposalSource(proposal);
        RefinementDirective[] directives = BuildDirectives(guidance, source);
        RefinementDirectiveType[] directiveTypes = directives.Select(directive => directive.Type).ToArray();
        bool fullRegeneration = directiveTypes.Contains(RefinementDirectiveType.ClarifyGoal);
        bool regenerateOptions = fullRegeneration || directiveTypes.Contains(RefinementDirectiveType.ExploreAlternative);
        bool reevaluateTradeoffs = fullRegeneration ||
            regenerateOptions ||
            directiveTypes.Contains(RefinementDirectiveType.AddConstraint) ||
            directiveTypes.Contains(RefinementDirectiveType.RemoveConstraint) ||
            directiveTypes.Contains(RefinementDirectiveType.ReevaluateRisk) ||
            directiveTypes.Contains(RefinementDirectiveType.ReevaluateCost) ||
            directiveTypes.Contains(RefinementDirectiveType.IncreasePriority) ||
            directiveTypes.Contains(RefinementDirectiveType.DecreasePriority);
        bool reevaluateRecommendation = fullRegeneration ||
            regenerateOptions ||
            reevaluateTradeoffs ||
            directiveTypes.Contains(RefinementDirectiveType.AddConstraint) ||
            directiveTypes.Contains(RefinementDirectiveType.RemoveConstraint) ||
            directiveTypes.Contains(RefinementDirectiveType.IncreasePriority) ||
            directiveTypes.Contains(RefinementDirectiveType.DecreasePriority) ||
            directiveTypes.Contains(RefinementDirectiveType.ReevaluateRecommendation);

        return new RefinementPlan(
            repository.Id,
            proposal.Id,
            DateTimeOffset.UtcNow,
            baseFingerprint,
            directives,
            regenerateOptions,
            reevaluateTradeoffs,
            reevaluateRecommendation,
            fullRegeneration,
            directives
                .Where(directive => directive.Type == RefinementDirectiveType.AddConstraint)
                .Select(directive => directive.Instruction ?? directive.Summary)
                .ToArray(),
            BuildDiagnostics(proposal, directives));
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<DecisionProposal> GetProposalAsync(Repository repository, string proposalId)
    {
        DecisionProposal? proposal = await decisionRepository.GetProposalAsync(repository, proposalId);
        return proposal ?? throw new KeyNotFoundException($"Decision proposal was not found: {proposalId}");
    }

    private static RefinementDirective[] BuildDirectives(string guidance, DecisionSourceReference source)
    {
        var directives = new List<RefinementDirective>();
        AddIf(ContainsAny(guidance, "remove constraint", "drop constraint"), RefinementDirectiveType.RemoveConstraint, "Remove or relax a stated constraint.", "Constraints");
        AddIf(ContainsAny(guidance, "constraint", "must", "require", "required"), RefinementDirectiveType.AddConstraint, "Add or tighten a review constraint.", "Constraints");
        AddIf(ContainsAny(guidance, "increase priority", "raise priority", "escalate", "blocking", "urgent"), RefinementDirectiveType.IncreasePriority, "Increase the priority of the decision or selected concern.", "Priority");
        AddIf(ContainsAny(guidance, "decrease priority", "lower priority", "deprioritize"), RefinementDirectiveType.DecreasePriority, "Decrease the priority of the decision or selected concern.", "Priority");
        AddIf(ContainsAny(guidance, "alternative", "another option", "new option", "explore", "compare against"), RefinementDirectiveType.ExploreAlternative, "Explore an additional or revised option.", "Options");
        AddIf(ContainsAny(guidance, "risk", "failure mode", "unsafe"), RefinementDirectiveType.ReevaluateRisk, "Reevaluate risk analysis.", "Risks");
        AddIf(ContainsAny(guidance, "cost", "effort", "expensive", "maintenance"), RefinementDirectiveType.ReevaluateCost, "Reevaluate cost analysis.", "Costs");
        AddIf(ContainsAny(guidance, "recommend", "recommendation", "preferred"), RefinementDirectiveType.ReevaluateRecommendation, "Reevaluate the recommendation.", "Recommendation");
        AddIf(ContainsAny(guidance, "goal", "scope", "clarify", "objective"), RefinementDirectiveType.ClarifyGoal, "Clarify the decision goal or scope.", "Context");

        if (directives.Count == 0)
        {
            Add(RefinementDirectiveType.ClarifyGoal, "Clarify the reviewer guidance before regeneration.", "Context");
        }

        return directives.ToArray();

        void AddIf(bool condition, RefinementDirectiveType type, string summary, string targetField)
        {
            if (condition)
            {
                Add(type, summary, targetField);
            }
        }

        void Add(RefinementDirectiveType type, string summary, string targetField)
        {
            if (directives.Any(directive => directive.Type == type))
            {
                return;
            }

            directives.Add(new RefinementDirective(
                $"DIR-{directives.Count + 1:0000}",
                type,
                summary,
                TargetField: targetField,
                Instruction: guidance,
                Sources: [source]));
        }
    }

    private static string[] BuildDiagnostics(DecisionProposal proposal, IReadOnlyList<RefinementDirective> directives)
    {
        var diagnostics = new List<string>
        {
            "Refinement analysis is advisory and does not mutate proposal content or package versions.",
            $"Analyzed {directives.Count} directive(s) from reviewer guidance."
        };

        if (proposal.State != DecisionProposalState.NeedsRefinement)
        {
            diagnostics.Add($"Proposal is currently {proposal.State}; regeneration still requires a valid refinement lifecycle transition.");
        }

        return diagnostics.ToArray();
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Fingerprint(DecisionProposal proposal)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(proposal, DecisionJson.Options));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static DecisionSourceReference ProposalSource(DecisionProposal proposal)
    {
        return new DecisionSourceReference(
            "DecisionProposal",
            $".agents/decisions/proposals/{proposal.Id}/proposal.json",
            ProposalId: proposal.Id,
            CandidateId: proposal.CandidateId);
    }
}
