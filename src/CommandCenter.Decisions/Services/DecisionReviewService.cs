using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionReviewService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IDecisionGenerationService generationService) : IDecisionReviewService
{
    public async Task<DecisionReviewWorkspace> GetReviewWorkspaceAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        return await BuildWorkspaceAsync(repository, proposal);
    }

    public async Task<IReadOnlyList<DecisionReviewNote>> ListReviewNotesAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        _ = await GetProposalAsync(repository, proposalId);
        return await decisionRepository.ListReviewNotesAsync(repository, proposalId);
    }

    public async Task<DecisionReviewNote> AddReviewNoteAsync(
        Guid repositoryId,
        string proposalId,
        DecisionReviewNoteRequest request)
    {
        if (request is null)
        {
            throw new ArgumentException("Review note request is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            throw new ArgumentException("Review note body is required.", nameof(request));
        }

        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        string noteId = await decisionRepository.AllocateReviewNoteIdAsync(repository, proposal.Id);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionSourceReference proposalSource = ProposalSource(proposal);
        var note = new DecisionReviewNote(
            noteId,
            repository.Id,
            proposal.Id,
            now,
            string.IsNullOrWhiteSpace(request.Reviewer) ? "reviewer" : request.Reviewer.Trim(),
            request.Body.Trim(),
            NormalizeSources(request.Sources, proposalSource));

        return await decisionRepository.SaveReviewNoteAsync(repository, note);
    }

    public async Task<DecisionReviewWorkspace> MarkProposalViewedAsync(
        Guid repositoryId,
        string proposalId,
        string? reason)
    {
        DecisionProposal proposal = await generationService.MarkProposalViewedAsync(repositoryId, proposalId, reason);
        return await SaveStatusAndBuildWorkspaceAsync(
            repositoryId,
            proposal,
            DecisionReviewState.Viewed,
            reason ?? "Proposal marked viewed by explicit review operation.");
    }

    public async Task<DecisionReviewWorkspace> MarkProposalNeedsRefinementAsync(
        Guid repositoryId,
        string proposalId,
        string? reason)
    {
        DecisionProposal proposal = await generationService.MarkProposalNeedsRefinementAsync(repositoryId, proposalId, reason);
        return await SaveStatusAndBuildWorkspaceAsync(
            repositoryId,
            proposal,
            DecisionReviewState.NeedsRefinement,
            reason ?? "Proposal marked as needing refinement by explicit review operation.");
    }

    public async Task<DecisionReviewWorkspace> MarkProposalReadyForResolutionAsync(
        Guid repositoryId,
        string proposalId,
        string? reason)
    {
        DecisionProposal proposal = await generationService.MarkProposalReadyForResolutionAsync(repositoryId, proposalId, reason);
        return await SaveStatusAndBuildWorkspaceAsync(
            repositoryId,
            proposal,
            DecisionReviewState.ReadyForResolution,
            reason ?? "Proposal marked ready for resolution by explicit review operation.");
    }

    private async Task<DecisionReviewWorkspace> SaveStatusAndBuildWorkspaceAsync(
        Guid repositoryId,
        DecisionProposal proposal,
        DecisionReviewState reviewState,
        string reason)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var reviewStatus = new DecisionReviewStatus(
            repository.Id,
            proposal.Id,
            reviewState,
            now,
            reason,
            [ProposalSource(proposal)]);
        await decisionRepository.SaveReviewStatusAsync(repository, reviewStatus);
        return await BuildWorkspaceAsync(repository, proposal);
    }

    private async Task<DecisionReviewWorkspace> BuildWorkspaceAsync(Repository repository, DecisionProposal proposal)
    {
        DecisionReviewStatus reviewStatus =
            await decisionRepository.GetReviewStatusAsync(repository, proposal.Id) ??
            CreateDefaultReviewStatus(repository, proposal);
        IReadOnlyList<DecisionReviewNote> notes = await decisionRepository.ListReviewNotesAsync(repository, proposal.Id);
        IReadOnlyList<DecisionProposalRevision> revisions = await decisionRepository.ListProposalRevisionsAsync(repository, proposal.Id);
        return new DecisionReviewWorkspace(
            proposal,
            reviewStatus,
            notes,
            revisions,
            BuildDiagnostics(proposal, notes));
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

    private static DecisionReviewStatus CreateDefaultReviewStatus(Repository repository, DecisionProposal proposal)
    {
        DecisionReviewState state = proposal.State switch
        {
            DecisionProposalState.Viewed => DecisionReviewState.Viewed,
            DecisionProposalState.NeedsRefinement => DecisionReviewState.NeedsRefinement,
            DecisionProposalState.ReadyForResolution => DecisionReviewState.ReadyForResolution,
            DecisionProposalState.Resolved or DecisionProposalState.Expired or DecisionProposalState.Discarded => DecisionReviewState.Closed,
            _ => DecisionReviewState.NotStarted
        };

        return new DecisionReviewStatus(
            repository.Id,
            proposal.Id,
            state,
            DateTimeOffset.MinValue,
            null,
            [ProposalSource(proposal)]);
    }

    private static DecisionReviewDiagnostics BuildDiagnostics(
        DecisionProposal proposal,
        IReadOnlyList<DecisionReviewNote> notes)
    {
        var warnings = new List<string>();
        if (proposal.Options.Count == 0)
        {
            warnings.Add("Proposal has no options.");
        }

        if (proposal.Recommendation is null)
        {
            warnings.Add("Proposal has no recommendation.");
        }

        if (proposal.Evidence.Count == 0)
        {
            warnings.Add("Proposal has no proposal-level evidence.");
        }

        return new DecisionReviewDiagnostics(
            proposal.Recommendation is not null,
            proposal.Evidence.Count > 0,
            proposal.Options.Count,
            proposal.Tradeoffs.Count,
            proposal.Assumptions.Count,
            notes.Count,
            warnings);
    }

    private static IReadOnlyList<DecisionSourceReference> NormalizeSources(
        IReadOnlyList<DecisionSourceReference>? sources,
        DecisionSourceReference fallbackSource)
    {
        DecisionSourceReference[] normalized = (sources ?? [])
            .Where(source => !string.IsNullOrWhiteSpace(source.SourceKind))
            .ToArray();
        return normalized.Length == 0 ? [fallbackSource] : normalized;
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
