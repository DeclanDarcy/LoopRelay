using System.Text;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Persistence;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionArtifactProjectionService(
    IDecisionRepository decisionRepository,
    IArtifactStore artifactStore) : IDecisionArtifactProjectionService
{
    public async Task ProjectDecisionAsync(Repository repository, Decision decision)
    {
        string id = DecisionArtifactPaths.ValidateId(decision.Id.Value, "DEC");
        await WriteAsync(repository, DecisionArtifactPaths.DecisionMarkdown(id), RenderDecision(decision));
    }

    public async Task ProjectCandidateAsync(Repository repository, DecisionCandidate candidate)
    {
        string id = DecisionArtifactPaths.ValidateId(candidate.Id, "CAND");
        await WriteAsync(repository, DecisionArtifactPaths.CandidateMarkdown(id), RenderCandidate(candidate));
    }

    public async Task ProjectProposalAsync(Repository repository, DecisionProposal proposal)
    {
        string id = DecisionArtifactPaths.ValidateId(proposal.Id, "PROP");
        await WriteAsync(repository, DecisionArtifactPaths.ProposalMarkdown(id), RenderProposal(proposal));
    }

    public async Task ProjectProposalRevisionAsync(Repository repository, DecisionProposalRevision revision)
    {
        string proposalId = DecisionArtifactPaths.ValidateId(revision.ProposalId, "PROP");
        string revisionId = DecisionArtifactPaths.ValidateId(revision.Id, "REV");
        await WriteAsync(repository, DecisionArtifactPaths.ProposalRevisionMarkdown(proposalId, revisionId), RenderProposalRevision(revision));
    }

    public async Task ProjectProposalRevisionComparisonAsync(
        Repository repository,
        DecisionProposalRevisionComparison comparison)
    {
        string proposalId = DecisionArtifactPaths.ValidateId(comparison.ProposalId, "PROP");
        string revisionId = DecisionArtifactPaths.ValidateId(comparison.RevisionId, "REV");
        await WriteAsync(
            repository,
            DecisionArtifactPaths.ProposalRevisionComparisonMarkdown(proposalId, revisionId),
            RenderProposalRevisionComparison(comparison));
    }

    public async Task RefreshDecisionIndexAsync(Repository repository)
    {
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
        IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);
        await WriteAsync(repository, DecisionArtifactPaths.DecisionsIndex(), RenderIndex(decisions, candidates, proposals));
    }

    public async Task RefreshAllAsync(Repository repository)
    {
        foreach (Decision decision in await decisionRepository.ListDecisionsAsync(repository))
        {
            await ProjectDecisionAsync(repository, decision);
        }

        foreach (DecisionCandidate candidate in await decisionRepository.ListCandidatesAsync(repository))
        {
            await ProjectCandidateAsync(repository, candidate);
        }

        foreach (DecisionProposal proposal in await decisionRepository.ListProposalsAsync(repository))
        {
            await ProjectProposalAsync(repository, proposal);
        }

        await RefreshDecisionIndexAsync(repository);
    }

    public async Task RecoverMissingProjectionsAsync(Repository repository)
    {
        foreach (Decision decision in await decisionRepository.ListDecisionsAsync(repository))
        {
            string id = DecisionArtifactPaths.ValidateId(decision.Id.Value, "DEC");
            await ProjectIfMissingAsync(repository, DecisionArtifactPaths.DecisionMarkdown(id), () => RenderDecision(decision));
        }

        foreach (DecisionCandidate candidate in await decisionRepository.ListCandidatesAsync(repository))
        {
            string id = DecisionArtifactPaths.ValidateId(candidate.Id, "CAND");
            await ProjectIfMissingAsync(repository, DecisionArtifactPaths.CandidateMarkdown(id), () => RenderCandidate(candidate));
        }

        foreach (DecisionProposal proposal in await decisionRepository.ListProposalsAsync(repository))
        {
            string id = DecisionArtifactPaths.ValidateId(proposal.Id, "PROP");
            await ProjectIfMissingAsync(repository, DecisionArtifactPaths.ProposalMarkdown(id), () => RenderProposal(proposal));
        }

        await ProjectIfMissingAsync(
            repository,
            DecisionArtifactPaths.DecisionsIndex(),
            async () =>
            {
                IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
                IReadOnlyList<DecisionCandidate> candidates = await decisionRepository.ListCandidatesAsync(repository);
                IReadOnlyList<DecisionProposal> proposals = await decisionRepository.ListProposalsAsync(repository);
                return RenderIndex(decisions, candidates, proposals);
            });
    }

    private Task WriteAsync(Repository repository, string relativePath, string content)
    {
        return artifactStore.WriteAsync(DecisionArtifactPaths.Resolve(repository, relativePath), content);
    }

    private async Task ProjectIfMissingAsync(Repository repository, string relativePath, Func<string> render)
    {
        string path = DecisionArtifactPaths.Resolve(repository, relativePath);
        if (!await artifactStore.ExistsAsync(path))
        {
            await artifactStore.WriteAsync(path, render());
        }
    }

    private async Task ProjectIfMissingAsync(Repository repository, string relativePath, Func<Task<string>> render)
    {
        string path = DecisionArtifactPaths.Resolve(repository, relativePath);
        if (!await artifactStore.ExistsAsync(path))
        {
            await artifactStore.WriteAsync(path, await render());
        }
    }

    private static string RenderDecision(Decision decision)
    {
        var markdown = new MarkdownProjectionBuilder();
        markdown.H1($"{decision.Id.Value}: {decision.Title}");
        markdown.Fields(
            ("State", decision.State.ToString()),
            ("Classification", decision.Classification.ToString()),
            ("Repository", decision.Metadata.RepositoryId.ToString()),
            ("Created", FormatTimestamp(decision.Metadata.CreatedAt)),
            ("Updated", FormatTimestamp(decision.Metadata.UpdatedAt)));
        markdown.H2("Context");
        markdown.Paragraph(decision.Context);
        markdown.H2("Resolution");
        if (decision.Resolution is null)
        {
            markdown.Paragraph("Unresolved.");
        }
        else
        {
            markdown.Fields(
                ("Outcome", decision.Resolution.Outcome.ToString()),
                ("Selected option", decision.Resolution.SelectedOptionId),
                ("Resolved by", decision.Resolution.ResolvedBy),
                ("Recommendation diverged", decision.Resolution.RecommendationDiverged.ToString()),
                ("Resolved", FormatTimestamp(decision.Resolution.ResolvedAt)));
            markdown.Paragraph(decision.Resolution.Rationale);
            markdown.H3("Sources");
            markdown.SourceList(decision.Resolution.Sources);
        }

        markdown.H2("Relationships");
        markdown.RelationshipList(decision.Relationships);
        markdown.H2("Evidence");
        markdown.EvidenceList(decision.Evidence);
        markdown.H2("History");
        markdown.HistoryList(decision.History);
        return markdown.ToString();
    }

    private static string RenderCandidate(DecisionCandidate candidate)
    {
        var markdown = new MarkdownProjectionBuilder();
        markdown.H1($"{candidate.Id}: {candidate.Title}");
        markdown.Fields(
            ("State", candidate.State.ToString()),
            ("Priority", candidate.Priority.ToString()),
            ("Classification", candidate.Classification.ToString()),
            ("Repository", candidate.RepositoryId.ToString()),
            ("Source fingerprint", candidate.SourceFingerprint));
        markdown.H2("Summary");
        markdown.Paragraph(candidate.Summary);
        markdown.H2("Signals");
        foreach (DecisionSignal signal in candidate.Signals
            .OrderBy(signal => signal.Kind, StringComparer.Ordinal)
            .ThenBy(signal => signal.Summary, StringComparer.Ordinal))
        {
            markdown.Bullet($"{signal.Kind} | {signal.Classification} | {signal.Priority} | {signal.Summary}");
            markdown.NestedEvidenceList(signal.Evidence);
        }

        markdown.EmptyListIf(candidate.Signals.Count == 0);
        markdown.H2("Evidence");
        markdown.EvidenceList(candidate.Evidence);
        markdown.H2("Sources");
        markdown.SourceList(candidate.Sources);
        markdown.H2("Diagnostics");
        foreach (string diagnostic in candidate.Diagnostics.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(diagnostic);
        }

        markdown.EmptyListIf(candidate.Diagnostics.Count == 0);
        markdown.H2("History");
        markdown.HistoryList(candidate.History);
        return markdown.ToString();
    }

    private static string RenderProposal(DecisionProposal proposal)
    {
        var markdown = new MarkdownProjectionBuilder();
        markdown.H1($"{proposal.Id}: {proposal.Title}");
        markdown.Fields(
            ("State", proposal.State.ToString()),
            ("Candidate", proposal.CandidateId),
            ("Repository", proposal.RepositoryId.ToString()));
        markdown.H2("Context");
        markdown.Paragraph(proposal.Context);
        markdown.H2("Options");
        foreach (DecisionOption option in proposal.Options.OrderBy(option => option.Id, StringComparer.Ordinal))
        {
            markdown.H3($"{option.Id}: {option.Title}");
            markdown.Paragraph(option.Description);
            markdown.H4("Evidence");
            markdown.EvidenceList(option.Evidence);
        }

        markdown.H2("Tradeoffs");
        foreach (DecisionTradeoff tradeoff in proposal.Tradeoffs
            .OrderBy(tradeoff => tradeoff.OptionId, StringComparer.Ordinal)
            .ThenBy(tradeoff => tradeoff.Benefit, StringComparer.Ordinal)
            .ThenBy(tradeoff => tradeoff.Cost, StringComparer.Ordinal))
        {
            markdown.Bullet($"Option {tradeoff.OptionId}: benefit {tradeoff.Benefit}; cost {tradeoff.Cost}");
            markdown.NestedEvidenceList(tradeoff.Evidence);
        }

        markdown.H2("Recommendation");
        if (proposal.Recommendation is null)
        {
            markdown.Paragraph("No recommendation.");
        }
        else
        {
            markdown.Fields(("Option", proposal.Recommendation.OptionId));
            markdown.Paragraph(proposal.Recommendation.Rationale);
            markdown.H3("Evidence");
            markdown.EvidenceList(proposal.Recommendation.Evidence);
        }

        markdown.H2("Assumptions");
        foreach (DecisionAssumption assumption in proposal.Assumptions.OrderBy(assumption => assumption.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{assumption.Id}: {assumption.Statement}");
            markdown.NestedEvidenceList(assumption.Evidence);
        }

        markdown.H2("Evidence");
        markdown.EvidenceList(proposal.Evidence);
        markdown.H2("History");
        markdown.HistoryList(proposal.History);
        return markdown.ToString();
    }

    private static string RenderProposalRevision(DecisionProposalRevision revision)
    {
        var markdown = new MarkdownProjectionBuilder();
        markdown.H1($"{revision.Id}: {revision.ProposalId}");
        markdown.Fields(
            ("Proposal", revision.ProposalId),
            ("Repository", revision.RepositoryId.ToString()),
            ("Created", FormatTimestamp(revision.CreatedAt)),
            ("Source proposal fingerprint", revision.SourceProposalFingerprint),
            ("Requested by", revision.RequestedBy ?? "Unspecified"));
        markdown.H2("Reason");
        markdown.Paragraph(revision.Reason);
        markdown.H2("Changed Fields");
        foreach (string field in revision.ChangedFields.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(field);
        }

        markdown.EmptyListIf(revision.ChangedFields.Count == 0);
        markdown.H2("Accepted Changes");
        foreach (string change in (revision.AcceptedChanges ?? []).Order(StringComparer.Ordinal))
        {
            markdown.Bullet(change);
        }

        markdown.EmptyListIf((revision.AcceptedChanges ?? []).Count == 0);
        markdown.H2("Rejected Changes");
        foreach (string change in (revision.RejectedChanges ?? []).Order(StringComparer.Ordinal))
        {
            markdown.Bullet(change);
        }

        markdown.EmptyListIf((revision.RejectedChanges ?? []).Count == 0);
        markdown.H2("Constraints");
        foreach (DecisionConstraint constraint in (revision.Constraints ?? []).OrderBy(constraint => constraint.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{constraint.Id}: {constraint.Statement}");
            markdown.NestedEvidenceList(constraint.Evidence);
        }

        markdown.EmptyListIf((revision.Constraints ?? []).Count == 0);
        markdown.H2("Retired Options");
        foreach (DecisionOption option in (revision.RetiredOptions ?? []).OrderBy(option => option.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{option.Id}: {option.Title} - {option.Description}");
        }

        markdown.EmptyListIf((revision.RetiredOptions ?? []).Count == 0);
        markdown.H2("Retired Assumptions");
        foreach (DecisionAssumption assumption in (revision.RetiredAssumptions ?? []).OrderBy(assumption => assumption.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{assumption.Id}: {assumption.Statement}");
        }

        markdown.EmptyListIf((revision.RetiredAssumptions ?? []).Count == 0);
        markdown.H2("Assumption Revisions");
        foreach (DecisionAssumptionRevision assumptionRevision in (revision.AssumptionRevisions ?? [])
            .OrderBy(item => item.AssumptionId, StringComparer.Ordinal)
            .ThenBy(item => item.ChangeType, StringComparer.Ordinal))
        {
            markdown.Bullet($"{assumptionRevision.AssumptionId} | {assumptionRevision.ChangeType} | {assumptionRevision.Reason}");
        }

        markdown.EmptyListIf((revision.AssumptionRevisions ?? []).Count == 0);
        markdown.H2("Option Revisions");
        foreach (DecisionOptionRevision optionRevision in (revision.OptionRevisions ?? [])
            .OrderBy(item => item.OptionId, StringComparer.Ordinal)
            .ThenBy(item => item.ChangeType, StringComparer.Ordinal))
        {
            markdown.Bullet($"{optionRevision.OptionId} | {optionRevision.ChangeType} | {optionRevision.Reason}");
        }

        markdown.EmptyListIf((revision.OptionRevisions ?? []).Count == 0);
        markdown.H2("Tradeoff Revisions");
        foreach (DecisionTradeoffRevision tradeoffRevision in (revision.TradeoffRevisions ?? [])
            .OrderBy(item => item.OptionId, StringComparer.Ordinal)
            .ThenBy(item => item.ChangeType, StringComparer.Ordinal))
        {
            markdown.Bullet($"{tradeoffRevision.OptionId} | {tradeoffRevision.ChangeType} | {tradeoffRevision.Reason}");
        }

        markdown.EmptyListIf((revision.TradeoffRevisions ?? []).Count == 0);
        markdown.H2("Recommendation Rationale");
        markdown.Fields(
            ("Previous", revision.PreviousRecommendationRationale ?? "None."),
            ("Revised", revision.RevisedRecommendationRationale ?? "None."));
        markdown.H2("Context");
        markdown.Fields(
            ("Previous", revision.PreviousContext ?? "None."),
            ("Revised", revision.RevisedContext ?? "None."));
        markdown.H2("Diagnostics");
        foreach (string diagnostic in (revision.Diagnostics ?? []).Order(StringComparer.Ordinal))
        {
            markdown.Bullet(diagnostic);
        }

        markdown.EmptyListIf((revision.Diagnostics ?? []).Count == 0);
        markdown.H2("Sources");
        markdown.SourceList(revision.Sources);
        return markdown.ToString();
    }

    private static string RenderProposalRevisionComparison(DecisionProposalRevisionComparison comparison)
    {
        var markdown = new MarkdownProjectionBuilder();
        markdown.H1($"{comparison.RevisionId}: {comparison.ProposalId} Comparison");
        markdown.Fields(
            ("Proposal", comparison.ProposalId),
            ("Repository", comparison.RepositoryId.ToString()),
            ("Source proposal fingerprint", comparison.SourceProposalFingerprint),
            ("Current proposal fingerprint", comparison.CurrentProposalFingerprint),
            ("Source matches current proposal", comparison.SourceMatchesCurrentProposal.ToString()));
        markdown.H2("Changed Fields");
        foreach (DecisionRevisionFieldComparison field in comparison.FieldComparisons
            .OrderBy(field => field.Field, StringComparer.Ordinal))
        {
            markdown.H3($"{field.Field}: {field.ChangeType}");
            markdown.Fields(
                ("Previous", field.PreviousValue ?? "None."),
                ("Revised", field.RevisedValue ?? "None."));
        }

        markdown.EmptyListIf(comparison.FieldComparisons.Count == 0);
        markdown.H2("Accepted Changes");
        foreach (string change in comparison.AcceptedChanges.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(change);
        }

        markdown.EmptyListIf(comparison.AcceptedChanges.Count == 0);
        markdown.H2("Rejected Changes");
        foreach (string change in comparison.RejectedChanges.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(change);
        }

        markdown.EmptyListIf(comparison.RejectedChanges.Count == 0);
        markdown.H2("Retired Options");
        foreach (DecisionOption option in comparison.RetiredOptions.OrderBy(option => option.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{option.Id}: {option.Title} - {option.Description}");
        }

        markdown.EmptyListIf(comparison.RetiredOptions.Count == 0);
        markdown.H2("Retired Assumptions");
        foreach (DecisionAssumption assumption in comparison.RetiredAssumptions.OrderBy(assumption => assumption.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{assumption.Id}: {assumption.Statement}");
        }

        markdown.EmptyListIf(comparison.RetiredAssumptions.Count == 0);
        markdown.H2("Diagnostics");
        foreach (string diagnostic in comparison.Diagnostics.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(diagnostic);
        }

        markdown.EmptyListIf(comparison.Diagnostics.Count == 0);
        markdown.H2("Sources");
        markdown.SourceList(comparison.Sources);
        return markdown.ToString();
    }

    private static string RenderIndex(
        IReadOnlyList<Decision> decisions,
        IReadOnlyList<DecisionCandidate> candidates,
        IReadOnlyList<DecisionProposal> proposals)
    {
        var markdown = new MarkdownProjectionBuilder();
        markdown.H1("Decisions");
        markdown.Paragraph("Generated from structured decision lifecycle artifacts. Structured JSON remains authoritative.");
        markdown.H2("Decision Records");
        foreach (Decision decision in decisions.OrderBy(decision => decision.Id.Value, StringComparer.Ordinal))
        {
            string outcome = decision.Resolution is null ? "Unresolved" : decision.Resolution.Outcome.ToString();
            markdown.Bullet($"{decision.Id.Value} | {decision.State} | {decision.Classification} | {outcome} | {decision.Title}");
        }

        markdown.EmptyListIf(decisions.Count == 0);
        markdown.H2("Candidates");
        foreach (DecisionCandidate candidate in candidates.OrderBy(candidate => candidate.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{candidate.Id} | {candidate.State} | {candidate.Priority} | {candidate.Classification} | {candidate.Title}");
        }

        markdown.EmptyListIf(candidates.Count == 0);
        markdown.H2("Proposals");
        foreach (DecisionProposal proposal in proposals.OrderBy(proposal => proposal.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{proposal.Id} | {proposal.State} | {proposal.CandidateId} | {proposal.Title}");
        }

        markdown.EmptyListIf(proposals.Count == 0);
        return markdown.ToString();
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToUniversalTime().ToString("O");
    }

    private sealed class MarkdownProjectionBuilder
    {
        private readonly StringBuilder builder = new();

        public void H1(string text)
        {
            AppendLine($"# {text}");
            AppendLine();
        }

        public void H2(string text)
        {
            AppendLine($"## {text}");
            AppendLine();
        }

        public void H3(string text)
        {
            AppendLine($"### {text}");
            AppendLine();
        }

        public void H4(string text)
        {
            AppendLine($"#### {text}");
            AppendLine();
        }

        public void Paragraph(string? text)
        {
            AppendLine(string.IsNullOrWhiteSpace(text) ? "None." : text.Trim());
            AppendLine();
        }

        public void Fields(params (string Label, string Value)[] fields)
        {
            foreach ((string label, string value) in fields)
            {
                AppendLine($"- {label}: {value}");
            }

            AppendLine();
        }

        public void Bullet(string text)
        {
            AppendLine($"- {text}");
        }

        public void EmptyListIf(bool condition)
        {
            if (condition)
            {
                Bullet("None.");
            }

            AppendLine();
        }

        public void RelationshipList(IReadOnlyList<DecisionRelationship> relationships)
        {
            foreach (DecisionRelationship relationship in relationships
                .OrderBy(relationship => relationship.SourceDecisionId.Value, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.TargetDecisionId.Value, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.Type.ToString(), StringComparer.Ordinal)
                .ThenBy(relationship => relationship.Rationale ?? string.Empty, StringComparer.Ordinal))
            {
                string rationale = string.IsNullOrWhiteSpace(relationship.Rationale)
                    ? string.Empty
                    : $" - {relationship.Rationale}";
                Bullet($"{relationship.SourceDecisionId.Value} {relationship.Type} {relationship.TargetDecisionId.Value}{rationale}");
            }

            EmptyListIf(relationships.Count == 0);
        }

        public void EvidenceList(IReadOnlyList<DecisionEvidence> evidence)
        {
            foreach (DecisionEvidence item in evidence.OrderBy(item => item.Summary, StringComparer.Ordinal))
            {
                Bullet(item.Summary);
                NestedSourceList(item.Sources);
            }

            EmptyListIf(evidence.Count == 0);
        }

        public void NestedEvidenceList(IReadOnlyList<DecisionEvidence> evidence)
        {
            foreach (DecisionEvidence item in evidence.OrderBy(item => item.Summary, StringComparer.Ordinal))
            {
                AppendLine($"  - Evidence: {item.Summary}");
                NestedSourceList(item.Sources, "    ");
            }
        }

        public void SourceList(IReadOnlyList<DecisionSourceReference> sources)
        {
            foreach (DecisionSourceReference source in SortSources(sources))
            {
                Bullet(FormatSource(source));
            }

            EmptyListIf(sources.Count == 0);
        }

        public void HistoryList(IReadOnlyList<DecisionHistoryEntry> history)
        {
            foreach (DecisionHistoryEntry entry in history
                .OrderBy(entry => entry.Timestamp)
                .ThenBy(entry => entry.Event, StringComparer.Ordinal)
                .ThenBy(entry => entry.ToState, StringComparer.Ordinal))
            {
                string fromState = string.IsNullOrWhiteSpace(entry.FromState) ? "None" : entry.FromState;
                string reason = string.IsNullOrWhiteSpace(entry.Reason) ? string.Empty : $" - {entry.Reason}";
                Bullet($"{FormatTimestamp(entry.Timestamp)} | {entry.Event} | {fromState} -> {entry.ToState}{reason}");
                NestedSourceList(entry.Sources);
            }

            EmptyListIf(history.Count == 0);
        }

        public override string ToString()
        {
            return builder.ToString();
        }

        private void NestedSourceList(IReadOnlyList<DecisionSourceReference> sources, string indent = "  ")
        {
            foreach (DecisionSourceReference source in SortSources(sources))
            {
                AppendLine($"{indent}- Source: {FormatSource(source)}");
            }
        }

        private static IEnumerable<DecisionSourceReference> SortSources(IReadOnlyList<DecisionSourceReference> sources)
        {
            return sources
                .OrderBy(source => source.SourceKind, StringComparer.Ordinal)
                .ThenBy(source => source.RelativePath ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(source => source.Section ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(source => source.ItemId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(source => source.DecisionId?.Value ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(source => source.ProposalId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(source => source.CandidateId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(source => source.Excerpt ?? string.Empty, StringComparer.Ordinal);
        }

        private static string FormatSource(DecisionSourceReference source)
        {
            var parts = new List<string> { source.SourceKind };
            Add("path", source.RelativePath);
            Add("section", source.Section);
            Add("item", source.ItemId);
            Add("decision", source.DecisionId?.Value);
            Add("proposal", source.ProposalId);
            Add("candidate", source.CandidateId);
            Add("excerpt", source.Excerpt);
            return string.Join("; ", parts);

            void Add(string label, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parts.Add($"{label}: {value}");
                }
            }
        }

        private void AppendLine(string text = "")
        {
            builder.Append(text);
            builder.Append('\n');
        }
    }
}
