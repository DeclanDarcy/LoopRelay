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
            request.PriorityAdjustments?.ToArray() ?? [],
            proposal.Recommendation?.Rationale,
            recommendation?.Rationale,
            proposal.Context,
            context,
            options,
            proposal.Tradeoffs.ToArray(),
            tradeoffs,
            assumptions);

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
        await projectionService.ProjectProposalRevisionComparisonAsync(repository, BuildComparison(repository, updated, revision));
        await projectionService.RefreshDecisionIndexAsync(repository);
        return updated;
    }

    public async Task<IReadOnlyList<DecisionProposalRevision>> ListProposalRevisionsAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        _ = await GetProposalAsync(repository, proposalId);
        return await decisionRepository.ListProposalRevisionsAsync(repository, proposalId);
    }

    public async Task<DecisionProposalLineage> GetProposalLineageAsync(Guid repositoryId, string proposalId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        IReadOnlyList<DecisionProposalRevision> revisions =
            await decisionRepository.ListProposalRevisionsAsync(repository, proposal.Id);
        IReadOnlyList<DecisionReviewNote> notes =
            await decisionRepository.ListReviewNotesAsync(repository, proposal.Id);
        DecisionReviewStatus review =
            await decisionRepository.GetReviewStatusAsync(repository, proposal.Id) ??
            CreateDefaultReviewStatus(repository, proposal);
        string currentFingerprint = Fingerprint(proposal);

        DecisionProposalRevisionSnapshot[] snapshots = revisions
            .OrderBy(revision => revision.CreatedAt)
            .ThenBy(revision => revision.Id, StringComparer.Ordinal)
            .Select(revision => new DecisionProposalRevisionSnapshot(
                revision,
                BuildComparison(repository, proposal, revision),
                false,
                "Historical revision is read-only explanatory history; currentProposal remains authoritative."))
            .ToArray();

        return new DecisionProposalLineage(
            repository.Id,
            proposal.Id,
            proposal.State,
            currentFingerprint,
            proposal,
            review,
            BuildLineageEvents(proposal, review, revisions, notes),
            snapshots,
            notes,
            BuildLineageDiagnostics(proposal, review, revisions, notes));
    }

    public async Task<DecisionProposalRevisionComparison> GetProposalRevisionComparisonAsync(
        Guid repositoryId,
        string proposalId,
        string revisionId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        DecisionProposal proposal = await GetProposalAsync(repository, proposalId);
        DecisionProposalRevision? revision = (await decisionRepository.ListProposalRevisionsAsync(repository, proposalId))
            .FirstOrDefault(item => string.Equals(item.Id, revisionId, StringComparison.Ordinal));
        if (revision is null)
        {
            throw new KeyNotFoundException($"Decision proposal revision was not found: {revisionId}");
        }

        return BuildComparison(repository, proposal, revision);
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

    private static IReadOnlyList<DecisionProposalLineageEvent> BuildLineageEvents(
        DecisionProposal proposal,
        DecisionReviewStatus review,
        IReadOnlyList<DecisionProposalRevision> revisions,
        IReadOnlyList<DecisionReviewNote> notes)
    {
        var events = new List<DecisionProposalLineageEvent>();
        events.AddRange(proposal.History.Select(entry => new DecisionProposalLineageEvent(
            entry.Timestamp,
            "ProposalHistory",
            proposal.Id,
            entry.Event,
            entry.FromState,
            entry.ToState,
            entry.Sources)));

        events.AddRange(revisions.Select(revision => new DecisionProposalLineageEvent(
            revision.CreatedAt,
            "Revision",
            revision.Id,
            revision.Reason,
            null,
            DecisionProposalState.Refined.ToString(),
            revision.Sources)));

        events.AddRange(notes.Select(note => new DecisionProposalLineageEvent(
            note.CreatedAt,
            "ReviewNote",
            note.Id,
            note.Body,
            null,
            null,
            note.Sources)));

        if (review.UpdatedAt != DateTimeOffset.MinValue)
        {
            events.Add(new DecisionProposalLineageEvent(
                review.UpdatedAt,
                "ReviewState",
                proposal.Id,
                review.Reason ?? $"Review state is {review.State}.",
                null,
                review.State.ToString(),
                review.Sources));
        }

        return events
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Kind, StringComparer.Ordinal)
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildLineageDiagnostics(
        DecisionProposal proposal,
        DecisionReviewStatus review,
        IReadOnlyList<DecisionProposalRevision> revisions,
        IReadOnlyList<DecisionReviewNote> notes)
    {
        var diagnostics = new List<string>
        {
            "Current proposal is authoritative; revision snapshots are historical and read-only.",
            "Revision comparisons explain evolution and do not resolve decisions."
        };

        if (revisions.Count == 0)
        {
            diagnostics.Add("No proposal revisions have been recorded.");
        }

        if (notes.Count == 0)
        {
            diagnostics.Add("No review notes have been recorded.");
        }

        if (review.State == DecisionReviewState.NotStarted)
        {
            diagnostics.Add("Review has not started for this proposal.");
        }

        if (proposal.State == DecisionProposalState.Resolved)
        {
            diagnostics.Add("Proposal is resolved; lineage remains explanatory rather than decision authority.");
        }

        return diagnostics.ToArray();
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
        AddIfPresent("PriorityAdjustments", request.PriorityAdjustments);
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

        if (request.PriorityAdjustments is { Count: > 0 })
        {
            diagnostics.Add("Request included explicit priority adjustment metadata without inferring proposal authority.");
        }

        return diagnostics.ToArray();
    }

    private static IReadOnlyList<DecisionRevisionFieldComparison> BuildFieldComparisons(DecisionProposalRevision revision)
    {
        var comparisons = new List<DecisionRevisionFieldComparison>();
        Add("Context", revision.PreviousContext, revision.RevisedContext);
        Add("Options", SummarizeOptions(revision.PreviousOptions ?? []), SummarizeOptions(revision.RevisedOptions ?? []));
        Add("Tradeoffs", SummarizeTradeoffs(revision.PreviousTradeoffs ?? []), SummarizeTradeoffs(revision.RevisedTradeoffs ?? []));
        Add(
            "Recommendation",
            revision.PreviousRecommendationRationale,
            revision.RevisedRecommendationRationale);
        Add(
            "Assumptions",
            SummarizeAssumptions(revision.PreviousAssumptions ?? []),
            SummarizeAssumptions(revision.RevisedAssumptions ?? []));

        foreach (string field in revision.ChangedFields.Order(StringComparer.Ordinal))
        {
            if (comparisons.All(comparison => !string.Equals(comparison.Field, field, StringComparison.Ordinal)))
            {
                comparisons.Add(new DecisionRevisionFieldComparison(field, "Metadata", null, null));
            }
        }

        return comparisons
            .OrderBy(comparison => comparison.Field, StringComparer.Ordinal)
            .ToArray();

        void Add(string field, string? previous, string? revised)
        {
            if (!revision.ChangedFields.Contains(field, StringComparer.Ordinal) &&
                string.Equals(previous, revised, StringComparison.Ordinal))
            {
                return;
            }

            comparisons.Add(new DecisionRevisionFieldComparison(
                field,
                ChangeType(previous, revised),
                previous,
                revised));
        }
    }

    private static DecisionProposalRevisionComparison BuildComparison(
        Repository repository,
        DecisionProposal proposal,
        DecisionProposalRevision revision)
    {
        string currentFingerprint = Fingerprint(proposal);
        return new DecisionProposalRevisionComparison(
            proposal.Id,
            revision.Id,
            repository.Id,
            revision.SourceProposalFingerprint,
            currentFingerprint,
            string.Equals(revision.SourceProposalFingerprint, currentFingerprint, StringComparison.Ordinal),
            revision.ChangedFields,
            BuildFieldComparisons(revision),
            revision.AcceptedChanges ?? [],
            revision.RejectedChanges ?? [],
            revision.Diagnostics ?? [],
            revision.Constraints ?? [],
            revision.PreviousOptions ?? [],
            revision.RevisedOptions ?? proposal.Options,
            revision.RetiredOptions ?? [],
            revision.PreviousAssumptions ?? [],
            revision.RevisedAssumptions ?? proposal.Assumptions,
            revision.RetiredAssumptions ?? [],
            revision.PreviousTradeoffs ?? [],
            revision.RevisedTradeoffs ?? proposal.Tradeoffs,
            revision.AssumptionRevisions ?? [],
            revision.OptionRevisions ?? [],
            revision.TradeoffRevisions ?? [],
            revision.PriorityAdjustments ?? [],
            revision.Sources);
    }

    private static string ChangeType(string? previous, string? revised)
    {
        if (string.IsNullOrWhiteSpace(previous) && !string.IsNullOrWhiteSpace(revised))
        {
            return "Added";
        }

        if (!string.IsNullOrWhiteSpace(previous) && string.IsNullOrWhiteSpace(revised))
        {
            return "Removed";
        }

        return string.Equals(previous, revised, StringComparison.Ordinal) ? "Unchanged" : "Changed";
    }

    private static string SummarizeOptions(IReadOnlyList<DecisionOption> options)
    {
        return string.Join(
            "\n",
            options
                .OrderBy(option => option.Id, StringComparer.Ordinal)
                .Select(option => $"{option.Id}: {option.Title} - {option.Description}"));
    }

    private static string SummarizeTradeoffs(IReadOnlyList<DecisionTradeoff> tradeoffs)
    {
        return string.Join(
            "\n",
            tradeoffs
                .OrderBy(tradeoff => tradeoff.OptionId, StringComparer.Ordinal)
                .ThenBy(tradeoff => tradeoff.Benefit, StringComparer.Ordinal)
                .ThenBy(tradeoff => tradeoff.Cost, StringComparer.Ordinal)
                .Select(tradeoff => $"{tradeoff.OptionId}: benefit {tradeoff.Benefit}; cost {tradeoff.Cost}"));
    }

    private static string SummarizeAssumptions(IReadOnlyList<DecisionAssumption> assumptions)
    {
        return string.Join(
            "\n",
            assumptions
                .OrderBy(assumption => assumption.Id, StringComparer.Ordinal)
                .Select(assumption => $"{assumption.Id}: {assumption.Statement}"));
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

    private static DecisionSourceReference ProposalSource(DecisionProposal proposal)
    {
        return new DecisionSourceReference(
            "DecisionProposal",
            ProposalPath(proposal.Id),
            ProposalId: proposal.Id,
            CandidateId: proposal.CandidateId);
    }
}
