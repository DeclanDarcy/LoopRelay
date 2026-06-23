using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class DecisionQualitySignalService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository,
    IHumanAuthoringBurdenService humanAuthoringBurdenService) : IDecisionQualitySignalService
{
    public async Task<IReadOnlyList<DecisionQualitySignal>> ExtractSignalsAsync(Guid repositoryId, string decisionId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        Decision decision = await GetDecisionAsync(repository, decisionId);
        var signals = new List<DecisionQualitySignal>();
        DecisionResolution? resolution = decision.Resolution;
        DecisionResolvedProposalSnapshot? snapshot = resolution?.SourceProposalSnapshot;
        DecisionSourceReference decisionSource = DecisionRecordSource(decision.Id);

        if (resolution is null)
        {
            Add(signals, repository, decision, "Lifecycle", QualitySignalDirection.Neutral, QualitySignalSeverity.Info,
                "Decision has not been resolved.", "Quality assessment is limited until a human resolution exists.", [decisionSource]);
            return signals;
        }

        switch (resolution.Outcome)
        {
            case DecisionOutcome.Accepted:
                Add(signals, repository, decision, "ResolutionOutcome", QualitySignalDirection.Positive, QualitySignalSeverity.Medium,
                    "Generated decision was accepted.", "Accepted generated decisions indicate useful decision-production output.", [decisionSource]);
                break;
            case DecisionOutcome.Rejected:
                Add(signals, repository, decision, "ResolutionOutcome", QualitySignalDirection.Negative, QualitySignalSeverity.High,
                    "Generated decision was rejected.", "Rejected outcomes indicate the generated package did not produce acceptable guidance.", [decisionSource]);
                break;
            case DecisionOutcome.Deferred:
                Add(signals, repository, decision, "ResolutionOutcome", QualitySignalDirection.Neutral, QualitySignalSeverity.Low,
                    "Generated decision was deferred.", "Deferred outcomes preserve governance but do not validate decision usefulness yet.", [decisionSource]);
                break;
        }

        if (snapshot is null)
        {
            Add(signals, repository, decision, "HumanAuthoringBurden", QualitySignalDirection.Negative, QualitySignalSeverity.Critical,
                "Decision bypassed generated proposal content.", "Generation bypass means the system did not replace human decision authoring.", [decisionSource]);
            return signals;
        }

        DecisionRecommendation? recommendation = snapshot.Recommendation;
        if (resolution.Outcome == DecisionOutcome.Accepted &&
            recommendation is not null &&
            recommendation.Mode != RecommendationMode.NoRecommendation &&
            string.Equals(recommendation.OptionId, resolution.SelectedOptionId, StringComparison.Ordinal))
        {
            Add(signals, repository, decision, "RecommendationQuality", QualitySignalDirection.Positive, QualitySignalSeverity.High,
                "Human accepted the recommended option.", "Recommendation and human resolution aligned.", [ProposalSource(snapshot, decision.Id)]);
        }

        if (resolution.RecommendationDiverged)
        {
            Add(signals, repository, decision, "RecommendationQuality", QualitySignalDirection.Negative, QualitySignalSeverity.Medium,
                "Human selected an alternative to the recommendation.", "Recommendation divergence lowers recommendation quality but may still validate option usefulness.", [ProposalSource(snapshot, decision.Id)]);
            Add(signals, repository, decision, "OptionQuality", QualitySignalDirection.Positive, QualitySignalSeverity.Medium,
                "Generated alternative option was used.", "Alternative utilization indicates the option set contained useful generated content.", [SelectedOptionSource(snapshot, decision.Id, resolution.SelectedOptionId)]);
        }

        IReadOnlyList<DecisionRefinementArtifact> refinements =
            await decisionRepository.ListRefinementArtifactsAsync(repository, snapshot.ProposalId);
        IReadOnlyList<DecisionProposalRevision> revisions =
            await decisionRepository.ListProposalRevisionsAsync(repository, snapshot.ProposalId);
        if (refinements.Count > 0)
        {
            Add(signals, repository, decision, "HumanEffort", QualitySignalDirection.Negative, QualitySignalSeverity.Medium,
                "Decision required scoped regeneration.", $"{refinements.Count} refinement artifact(s) were recorded before resolution.", [ProposalSource(snapshot, decision.Id)]);
        }

        if (revisions.Count > 0)
        {
            Add(signals, repository, decision, "HumanEffort", QualitySignalDirection.Negative, QualitySignalSeverity.Medium,
                "Decision required direct proposal revision.", $"{revisions.Count} proposal revision(s) were recorded before resolution.", [ProposalSource(snapshot, decision.Id)]);
        }

        foreach (HumanAuthoringBurdenSignal burdenSignal in await humanAuthoringBurdenService.ExtractSignalsAsync(repositoryId, decisionId))
        {
            QualitySignalDirection direction = burdenSignal.Burden switch
            {
                HumanAuthoringBurden.ReviewOnly => QualitySignalDirection.Positive,
                HumanAuthoringBurden.MinorEdit => QualitySignalDirection.Positive,
                HumanAuthoringBurden.Unknown => QualitySignalDirection.Neutral,
                _ => QualitySignalDirection.Negative
            };
            QualitySignalSeverity severity = burdenSignal.Burden switch
            {
                HumanAuthoringBurden.GenerationBypassed => QualitySignalSeverity.Critical,
                HumanAuthoringBurden.FullRewrite => QualitySignalSeverity.High,
                HumanAuthoringBurden.MajorRefinement => QualitySignalSeverity.Medium,
                _ => QualitySignalSeverity.Low
            };
            Add(signals, repository, decision, "HumanAuthoringBurden", direction, severity,
                $"Human authoring burden: {burdenSignal.Burden}.", burdenSignal.Summary, burdenSignal.Sources);
        }

        return signals;
    }

    private async Task<Repository> GetRepositoryAsync(Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }

    private async Task<Decision> GetDecisionAsync(Repository repository, string decisionId)
    {
        DecisionId id = DecisionId.Parse(decisionId);
        Decision? decision = await decisionRepository.GetDecisionAsync(repository, id);
        return decision ?? throw new KeyNotFoundException($"Decision was not found: {id.Value}");
    }

    private static void Add(
        ICollection<DecisionQualitySignal> signals,
        Repository repository,
        Decision decision,
        string category,
        QualitySignalDirection direction,
        QualitySignalSeverity severity,
        string summary,
        string detail,
        IReadOnlyList<DecisionSourceReference> sources)
    {
        signals.Add(new DecisionQualitySignal(
            $"signal-{signals.Count + 1:0000}",
            repository.Id,
            decision.Id.Value,
            category,
            direction,
            severity,
            summary,
            detail,
            sources));
    }

    private static DecisionSourceReference DecisionRecordSource(DecisionId decisionId)
    {
        return new DecisionSourceReference(
            "DecisionRecord",
            $".agents/decisions/records/{decisionId.Value}/decision.json",
            DecisionId: decisionId);
    }

    private static DecisionSourceReference ProposalSource(DecisionResolvedProposalSnapshot snapshot, DecisionId decisionId)
    {
        return new DecisionSourceReference(
            "DecisionProposal",
            $".agents/decisions/proposals/{snapshot.ProposalId}/proposal.json",
            DecisionId: decisionId,
            ProposalId: snapshot.ProposalId,
            CandidateId: snapshot.CandidateId);
    }

    private static DecisionSourceReference SelectedOptionSource(
        DecisionResolvedProposalSnapshot snapshot,
        DecisionId decisionId,
        string selectedOptionId)
    {
        DecisionOption? option = snapshot.Options.FirstOrDefault(option =>
            string.Equals(option.Id, selectedOptionId, StringComparison.Ordinal));
        return new DecisionSourceReference(
            "DecisionOption",
            $".agents/decisions/proposals/{snapshot.ProposalId}/proposal.json",
            Section: "Options",
            ItemId: selectedOptionId,
            DecisionId: decisionId,
            ProposalId: snapshot.ProposalId,
            CandidateId: snapshot.CandidateId,
            Excerpt: option?.Title);
    }
}
