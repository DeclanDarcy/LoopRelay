using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Services;

public sealed class HumanAuthoringBurdenService(
    IRepositoryService repositoryService,
    IDecisionRepository decisionRepository) : IHumanAuthoringBurdenService
{
    public async Task<IReadOnlyList<HumanAuthoringBurdenSignal>> ExtractSignalsAsync(Guid repositoryId, string decisionId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        Decision decision = await GetDecisionAsync(repository, decisionId);
        var signals = new List<HumanAuthoringBurdenSignal>();
        DecisionResolvedProposalSnapshot? snapshot = decision.Resolution?.SourceProposalSnapshot;
        DecisionSourceReference decisionSource = DecisionRecordSource(decision.Id);

        if (snapshot is null)
        {
            signals.Add(new HumanAuthoringBurdenSignal(
                "burden-0001",
                repository.Id,
                decision.Id.Value,
                HumanAuthoringBurden.GenerationBypassed,
                "DecisionResolution",
                "Decision was resolved without generated proposal evidence.",
                [decisionSource]));
            return signals;
        }

        IReadOnlyList<DecisionProposalRevision> revisions =
            await decisionRepository.ListProposalRevisionsAsync(repository, snapshot.ProposalId);
        IReadOnlyList<DecisionRefinementArtifact> refinements =
            await decisionRepository.ListRefinementArtifactsAsync(repository, snapshot.ProposalId);

        foreach (DecisionProposalRevision revision in revisions)
        {
            signals.Add(new HumanAuthoringBurdenSignal(
                NextBurdenId(signals),
                repository.Id,
                decision.Id.Value,
                revision.HumanAuthoringBurden,
                "DecisionProposalRevision",
                $"Revision {revision.Id} classified human authoring burden as {revision.HumanAuthoringBurden}.",
                [ProposalRevisionSource(snapshot.ProposalId, revision.Id, decision.Id)]));
        }

        foreach (DecisionRefinementArtifact refinement in refinements)
        {
            signals.Add(new HumanAuthoringBurdenSignal(
                NextBurdenId(signals),
                repository.Id,
                decision.Id.Value,
                refinement.HumanAuthoringBurden,
                "DecisionRefinementArtifact",
                $"Refinement {refinement.Id} classified human authoring burden as {refinement.HumanAuthoringBurden}.",
                [ProposalRefinementSource(snapshot.ProposalId, refinement.Id, decision.Id)]));
        }

        if (signals.Count == 0)
        {
            signals.Add(new HumanAuthoringBurdenSignal(
                NextBurdenId(signals),
                repository.Id,
                decision.Id.Value,
                HumanAuthoringBurden.ReviewOnly,
                "DecisionResolution",
                "Generated proposal was resolved without persisted refinement or rewrite evidence.",
                [decisionSource]));
        }

        return signals;
    }

    public async Task<HumanAuthoringBurdenReport> GenerateReportAsync(Guid repositoryId)
    {
        Repository repository = await GetRepositoryAsync(repositoryId);
        IReadOnlyList<Decision> decisions = await decisionRepository.ListDecisionsAsync(repository);
        var signals = new List<HumanAuthoringBurdenSignal>();
        foreach (Decision decision in decisions)
        {
            signals.AddRange(await ExtractSignalsAsync(repositoryId, decision.Id.Value));
        }

        HumanAuthoringBurden EffectiveBurden(Decision decision)
        {
            IReadOnlyList<HumanAuthoringBurdenSignal> decisionSignals = signals
                .Where(signal => string.Equals(signal.DecisionId, decision.Id.Value, StringComparison.Ordinal))
                .ToArray();
            return decisionSignals
                .Select(signal => signal.Burden)
                .DefaultIfEmpty(HumanAuthoringBurden.Unknown)
                .OrderByDescending(BurdenWeight)
                .First();
        }

        IReadOnlyList<HumanAuthoringBurden> burdens = decisions.Select(EffectiveBurden).ToArray();
        return new HumanAuthoringBurdenReport(
            repository.Id,
            decisions.Count,
            burdens.Count(burden => burden == HumanAuthoringBurden.ReviewOnly),
            burdens.Count(burden => burden == HumanAuthoringBurden.MinorEdit),
            burdens.Count(burden => burden == HumanAuthoringBurden.MajorRefinement),
            burdens.Count(burden => burden == HumanAuthoringBurden.FullRewrite),
            burdens.Count(burden => burden == HumanAuthoringBurden.GenerationBypassed),
            burdens.Count(burden => burden == HumanAuthoringBurden.Unknown),
            signals);
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

    private static string NextBurdenId(IReadOnlyCollection<HumanAuthoringBurdenSignal> signals)
    {
        return $"burden-{signals.Count + 1:0000}";
    }

    private static int BurdenWeight(HumanAuthoringBurden burden)
    {
        return burden switch
        {
            HumanAuthoringBurden.GenerationBypassed => 5,
            HumanAuthoringBurden.FullRewrite => 4,
            HumanAuthoringBurden.MajorRefinement => 3,
            HumanAuthoringBurden.MinorEdit => 2,
            HumanAuthoringBurden.ReviewOnly => 1,
            _ => 0
        };
    }

    private static DecisionSourceReference DecisionRecordSource(DecisionId decisionId)
    {
        return new DecisionSourceReference(
            "DecisionRecord",
            $".agents/decisions/records/{decisionId.Value}/decision.json",
            DecisionId: decisionId);
    }

    private static DecisionSourceReference ProposalRevisionSource(string proposalId, string revisionId, DecisionId decisionId)
    {
        return new DecisionSourceReference(
            "DecisionProposalRevision",
            $".agents/decisions/proposals/{proposalId}/revisions/{revisionId}.json",
            ItemId: revisionId,
            DecisionId: decisionId,
            ProposalId: proposalId);
    }

    private static DecisionSourceReference ProposalRefinementSource(string proposalId, string refinementId, DecisionId decisionId)
    {
        return new DecisionSourceReference(
            "DecisionRefinementArtifact",
            $".agents/decisions/proposals/{proposalId}/refinements/{refinementId}.json",
            ItemId: refinementId,
            DecisionId: decisionId,
            ProposalId: proposalId);
    }
}
