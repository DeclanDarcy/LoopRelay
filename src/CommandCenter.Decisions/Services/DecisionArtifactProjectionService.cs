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

    public async Task ProjectPackageVersionAsync(Repository repository, DecisionPackageVersion packageVersion)
    {
        string proposalId = DecisionArtifactPaths.ValidateId(packageVersion.ProposalId, "PROP");
        string packageId = DecisionArtifactPaths.ValidateId(packageVersion.Id, "PKG");
        await WriteAsync(
            repository,
            DecisionArtifactPaths.ProposalPackageMarkdown(proposalId, packageId),
            RenderPackageVersion(packageVersion));
    }

    public async Task ProjectPackageComparisonAsync(
        Repository repository,
        DecisionPackageComparison comparison)
    {
        string proposalId = DecisionArtifactPaths.ValidateId(comparison.ProposalId, "PROP");
        string leftPackageId = DecisionArtifactPaths.ValidateId(comparison.LeftPackageId, "PKG");
        string rightPackageId = DecisionArtifactPaths.ValidateId(comparison.RightPackageId, "PKG");
        await WriteAsync(
            repository,
            DecisionArtifactPaths.ProposalPackageComparisonMarkdown(proposalId, leftPackageId, rightPackageId),
            RenderPackageComparison(comparison));
    }

    public async Task ProjectDecisionAssimilationRecommendationAsync(
        Repository repository,
        DecisionAssimilationRecommendation recommendation)
    {
        string id = DecisionArtifactPaths.ValidateId(recommendation.DecisionId, "DEC");
        await WriteAsync(
            repository,
            DecisionArtifactPaths.AssimilationRecommendationMarkdown(id),
            RenderAssimilationRecommendation(recommendation));
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
            foreach (DecisionPackageVersion packageVersion in await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id))
            {
                await ProjectPackageVersionAsync(repository, packageVersion);
            }
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
            foreach (DecisionPackageVersion packageVersion in await decisionRepository.ListPackageVersionsAsync(repository, id))
            {
                await ProjectIfMissingAsync(
                    repository,
                    DecisionArtifactPaths.ProposalPackageMarkdown(id, packageVersion.Id),
                    () => RenderPackageVersion(packageVersion));
            }
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
            if (decision.Resolution.SourceProposalSnapshot is not null)
            {
                DecisionResolvedProposalSnapshot snapshot = decision.Resolution.SourceProposalSnapshot;
                markdown.Fields(
                    ("Source proposal", snapshot.ProposalId),
                    ("Source candidate", snapshot.CandidateId),
                    ("Source proposal state", snapshot.ProposalState.ToString()),
                    ("Source proposal fingerprint", snapshot.ProposalFingerprint),
                    ("Source package", snapshot.PackageId ?? "None"),
                    ("Source package fingerprint", snapshot.PackageFingerprint ?? "None"),
                    ("Source package created", snapshot.PackageVersionCreatedAt.HasValue ? FormatTimestamp(snapshot.PackageVersionCreatedAt.Value) : "None"),
                    ("Authority resolved", snapshot.AuthorityResolvedAt.HasValue ? FormatTimestamp(snapshot.AuthorityResolvedAt.Value) : "None"),
                    ("Captured revisions", snapshot.Revisions.Count.ToString()));
            }

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
            markdown.Fields(("Type", option.Type.ToString()));
            markdown.Paragraph(option.Description);
            markdown.H4("Assumptions");
            foreach (string assumption in option.Assumptions.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(assumption);
            }

            markdown.EmptyListIf(option.Assumptions.Count == 0);
            markdown.H4("Dependencies");
            foreach (string dependency in option.Dependencies.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(dependency);
            }

            markdown.EmptyListIf(option.Dependencies.Count == 0);
            markdown.H4("Diagnostics");
            foreach (string diagnostic in option.Diagnostics.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(diagnostic);
            }

            markdown.EmptyListIf(option.Diagnostics.Count == 0);
            markdown.H4("Evidence");
            markdown.EvidenceList(option.Evidence);
        }

        markdown.H2("Option Relationships");
        foreach (DecisionOptionRelationship relationship in proposal.OptionRelationships
            .OrderBy(relationship => relationship.SourceOptionId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetOptionId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.Type.ToString(), StringComparer.Ordinal)
            .ThenBy(relationship => relationship.Rationale, StringComparer.Ordinal))
        {
            markdown.Bullet($"{relationship.SourceOptionId} {relationship.Type} {relationship.TargetOptionId}: {relationship.Rationale}");
            markdown.NestedEvidenceList(relationship.Evidence);
        }

        markdown.EmptyListIf(proposal.OptionRelationships.Count == 0);
        markdown.H2("Tradeoffs");
        foreach (DecisionTradeoff tradeoff in proposal.Tradeoffs
            .OrderBy(tradeoff => tradeoff.OptionId, StringComparer.Ordinal)
            .ThenBy(tradeoff => tradeoff.Benefit, StringComparer.Ordinal)
            .ThenBy(tradeoff => tradeoff.Cost, StringComparer.Ordinal))
        {
            markdown.Bullet($"Option {tradeoff.OptionId}: benefit {tradeoff.Benefit}; cost {tradeoff.Cost}");
            markdown.NestedEvidenceList(tradeoff.Evidence);
        }

        markdown.H2("Structured Tradeoff Analysis");
        foreach (AnalyzedDecisionOption analyzedOption in proposal.AnalyzedOptions
            .OrderBy(option => option.OptionId, StringComparer.Ordinal))
        {
            markdown.H3(analyzedOption.OptionId);
            markdown.H4("Benefits");
            foreach (DecisionBenefit benefit in analyzedOption.Benefits
                .OrderBy(benefit => benefit.Statement, StringComparer.Ordinal))
            {
                markdown.Bullet($"{benefit.Impact}: {benefit.Statement}");
                markdown.NestedEvidenceList(benefit.Evidence);
            }

            markdown.EmptyListIf(analyzedOption.Benefits.Count == 0);
            markdown.H4("Costs");
            foreach (DecisionCost cost in analyzedOption.Costs
                .OrderBy(cost => cost.Statement, StringComparer.Ordinal))
            {
                markdown.Bullet($"{cost.Impact}: {cost.Statement}");
                markdown.NestedEvidenceList(cost.Evidence);
            }

            markdown.EmptyListIf(analyzedOption.Costs.Count == 0);
            markdown.H4("Risks");
            foreach (DecisionRisk risk in analyzedOption.Risks
                .OrderByDescending(risk => risk.Severity)
                .ThenBy(risk => risk.Statement, StringComparer.Ordinal))
            {
                string unknown = risk.IsUnknown ? "unknown; " : string.Empty;
                markdown.Bullet($"{risk.Severity}: {unknown}{risk.Statement}");
                markdown.NestedEvidenceList(risk.Evidence);
            }

            markdown.EmptyListIf(analyzedOption.Risks.Count == 0);
            markdown.H4("Dependencies");
            foreach (DecisionDependency dependency in analyzedOption.Dependencies
                .OrderBy(dependency => dependency.Statement, StringComparer.Ordinal))
            {
                markdown.Bullet(dependency.Statement);
                markdown.NestedEvidenceList(dependency.Evidence);
            }

            markdown.EmptyListIf(analyzedOption.Dependencies.Count == 0);
            markdown.H4("Consequences");
            foreach (DecisionConsequence consequence in analyzedOption.Consequences
                .OrderBy(consequence => consequence.Statement, StringComparer.Ordinal))
            {
                markdown.Bullet($"{consequence.Impact}: {consequence.Statement}");
                markdown.NestedEvidenceList(consequence.Evidence);
            }

            markdown.EmptyListIf(analyzedOption.Consequences.Count == 0);
            markdown.H4("Diagnostics");
            foreach (string diagnostic in analyzedOption.Diagnostics.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(diagnostic);
            }

            markdown.EmptyListIf(analyzedOption.Diagnostics.Count == 0);
        }

        markdown.EmptyListIf(proposal.AnalyzedOptions.Count == 0);
        markdown.H2("Tradeoff Comparisons");
        foreach (DecisionTradeoffComparison comparison in proposal.TradeoffComparisons
            .OrderBy(comparison => comparison.OptionId, StringComparer.Ordinal))
        {
            markdown.H3(comparison.OptionId);
            markdown.H4("Relative Strengths");
            foreach (string strength in comparison.RelativeStrengths.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(strength);
            }

            markdown.EmptyListIf(comparison.RelativeStrengths.Count == 0);
            markdown.H4("Relative Weaknesses");
            foreach (string weakness in comparison.RelativeWeaknesses.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(weakness);
            }

            markdown.EmptyListIf(comparison.RelativeWeaknesses.Count == 0);
            markdown.H4("Unique Advantages");
            foreach (string advantage in comparison.UniqueAdvantages.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(advantage);
            }

            markdown.EmptyListIf(comparison.UniqueAdvantages.Count == 0);
            markdown.H4("Unique Risks");
            foreach (string risk in comparison.UniqueRisks.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(risk);
            }

            markdown.EmptyListIf(comparison.UniqueRisks.Count == 0);
            markdown.H4("Disqualifying Constraints");
            foreach (string constraint in comparison.DisqualifyingConstraints.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(constraint);
            }

            markdown.EmptyListIf(comparison.DisqualifyingConstraints.Count == 0);
            markdown.H4("Evidence");
            markdown.EvidenceList(comparison.Evidence);
        }

        markdown.EmptyListIf(proposal.TradeoffComparisons.Count == 0);
        markdown.H2("Recommendation");
        if (proposal.Recommendation is null)
        {
            markdown.Paragraph("No recommendation.");
        }
        else
        {
            markdown.Fields(
                ("Mode", proposal.Recommendation.Mode.ToString()),
                ("Option", string.IsNullOrWhiteSpace(proposal.Recommendation.OptionId)
                    ? "None"
                    : proposal.Recommendation.OptionId),
                ("Summary", string.IsNullOrWhiteSpace(proposal.Recommendation.Summary)
                    ? "None."
                    : proposal.Recommendation.Summary));
            markdown.Paragraph(proposal.Recommendation.Rationale);
            markdown.H3("Supporting Factors");
            foreach (string factor in proposal.Recommendation.SupportingFactors.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(factor);
            }

            markdown.EmptyListIf(proposal.Recommendation.SupportingFactors.Count == 0);
            markdown.H3("Concerns");
            foreach (string concern in proposal.Recommendation.Concerns.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(concern);
            }

            markdown.EmptyListIf(proposal.Recommendation.Concerns.Count == 0);
            markdown.H3("Assumptions");
            foreach (string assumption in proposal.Recommendation.Assumptions.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(assumption);
            }

            markdown.EmptyListIf(proposal.Recommendation.Assumptions.Count == 0);
            markdown.H3("Alternative Explanations");
            foreach (string explanation in proposal.Recommendation.AlternativeExplanations.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(explanation);
            }

            markdown.EmptyListIf(proposal.Recommendation.AlternativeExplanations.Count == 0);
            markdown.H3("Option Evaluations");
            foreach (OptionEvaluation evaluation in proposal.Recommendation.OptionEvaluations
                .OrderBy(evaluation => evaluation.Rank)
                .ThenBy(evaluation => evaluation.OptionId, StringComparer.Ordinal))
            {
                markdown.H4($"{evaluation.Rank}. {evaluation.OptionId}");
                markdown.Fields(
                    ("Score", evaluation.Score.ToString()),
                    ("Summary", evaluation.Summary),
                    ("Score explanation", evaluation.ScoreExplanation));
                markdown.H4("Constraints");
                foreach (string constraint in evaluation.Constraints.Order(StringComparer.Ordinal))
                {
                    markdown.Bullet(constraint);
                }

                markdown.EmptyListIf(evaluation.Constraints.Count == 0);
            }

            markdown.EmptyListIf(proposal.Recommendation.OptionEvaluations.Count == 0);
            markdown.H3("Recommendation Evidence");
            foreach (RecommendationEvidence item in proposal.Recommendation.RecommendationEvidence
                .OrderBy(item => item.Type.ToString(), StringComparer.Ordinal)
                .ThenBy(item => item.OptionId, StringComparer.Ordinal)
                .ThenBy(item => item.Summary, StringComparer.Ordinal))
            {
                markdown.Bullet($"{item.Type} | {item.OptionId}: {item.Summary}");
                markdown.NestedEvidenceList(item.Evidence);
            }

            markdown.EmptyListIf(proposal.Recommendation.RecommendationEvidence.Count == 0);
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
        markdown.H2("Generation Diagnostics");
        if (proposal.GenerationDiagnostics is null)
        {
            markdown.Bullet("None.");
            markdown.EmptyListIf(false);
        }
        else
        {
            DecisionGenerationDiagnostics diagnostics = proposal.GenerationDiagnostics;
            markdown.Fields(
                ("Generated options", diagnostics.GeneratedOptionCount.ToString()),
                ("Accepted options", diagnostics.AcceptedOptionCount.ToString()),
                ("Rejected options", diagnostics.RejectedOptionCount.ToString()),
                ("Deduplicated options", diagnostics.DeduplicatedOptionCount.ToString()),
                ("Fallback options", diagnostics.FallbackOptionCount.ToString()));
            markdown.H3("Validation Results");
            foreach (DecisionOptionValidationResult validation in diagnostics.OptionValidationResults
                .OrderBy(validation => validation.OptionId, StringComparer.Ordinal))
            {
                markdown.Bullet($"{validation.OptionId}: {(validation.IsValid ? "Valid" : "Invalid")}");
                foreach (DecisionOptionValidationIssue issue in validation.Issues
                    .OrderBy(issue => issue.Type.ToString(), StringComparer.Ordinal)
                    .ThenBy(issue => issue.Message, StringComparer.Ordinal))
                {
                    markdown.Bullet($"{validation.OptionId} | {issue.Type}: {issue.Message}");
                }
            }

            markdown.EmptyListIf(diagnostics.OptionValidationResults.Count == 0);
            markdown.H3("Diagnostics");
            foreach (string diagnostic in diagnostics.Diagnostics.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(diagnostic);
            }

            markdown.EmptyListIf(diagnostics.Diagnostics.Count == 0);
        }

        markdown.H2("Tradeoff Analysis Diagnostics");
        if (proposal.TradeoffAnalysisDiagnostics is null)
        {
            markdown.Bullet("None.");
            markdown.EmptyListIf(false);
        }
        else
        {
            DecisionTradeoffAnalysisDiagnostics diagnostics = proposal.TradeoffAnalysisDiagnostics;
            markdown.Fields(
                ("Analyzed options", diagnostics.AnalyzedOptionCount.ToString()),
                ("Context fingerprint", diagnostics.ContextFingerprint));
            markdown.H3("Unknowns");
            foreach (string unknown in diagnostics.Unknowns.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(unknown);
            }

            markdown.EmptyListIf(diagnostics.Unknowns.Count == 0);
            markdown.H3("Validation Warnings");
            foreach (string warning in diagnostics.ValidationWarnings.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(warning);
            }

            markdown.EmptyListIf(diagnostics.ValidationWarnings.Count == 0);
            markdown.H3("Diagnostics");
            foreach (string diagnostic in diagnostics.Diagnostics.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(diagnostic);
            }

            markdown.EmptyListIf(diagnostics.Diagnostics.Count == 0);
        }

        markdown.H2("History");
        markdown.HistoryList(proposal.History);
        return markdown.ToString();
    }

    private static string RenderPackageVersion(DecisionPackageVersion packageVersion)
    {
        DecisionPackage package = packageVersion.Package;
        var markdown = new MarkdownProjectionBuilder();
        markdown.H1($"{packageVersion.Id}: {package.Title}");
        markdown.Fields(
            ("Package ID", packageVersion.Id),
            ("Proposal ID", package.ProposalId),
            ("Candidate ID", package.CandidateId),
            ("Repository", package.RepositoryId.ToString()),
            ("Generated At", FormatTimestamp(package.GeneratedAt)),
            ("Package Fingerprint", packageVersion.PackageFingerprint),
            ("Context Fingerprint", package.Metadata.ContextFingerprint),
            ("Repository State Fingerprint", package.Metadata.RepositoryStateFingerprint),
            ("Generator Version", package.Metadata.GeneratorVersion),
            ("Source Proposal Fingerprint", package.Metadata.SourceProposalFingerprint),
            ("Milestone", string.IsNullOrWhiteSpace(package.Metadata.MilestonePath)
                ? package.Metadata.MilestoneId
                : package.Metadata.MilestonePath));
        markdown.H2("Decision Summary");
        markdown.Paragraph(package.DecisionSummary);
        markdown.H2("Decision Context");
        RenderContextEntries(markdown, "Goals", package.ContextSummary.Goals);
        RenderContextEntries(markdown, "Constraints", package.ContextSummary.Constraints);
        RenderContextEntries(markdown, "Risks", package.ContextSummary.Risks);
        RenderContextEntries(markdown, "Questions", package.ContextSummary.Questions);
        RenderContextEntries(markdown, "Prior Decisions", package.ContextSummary.PriorDecisions);
        RenderContextEntries(markdown, "Repository State", package.ContextSummary.RepositoryState);
        RenderContextEntries(markdown, "Dependencies", package.ContextSummary.Dependencies);
        RenderContextEntries(markdown, "Handoff State", package.ContextSummary.HandoffState);
        markdown.H3("Context Diagnostics");
        foreach (string diagnostic in package.ContextSummary.Diagnostics.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(diagnostic);
        }

        markdown.EmptyListIf(package.ContextSummary.Diagnostics.Count == 0);
        markdown.H2("Options");
        foreach (DecisionOption option in package.Options.OrderBy(option => option.Id, StringComparer.Ordinal))
        {
            markdown.H3($"{option.Id}: {option.Title}");
            markdown.Fields(("Type", option.Type.ToString()));
            markdown.Paragraph(option.Description);
            markdown.H4("Assumptions");
            foreach (string assumption in option.Assumptions.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(assumption);
            }

            markdown.EmptyListIf(option.Assumptions.Count == 0);
            markdown.H4("Dependencies");
            foreach (string dependency in option.Dependencies.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(dependency);
            }

            markdown.EmptyListIf(option.Dependencies.Count == 0);
            markdown.H4("Evidence");
            markdown.EvidenceList(option.Evidence);
        }

        markdown.EmptyListIf(package.Options.Count == 0);
        markdown.H2("Option Relationships");
        foreach (DecisionOptionRelationship relationship in package.OptionRelationships
            .OrderBy(relationship => relationship.SourceOptionId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetOptionId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.Type.ToString(), StringComparer.Ordinal))
        {
            markdown.Bullet($"{relationship.SourceOptionId} {relationship.Type} {relationship.TargetOptionId}: {relationship.Rationale}");
            markdown.NestedEvidenceList(relationship.Evidence);
        }

        markdown.EmptyListIf(package.OptionRelationships.Count == 0);
        markdown.H2("Tradeoff Analysis");
        foreach (AnalyzedDecisionOption analyzedOption in package.AnalyzedOptions
            .OrderBy(option => option.OptionId, StringComparer.Ordinal))
        {
            markdown.H3(analyzedOption.OptionId);
            markdown.H4("Benefits");
            foreach (DecisionBenefit benefit in analyzedOption.Benefits.OrderBy(benefit => benefit.Statement, StringComparer.Ordinal))
            {
                markdown.Bullet($"{benefit.Impact}: {benefit.Statement}");
                markdown.NestedEvidenceList(benefit.Evidence);
            }

            markdown.EmptyListIf(analyzedOption.Benefits.Count == 0);
            markdown.H4("Costs");
            foreach (DecisionCost cost in analyzedOption.Costs.OrderBy(cost => cost.Statement, StringComparer.Ordinal))
            {
                markdown.Bullet($"{cost.Impact}: {cost.Statement}");
                markdown.NestedEvidenceList(cost.Evidence);
            }

            markdown.EmptyListIf(analyzedOption.Costs.Count == 0);
            markdown.H4("Risks");
            foreach (DecisionRisk risk in analyzedOption.Risks
                .OrderByDescending(risk => risk.Severity)
                .ThenBy(risk => risk.Statement, StringComparer.Ordinal))
            {
                string unknown = risk.IsUnknown ? "unknown; " : string.Empty;
                markdown.Bullet($"{risk.Severity}: {unknown}{risk.Statement}");
                markdown.NestedEvidenceList(risk.Evidence);
            }

            markdown.EmptyListIf(analyzedOption.Risks.Count == 0);
            markdown.H4("Dependencies");
            foreach (DecisionDependency dependency in analyzedOption.Dependencies.OrderBy(dependency => dependency.Statement, StringComparer.Ordinal))
            {
                markdown.Bullet(dependency.Statement);
                markdown.NestedEvidenceList(dependency.Evidence);
            }

            markdown.EmptyListIf(analyzedOption.Dependencies.Count == 0);
            markdown.H4("Consequences");
            foreach (DecisionConsequence consequence in analyzedOption.Consequences.OrderBy(consequence => consequence.Statement, StringComparer.Ordinal))
            {
                markdown.Bullet($"{consequence.Impact}: {consequence.Statement}");
                markdown.NestedEvidenceList(consequence.Evidence);
            }

            markdown.EmptyListIf(analyzedOption.Consequences.Count == 0);
        }

        markdown.EmptyListIf(package.AnalyzedOptions.Count == 0);
        markdown.H2("Recommendation");
        if (package.Recommendation is null)
        {
            markdown.Paragraph("No recommendation.");
        }
        else
        {
            markdown.Fields(
                ("Mode", package.Recommendation.Mode.ToString()),
                ("Option", string.IsNullOrWhiteSpace(package.Recommendation.OptionId)
                    ? "None"
                    : package.Recommendation.OptionId),
                ("Summary", string.IsNullOrWhiteSpace(package.Recommendation.Summary)
                    ? "None."
                    : package.Recommendation.Summary));
            markdown.Paragraph(package.Recommendation.Rationale);
            markdown.H3("Recommendation Evidence");
            foreach (RecommendationEvidence evidence in package.Recommendation.RecommendationEvidence
                .OrderBy(evidence => evidence.Type.ToString(), StringComparer.Ordinal)
                .ThenBy(evidence => evidence.OptionId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(evidence => evidence.Summary, StringComparer.Ordinal))
            {
                string option = string.IsNullOrWhiteSpace(evidence.OptionId) ? "package" : evidence.OptionId;
                markdown.Bullet($"{evidence.Type} | {option}: {evidence.Summary}");
                markdown.NestedEvidenceList(evidence.Evidence);
            }

            markdown.EmptyListIf(package.Recommendation.RecommendationEvidence.Count == 0);
            markdown.H3("Alternative Explanations");
            foreach (string explanation in package.Recommendation.AlternativeExplanations.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(explanation);
            }

            markdown.EmptyListIf(package.Recommendation.AlternativeExplanations.Count == 0);
        }

        markdown.H2("Open Concerns");
        foreach (string concern in package.OpenConcerns.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(concern);
        }

        markdown.EmptyListIf(package.OpenConcerns.Count == 0);
        markdown.H2("Assumptions");
        foreach (DecisionAssumption assumption in package.Assumptions.OrderBy(assumption => assumption.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{assumption.Id}: {assumption.Statement}");
            markdown.NestedEvidenceList(assumption.Evidence);
        }

        markdown.EmptyListIf(package.Assumptions.Count == 0);
        markdown.H2("Diagnostics");
        if (package.GenerationDiagnostics is not null)
        {
            markdown.H3("Generation");
            foreach (string diagnostic in package.GenerationDiagnostics.Diagnostics.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(diagnostic);
            }

            markdown.EmptyListIf(package.GenerationDiagnostics.Diagnostics.Count == 0);
        }

        if (package.TradeoffAnalysisDiagnostics is not null)
        {
            markdown.H3("Tradeoff Analysis");
            foreach (string diagnostic in package.TradeoffAnalysisDiagnostics.Diagnostics.Order(StringComparer.Ordinal))
            {
                markdown.Bullet(diagnostic);
            }

            markdown.EmptyListIf(package.TradeoffAnalysisDiagnostics.Diagnostics.Count == 0);
        }

        markdown.H2("Evidence");
        markdown.EvidenceList(package.Evidence);
        return markdown.ToString();
    }

    private static string RenderPackageComparison(DecisionPackageComparison comparison)
    {
        var markdown = new MarkdownProjectionBuilder();
        markdown.H1($"{comparison.LeftPackageId}..{comparison.RightPackageId}: {comparison.ProposalId} Package Comparison");
        markdown.Fields(
            ("Proposal", comparison.ProposalId),
            ("Repository", comparison.RepositoryId.ToString()),
            ("Left package", comparison.LeftPackageId),
            ("Right package", comparison.RightPackageId),
            ("Left fingerprint", comparison.LeftPackageFingerprint),
            ("Right fingerprint", comparison.RightPackageFingerprint),
            ("Recommendation changed", comparison.RecommendationChanged.ToString()),
            ("Options changed", comparison.OptionsChanged.ToString()),
            ("Evidence changed", comparison.EvidenceChanged.ToString()),
            ("Risks changed", comparison.RisksChanged.ToString()),
            ("Context fingerprint changed", comparison.ContextFingerprintChanged.ToString()));
        markdown.H2("Field Comparisons");
        foreach (DecisionRevisionFieldComparison field in comparison.FieldComparisons
            .OrderBy(field => field.Field, StringComparer.Ordinal))
        {
            markdown.H3($"{field.Field}: {field.ChangeType}");
            markdown.Fields(
                ("Previous", field.PreviousValue ?? "None."),
                ("Revised", field.RevisedValue ?? "None."));
        }

        markdown.EmptyListIf(comparison.FieldComparisons.Count == 0);
        markdown.H2("Added Options");
        foreach (DecisionOption option in comparison.AddedOptions.OrderBy(option => option.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{option.Id}: {option.Title} - {option.Description}");
        }

        markdown.EmptyListIf(comparison.AddedOptions.Count == 0);
        markdown.H2("Modified Options");
        foreach (DecisionOption option in comparison.ModifiedOptions.OrderBy(option => option.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{option.Id}: {option.Title} - {option.Description}");
        }

        markdown.EmptyListIf(comparison.ModifiedOptions.Count == 0);
        markdown.H2("Removed Options");
        foreach (DecisionOption option in comparison.RemovedOptions.OrderBy(option => option.Id, StringComparer.Ordinal))
        {
            markdown.Bullet($"{option.Id}: {option.Title} - {option.Description}");
        }

        markdown.EmptyListIf(comparison.RemovedOptions.Count == 0);
        markdown.H2("Added Evidence");
        foreach (string evidence in comparison.AddedEvidence.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(evidence);
        }

        markdown.EmptyListIf(comparison.AddedEvidence.Count == 0);
        markdown.H2("Added Risks");
        foreach (string risk in comparison.AddedRisks.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(risk);
        }

        markdown.EmptyListIf(comparison.AddedRisks.Count == 0);
        markdown.H2("Diagnostics");
        foreach (string diagnostic in comparison.Diagnostics.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(diagnostic);
        }

        markdown.EmptyListIf(comparison.Diagnostics.Count == 0);
        return markdown.ToString();
    }

    private static void RenderContextEntries(
        MarkdownProjectionBuilder markdown,
        string title,
        IReadOnlyList<DecisionGenerationContextEntry> entries)
    {
        markdown.H3(title);
        foreach (DecisionGenerationContextEntry entry in entries
            .OrderBy(entry => entry.Id, StringComparer.Ordinal)
            .ThenBy(entry => entry.Statement, StringComparer.Ordinal))
        {
            markdown.Bullet($"{entry.Id}: {entry.Statement}");
            markdown.NestedEvidenceList(entry.Evidence);
        }

        markdown.EmptyListIf(entries.Count == 0);
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
        markdown.H2("Priority Adjustments");
        foreach (DecisionPriorityAdjustment adjustment in (revision.PriorityAdjustments ?? [])
            .OrderBy(item => item.PreviousPriority.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.NewPriority.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.Reason, StringComparer.Ordinal))
        {
            string attribution = string.IsNullOrWhiteSpace(adjustment.Attribution)
                ? "Unspecified"
                : adjustment.Attribution;
            markdown.Bullet($"{adjustment.PreviousPriority} -> {adjustment.NewPriority} | {adjustment.Reason} | attribution: {attribution}");
            markdown.NestedSourceList([adjustment.Source]);
        }

        markdown.EmptyListIf((revision.PriorityAdjustments ?? []).Count == 0);
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
        markdown.H2("Priority Adjustments");
        foreach (DecisionPriorityAdjustment adjustment in comparison.PriorityAdjustments
            .OrderBy(item => item.PreviousPriority.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.NewPriority.ToString(), StringComparer.Ordinal)
            .ThenBy(item => item.Reason, StringComparer.Ordinal))
        {
            string attribution = string.IsNullOrWhiteSpace(adjustment.Attribution)
                ? "Unspecified"
                : adjustment.Attribution;
            markdown.Bullet($"{adjustment.PreviousPriority} -> {adjustment.NewPriority} | {adjustment.Reason} | attribution: {attribution}");
            markdown.NestedSourceList([adjustment.Source]);
        }

        markdown.EmptyListIf(comparison.PriorityAdjustments.Count == 0);
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

    private static string RenderAssimilationRecommendation(DecisionAssimilationRecommendation recommendation)
    {
        var markdown = new MarkdownProjectionBuilder();
        markdown.H1($"{recommendation.DecisionId}: Operational Context Assimilation Recommendation");
        markdown.Fields(
            ("Repository", recommendation.RepositoryId.ToString()),
            ("Created", FormatTimestamp(recommendation.CreatedAt)),
            ("Decision fingerprint", recommendation.DecisionFingerprint),
            ("Context snapshot", recommendation.ContextSnapshotId),
            ("Context fingerprint", recommendation.ContextFingerprint),
            ("Requested by", recommendation.RequestedBy ?? "Unspecified"));
        markdown.H2("Projected Stable Decision");
        markdown.Paragraph(recommendation.ProjectedStableDecision);
        markdown.H2("Rationale");
        markdown.Paragraph(recommendation.Rationale);
        markdown.H2("Notes");
        markdown.Paragraph(recommendation.Notes);
        markdown.H2("Evidence");
        markdown.EvidenceList(recommendation.Evidence);
        markdown.H2("Sources");
        markdown.SourceList(recommendation.Sources);
        markdown.H2("Diagnostics");
        foreach (string diagnostic in recommendation.Diagnostics.Order(StringComparer.Ordinal))
        {
            markdown.Bullet(diagnostic);
        }

        markdown.EmptyListIf(recommendation.Diagnostics.Count == 0);
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

        public void NestedSourceList(IReadOnlyList<DecisionSourceReference> sources, string indent = "  ")
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
