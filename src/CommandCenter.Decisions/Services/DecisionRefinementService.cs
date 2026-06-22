using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionRefinementService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionArtifactProjectionService projectionService) : IDecisionRefinementService
{
    public async Task<DecisionProposal> RefineProposalAsync(
        Guid repositoryId,
        string proposalId,
        DecisionRefinementRequest request)
    {
        if (request is null)
        {
            throw new ArgumentException("Refinement request is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Refinement reason is required.", nameof(request));
        }

        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        DecisionTransitionResult transition = DecisionLifecycleRules.ValidateProposalTransition(
            proposal.State,
            DecisionProposalState.Refined);
        if (!transition.IsValid)
        {
            throw new InvalidOperationException(transition.Error);
        }

        string sourceFingerprint = Fingerprint(proposal);
        if (!string.IsNullOrWhiteSpace(request.BaseProposalFingerprint) &&
            !string.Equals(request.BaseProposalFingerprint.Trim(), sourceFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refinement base proposal fingerprint is stale.");
        }

        DecisionOption[] options = request.Options?.ToArray() ?? proposal.Options.ToArray();
        if (options.Length == 0)
        {
            throw new ArgumentException("Refined proposals require at least one option.", nameof(request));
        }

        DecisionTradeoff[] tradeoffs = request.Tradeoffs?.ToArray() ?? proposal.Tradeoffs.ToArray();
        DecisionAssumption[] assumptions = request.Assumptions?.ToArray() ?? proposal.Assumptions.ToArray();
        DecisionRecommendation? recommendation = request.Recommendation ?? proposal.Recommendation;
        string context = request.Context ?? proposal.Context;
        string[] changedFields = ChangedFields(proposal, context, options, tradeoffs, recommendation, assumptions, request);
        if (changedFields.Length == 0)
        {
            throw new ArgumentException("Refinement must change proposal content.", nameof(request));
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string reason = request.Reason.Trim();
        string revisionId = await decisionRepository.AllocateProposalRevisionIdAsync(repository, proposal.Id);
        var source = new DecisionSourceReference("DecisionProposal", ProposalPath(proposal.Id), ProposalId: proposal.Id);
        DecisionOption[] retiredOptions = proposal.Options
            .Where(previous => !options.Any(current => string.Equals(current.Id, previous.Id, StringComparison.Ordinal)))
            .ToArray();
        DecisionAssumption[] retiredAssumptions = proposal.Assumptions
            .Where(previous => !assumptions.Any(current => string.Equals(current.Id, previous.Id, StringComparison.Ordinal)))
            .ToArray();

        var revision = new DecisionProposalRevision(
            revisionId,
            repository.Id,
            proposal.Id,
            now,
            reason,
            changedFields,
            sourceFingerprint,
            [source],
            string.IsNullOrWhiteSpace(request.RequestedBy) ? null : request.RequestedBy.Trim(),
            changedFields,
            request.RejectedChanges?.Where(change => !string.IsNullOrWhiteSpace(change)).Select(change => change.Trim()).ToArray() ?? [],
            BuildDiagnostics(request, retiredOptions, retiredAssumptions),
            proposal.Options.ToArray(),
            retiredOptions,
            proposal.Assumptions.ToArray(),
            retiredAssumptions,
            request.Constraints?.ToArray() ?? [],
            request.AssumptionRevisions?.ToArray() ?? [],
            request.OptionRevisions?.ToArray() ?? [],
            request.TradeoffRevisions?.ToArray() ?? [],
            proposal.Recommendation?.Rationale,
            recommendation?.Rationale);

        DecisionProposal updated = proposal with
        {
            State = DecisionProposalState.Refined,
            Context = context,
            Options = options,
            Tradeoffs = tradeoffs,
            Recommendation = recommendation,
            Assumptions = assumptions,
            History = proposal.History
                .Concat([
                    new DecisionHistoryEntry(
                        now,
                        "Refined",
                        proposal.State.ToString(),
                        DecisionProposalState.Refined.ToString(),
                        reason,
                        [
                            source,
                            new DecisionSourceReference(
                                "DecisionProposalRevision",
                                $".agents/decisions/proposals/{proposal.Id}/revisions/{revisionId}.json",
                                ProposalId: proposal.Id)
                        ])
                ])
                .ToArray()
        };

        await decisionRepository.SaveProposalRevisionAsync(repository, revision);
        await projectionService.ProjectProposalRevisionAsync(repository, revision);
        await decisionRepository.SaveProposalAsync(repository, updated);
        await projectionService.ProjectProposalAsync(repository, updated);
        await projectionService.RefreshDecisionIndexAsync(repository);
        return updated;
    }

    public async Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        _ = await GetProposalAsync(repository, proposalId);
        return await decisionRepository.ListProposalRevisionsAsync(repository, proposalId);
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

    private static string[] ChangedFields(
        DecisionProposal proposal,
        string context,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<DecisionTradeoff> tradeoffs,
        DecisionRecommendation? recommendation,
        IReadOnlyList<DecisionAssumption> assumptions,
        DecisionRefinementRequest request)
    {
        var changed = new List<string>();
        AddIfChanged("Context", proposal.Context, context);
        AddIfChanged("Options", proposal.Options, options);
        AddIfChanged("Tradeoffs", proposal.Tradeoffs, tradeoffs);
        AddIfChanged("Recommendation", proposal.Recommendation, recommendation);
        AddIfChanged("Assumptions", proposal.Assumptions, assumptions);
        AddIfPresent("Constraints", request.Constraints);
        AddIfPresent("AssumptionRevisions", request.AssumptionRevisions);
        AddIfPresent("OptionRevisions", request.OptionRevisions);
        AddIfPresent("TradeoffRevisions", request.TradeoffRevisions);
        AddIfPresent("RejectedChanges", request.RejectedChanges);
        return changed.Distinct(StringComparer.Ordinal).ToArray();

        void AddIfChanged<T>(string field, T before, T after)
        {
            if (!string.Equals(Serialize(before), Serialize(after), StringComparison.Ordinal))
            {
                changed.Add(field);
            }
        }

        void AddIfPresent<T>(string field, IReadOnlyList<T>? values)
        {
            if (values is { Count: > 0 })
            {
                changed.Add(field);
            }
        }
    }

    private static string[] BuildDiagnostics(
        DecisionRefinementRequest request,
        IReadOnlyList<DecisionOption> retiredOptions,
        IReadOnlyList<DecisionAssumption> retiredAssumptions)
    {
        var diagnostics = new List<string>();
        if (string.IsNullOrWhiteSpace(request.BaseProposalFingerprint))
        {
            diagnostics.Add("No base proposal fingerprint supplied; refinement used current proposal state.");
        }

        if (retiredOptions.Count > 0)
        {
            diagnostics.Add($"Retired {retiredOptions.Count} option(s) while preserving them in revision history.");
        }

        if (retiredAssumptions.Count > 0)
        {
            diagnostics.Add($"Retired {retiredAssumptions.Count} assumption(s) while preserving them in revision history.");
        }

        if (request.RejectedChanges is { Count: > 0 })
        {
            diagnostics.Add("Request included rejected changes for review traceability.");
        }

        return diagnostics.ToArray();
    }

    private static string Fingerprint(DecisionProposal proposal)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Serialize(proposal));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, DecisionJson.Options);
    }

    private static string ProposalPath(string proposalId)
    {
        return $".agents/decisions/proposals/{proposalId}/proposal.json";
    }
}
