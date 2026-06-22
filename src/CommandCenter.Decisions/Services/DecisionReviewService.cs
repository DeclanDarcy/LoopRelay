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
    public async Task<IReadOnlyList<DecisionProposalBrowserItem>> ListProposalBrowserItemsAsync(
        Guid repositoryId,
        IReadOnlySet<DecisionProposalState>? states = null)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
        Dictionary<string, DecisionCandidate> candidatesById = candidates.ToDictionary(
            candidate => candidate.Id,
            StringComparer.Ordinal);
        IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);
        var items = new List<DecisionProposalBrowserItem>();
        foreach (DecisionProposal proposal in proposals
            .Where(proposal => states is null || states.Contains(proposal.State))
            .OrderBy(proposal => proposal.Id, StringComparer.Ordinal))
        {
            DecisionReviewStatus reviewStatus =
                await decisionRepository.GetReviewStatusAsync(repository, proposal.Id) ??
                CreateDefaultReviewStatus(repository, proposal);
            candidatesById.TryGetValue(proposal.CandidateId, out DecisionCandidate? candidate);
            DateTimeOffset createdAt = proposal.History
                .Select(entry => entry.Timestamp)
                .DefaultIfEmpty(DateTimeOffset.MinValue)
                .Min();
            DateTimeOffset updatedAt = proposal.History
                .Select(entry => entry.Timestamp)
                .DefaultIfEmpty(DateTimeOffset.MinValue)
                .Max();
            items.Add(new DecisionProposalBrowserItem(
                proposal.Id,
                proposal.CandidateId,
                proposal.State,
                proposal.Title,
                candidate?.Classification ?? DecisionClassification.Tactical,
                candidate?.Priority ?? DecisionCandidatePriority.Medium,
                createdAt,
                updatedAt,
                reviewStatus.State,
                reviewStatus.UpdatedAt,
                proposal.State == DecisionProposalState.Resolved));
        }

        return items;
    }

    public async Task<DecisionReviewWorkspace> GetReviewWorkspaceAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        return await BuildWorkspaceAsync(repository, proposal);
    }

    public async Task<DecisionOptionComparison> GetOptionComparisonAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        DecisionOptionComparisonItem[] options = proposal.Options
            .Select(option =>
            {
                DecisionTradeoff[] tradeoffs = proposal.Tradeoffs
                    .Where(tradeoff => string.Equals(tradeoff.OptionId, option.Id, StringComparison.Ordinal))
                    .ToArray();
                DecisionEvidence[] evidence = option.Evidence
                    .Concat(tradeoffs.SelectMany(tradeoff => tradeoff.Evidence))
                    .DistinctBy(evidence => evidence.Summary, StringComparer.Ordinal)
                    .ToArray();
                return new DecisionOptionComparisonItem(
                    option.Id,
                    option.Title,
                    option.Description,
                    string.Equals(proposal.Recommendation?.OptionId, option.Id, StringComparison.Ordinal),
                    tradeoffs.Select(tradeoff => tradeoff.Benefit).ToArray(),
                    tradeoffs.Select(tradeoff => tradeoff.Cost).ToArray(),
                    evidence);
            })
            .ToArray();
        return new DecisionOptionComparison(proposal.Id, proposal.Recommendation?.OptionId, options);
    }

    public async Task<DecisionEvidenceInspection> GetEvidenceInspectionAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        IReadOnlyList<DecisionReviewNote> notes = await decisionRepository.ListReviewNotesAsync(repository, proposal.Id);
        return new DecisionEvidenceInspection(
            proposal.Id,
            proposal.CandidateId,
            BuildEvidenceItems(proposal),
            BuildDiagnostics(proposal, notes));
    }

    public async Task<IReadOnlyList<DecisionSourceAttribution>> ListSourceAttributionsAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        return BuildEvidenceItems(proposal)
            .SelectMany(item => item.Sources)
            .Concat(ProposalSource(proposal).YieldSourceAttribution("Proposal", proposal.Id))
            .OrderBy(source => source.RelativePath, StringComparer.Ordinal)
            .ThenBy(source => source.Section, StringComparer.Ordinal)
            .ThenBy(source => source.AppliesToKind, StringComparer.Ordinal)
            .ThenBy(source => source.ItemId, StringComparer.Ordinal)
            .ToArray();
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

    private static DecisionEvidenceInspectionItem[] BuildEvidenceItems(DecisionProposal proposal)
    {
        var items = new List<DecisionEvidenceInspectionItem>();
        AddEvidence("Proposal", proposal.Id, proposal.Evidence);
        foreach (DecisionOption option in proposal.Options)
        {
            AddEvidence("Option", option.Id, option.Evidence);
        }

        foreach (DecisionTradeoff tradeoff in proposal.Tradeoffs)
        {
            AddEvidence("Tradeoff", tradeoff.OptionId, tradeoff.Evidence);
        }

        if (proposal.Recommendation is not null)
        {
            AddEvidence("Recommendation", proposal.Recommendation.OptionId, proposal.Recommendation.Evidence);
        }

        foreach (DecisionAssumption assumption in proposal.Assumptions)
        {
            AddEvidence("Assumption", assumption.Id, assumption.Evidence);
        }

        return items
            .OrderBy(item => item.AppliesToKind, StringComparer.Ordinal)
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .ThenBy(item => item.Summary, StringComparer.Ordinal)
            .ToArray();

        void AddEvidence(string appliesToKind, string? itemId, IReadOnlyList<DecisionEvidence> evidence)
        {
            foreach (DecisionEvidence evidenceItem in evidence)
            {
                items.Add(new DecisionEvidenceInspectionItem(
                    appliesToKind,
                    itemId,
                    evidenceItem.Summary,
                    evidenceItem.Sources.Select(source => ToAttribution(appliesToKind, itemId, source)).ToArray()));
            }
        }
    }

    private static DecisionSourceAttribution ToAttribution(
        string appliesToKind,
        string? itemId,
        DecisionSourceReference source)
    {
        return new DecisionSourceAttribution(
            appliesToKind,
            itemId,
            source.SourceKind,
            source.RelativePath,
            source.Section,
            source.Excerpt,
            source);
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

file static class DecisionSourceReferenceExtensions
{
    public static IReadOnlyList<DecisionSourceAttribution> YieldSourceAttribution(
        this DecisionSourceReference source,
        string appliesToKind,
        string? itemId)
    {
        return
        [
            new DecisionSourceAttribution(
                appliesToKind,
                itemId,
                source.SourceKind,
                source.RelativePath,
                source.Section,
                source.Excerpt,
                source)
        ];
    }
}
