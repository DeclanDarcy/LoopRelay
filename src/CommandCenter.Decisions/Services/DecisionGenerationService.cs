using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionGenerationService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionArtifactProjectionService projectionService) : IDecisionGenerationService
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
        DecisionOption[] options = BuildOptions(candidate, evidence);
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

    private static DecisionOption[] BuildOptions(DecisionCandidate candidate, IReadOnlyList<DecisionEvidence> evidence)
    {
        var options = new List<DecisionOption>
        {
            new(
                "option-1",
                $"Resolve {candidate.Title}",
                $"Adopt a project direction that directly addresses the promoted candidate: {candidate.Summary}",
                EvidenceForRecommendation(evidence, candidate))
        };

        if (candidate.Signals.Any(signal => signal.Kind is "Conflict" or "ArchitecturalFork"))
        {
            options.Add(new DecisionOption(
                "option-2",
                "Preserve current direction until stronger evidence exists",
                "Defer authoritative direction and keep existing behavior unchanged while collecting clearer evidence.",
                evidence.Where(item => item.Sources.Any(source => source.CandidateId == candidate.Id)).ToArray()));
        }

        return options.ToArray();
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

    private static string CandidatePath(string candidateId)
    {
        return $".agents/decisions/candidates/{candidateId}/candidate.json";
    }

    private static string ProposalPath(string proposalId)
    {
        return $".agents/decisions/proposals/{proposalId}/proposal.json";
    }
}
