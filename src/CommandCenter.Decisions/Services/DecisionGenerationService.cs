using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;
using CommandCenter.Decisions.Primitives;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionGenerationService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionArtifactProjectionService projectionService,
    IOptionGenerationService optionGenerationService) : IDecisionGenerationService
{
    public async Task<IReadOnlyList<DecisionProposal>> ListProposalsAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        return await decisionRepository.ListProposalsAsync(repository);
    }

    public async Task<DecisionProposal> GetProposalAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal? proposal = await decisionRepository.GetProposalAsync(repository, proposalId);
        return proposal ?? throw new KeyNotFoundException($"Decision proposal was not found: {proposalId}");
    }

    public async Task<DecisionProposal> GenerateProposalAsync(Guid repositoryId, string candidateId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionCandidate candidate = await GetCandidateAsync(repository, candidateId);
        if (candidate.State != DecisionCandidateState.Promoted)
        {
            throw new InvalidOperationException("Only promoted candidates can generate decision proposals.");
        }

        IReadOnlyList<DecisionProposal> existing = await decisionRepository.ListProposalsAsync(repository);
        if (existing.Any(proposal =>
            proposal.CandidateId == candidate.Id &&
            proposal.State is not DecisionProposalState.Expired and not DecisionProposalState.Discarded))
        {
            throw new InvalidOperationException($"An active proposal already exists for candidate {candidate.Id}.");
        }

        string proposalId = await decisionRepository.AllocateProposalIdAsync(repository);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionEvidence candidateEvidence = new(
            $"Candidate {candidate.Id} was promoted for proposal generation.",
            [new DecisionSourceReference(
                "DecisionCandidate",
                CandidatePath(candidate.Id),
                CandidateId: candidate.Id)]);
        DecisionEvidence[] evidence = candidate.Evidence
            .Concat([candidateEvidence])
            .OrderBy(evidence => evidence.Summary, StringComparer.Ordinal)
            .ToArray();
        DecisionOption[] options = optionGenerationService.GenerateOptions(candidate, evidence).ToArray();
        DecisionTradeoff[] tradeoffs = BuildTradeoffs(candidate, options, evidence);
        DecisionAssumption[] assumptions = BuildAssumptions(candidate, options, evidence);
        var recommendation = new DecisionRecommendation(
            options[0].Id,
            $"Advisory recommendation: resolve '{candidate.Title}' using the option most directly supported by the promoted candidate evidence.",
            EvidenceForRecommendation(evidence, candidate));

        var proposal = new DecisionProposal(
            proposalId,
            repository.Id,
            candidate.Id,
            DecisionProposalState.Generated,
            candidate.Title,
            candidate.Summary,
            options,
            tradeoffs,
            recommendation,
            assumptions,
            evidence,
            [new DecisionHistoryEntry(
                now,
                "Generated",
                null,
                DecisionProposalState.Generated.ToString(),
                "Generated from promoted decision candidate.",
                [new DecisionSourceReference("DecisionCandidate", CandidatePath(candidate.Id), CandidateId: candidate.Id)])]);

        await decisionRepository.SaveProposalAsync(repository, proposal);
        await projectionService.ProjectProposalAsync(repository, proposal);
        await projectionService.RefreshDecisionIndexAsync(repository);
        return proposal;
    }

    public async Task<DecisionProposal> ExpireProposalAsync(Guid repositoryId, string proposalId, string? reason)
    {
        return await TransitionProposalAsync(
            repositoryId,
            proposalId,
            DecisionProposalState.Expired,
            "Expired",
            reason ?? "Proposal expired by explicit proposal-management operation.");
    }

    public async Task<DecisionProposal> DiscardProposalAsync(Guid repositoryId, string proposalId, string? reason)
    {
        return await TransitionProposalAsync(
            repositoryId,
            proposalId,
            DecisionProposalState.Discarded,
            "Discarded",
            reason ?? "Proposal discarded by explicit proposal-management operation.");
    }

    public async Task<DecisionProposal> MarkProposalViewedAsync(Guid repositoryId, string proposalId, string? reason)
    {
        return await TransitionProposalAsync(
            repositoryId,
            proposalId,
            DecisionProposalState.Viewed,
            "Viewed",
            reason ?? "Proposal marked viewed by explicit review operation.");
    }

    public async Task<DecisionProposal> MarkProposalNeedsRefinementAsync(Guid repositoryId, string proposalId, string? reason)
    {
        return await TransitionProposalAsync(
            repositoryId,
            proposalId,
            DecisionProposalState.NeedsRefinement,
            "NeedsRefinement",
            reason ?? "Proposal marked as needing refinement by explicit review operation.");
    }

    public async Task<DecisionProposal> MarkProposalReadyForResolutionAsync(Guid repositoryId, string proposalId, string? reason)
    {
        return await TransitionProposalAsync(
            repositoryId,
            proposalId,
            DecisionProposalState.ReadyForResolution,
            "ReadyForResolution",
            reason ?? "Proposal marked ready for resolution by explicit review operation.");
    }

    public async Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        _ = await GetProposalAsync(repositoryId, proposalId);
        return await decisionRepository.ListProposalRevisionsAsync(repository, proposalId);
    }

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
        DecisionProposal proposal = await GetProposalAsync(repositoryId, proposalId);
        DecisionTransitionResult transition = DecisionLifecycleRules.ValidateProposalTransition(
            proposal.State,
            DecisionProposalState.Refined);
        if (!transition.IsValid)
        {
            throw new InvalidOperationException(transition.Error);
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
        string[] changedFields = ChangedFields(proposal, context, options, tradeoffs, recommendation, assumptions);
        if (changedFields.Length == 0)
        {
            throw new ArgumentException("Refinement must change proposal content.", nameof(request));
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string revisionId = await decisionRepository.AllocateProposalRevisionIdAsync(repository, proposal.Id);
        var source = new DecisionSourceReference("DecisionProposal", ProposalPath(proposal.Id), ProposalId: proposal.Id);
        var revision = new DecisionProposalRevision(
            revisionId,
            repository.Id,
            proposal.Id,
            now,
            request.Reason.Trim(),
            changedFields,
            Fingerprint(proposal),
            [source]);

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
                        request.Reason.Trim(),
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

    private async Task<DecisionProposal> TransitionProposalAsync(
        Guid repositoryId,
        string proposalId,
        DecisionProposalState targetState,
        string eventName,
        string reason)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repositoryId, proposalId);
        DecisionTransitionResult transition = DecisionLifecycleRules.ValidateProposalTransition(
            proposal.State,
            targetState);
        if (!transition.IsValid)
        {
            throw new InvalidOperationException(transition.Error);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionProposal updated = proposal with
        {
            State = targetState,
            History = proposal.History
                .Concat([
                    new DecisionHistoryEntry(
                        now,
                        eventName,
                        proposal.State.ToString(),
                        targetState.ToString(),
                        reason,
                        [new DecisionSourceReference("DecisionProposal", ProposalPath(proposal.Id), ProposalId: proposal.Id)])
                ])
                .ToArray()
        };

        await decisionRepository.SaveProposalAsync(repository, updated);
        await projectionService.ProjectProposalAsync(repository, updated);
        await projectionService.RefreshDecisionIndexAsync(repository);
        return updated;
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<DecisionCandidate> GetCandidateAsync(Repository repository, string candidateId)
    {
        DecisionCandidate? candidate = await decisionRepository.GetCandidateAsync(repository, candidateId);
        return candidate ?? throw new KeyNotFoundException($"Decision candidate was not found: {candidateId}");
    }

    private static DecisionTradeoff[] BuildTradeoffs(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        var tradeoffs = new List<DecisionTradeoff>
        {
            new(
                "option-1",
                "Creates an explicit lifecycle proposal from promoted candidate evidence.",
                "May resolve direction before all downstream implementation details are known.",
                EvidenceForRecommendation(evidence, candidate))
        };

        if (options.Any(option => option.Id == "option-2"))
        {
            tradeoffs.Add(new DecisionTradeoff(
                "option-2",
                "Avoids premature commitment while evidence is incomplete.",
                "Leaves the promoted candidate unresolved and may keep execution blocked.",
                evidence.Where(item => item.Sources.Any(source => source.CandidateId == candidate.Id)).ToArray()));
        }

        return tradeoffs.ToArray();
    }

    private static DecisionAssumption[] BuildAssumptions(
        DecisionCandidate candidate,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<DecisionEvidence> evidence)
    {
        var assumptions = new List<DecisionAssumption>
        {
            new(
                "assumption-1",
                "The promoted candidate evidence is current enough to support a first-pass proposal.",
                EvidenceForRecommendation(evidence, candidate))
        };

        if (options.Count == 1)
        {
            assumptions.Add(new DecisionAssumption(
                "assumption-2",
                "Only one viable option is currently represented in repository evidence; no unsupported alternatives were generated.",
                EvidenceForRecommendation(evidence, candidate)));
        }

        return assumptions.ToArray();
    }

    private static DecisionEvidence[] EvidenceForRecommendation(
        IReadOnlyList<DecisionEvidence> evidence,
        DecisionCandidate candidate)
    {
        return evidence
            .Where(item => item.Sources.Count == 0 ||
                item.Sources.Any(source => source.CandidateId == candidate.Id || source.RelativePath is not null))
            .OrderBy(item => item.Summary, StringComparer.Ordinal)
            .Take(4)
            .ToArray();
    }

    private static string[] ChangedFields(
        DecisionProposal proposal,
        string context,
        IReadOnlyList<DecisionOption> options,
        IReadOnlyList<DecisionTradeoff> tradeoffs,
        DecisionRecommendation? recommendation,
        IReadOnlyList<DecisionAssumption> assumptions)
    {
        var changed = new List<string>();
        AddIfChanged("Context", proposal.Context, context);
        AddIfChanged("Options", proposal.Options, options);
        AddIfChanged("Tradeoffs", proposal.Tradeoffs, tradeoffs);
        AddIfChanged("Recommendation", proposal.Recommendation, recommendation);
        AddIfChanged("Assumptions", proposal.Assumptions, assumptions);
        return changed.ToArray();

        void AddIfChanged<T>(string field, T before, T after)
        {
            if (!string.Equals(Serialize(before), Serialize(after), StringComparison.Ordinal))
            {
                changed.Add(field);
            }
        }
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

    private static string CandidatePath(string candidateId)
    {
        return $".agents/decisions/candidates/{candidateId}/candidate.json";
    }

    private static string ProposalPath(string proposalId)
    {
        return $".agents/decisions/proposals/{proposalId}/proposal.json";
    }
}
